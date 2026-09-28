namespace AINoteSummarizer

open System
open System.Net.Http
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open System.Threading.Tasks

type SummaryLength =
    | Short
    | Medium
    | Detailed

type Provider =
    | OpenAI
    | Local

type SummaryResult = { Text: string; Source: string }

module Summarizer =

    // ---------- konfiguracja (zmienne srodowiskowe) ----------

    let private env (name: string) =
        match Environment.GetEnvironmentVariable name with
        | null -> ""
        | v -> v.Trim()

    let private openAiKey () = Settings.apiKey ()

    let private openAiModel () = Settings.model ()

    /// Chmura (OpenAI lub inny serwis zgodny z API OpenAI), gdy jest klucz (zapisany w aplikacji lub w OPENAI_API_KEY); w przeciwnym razie tryb lokalny.
    let activeProvider () =
        if openAiKey () <> "" then OpenAI else Local

    /// Nazwa hosta z adresu API, np. "api.groq.com".
    let private hostName () =
        try Uri(Settings.baseUrl ()).Host with _ -> "API"

    let providerLabel () =
        match activeProvider () with
        | OpenAI ->
            let h = hostName ()
            if h = "api.openai.com" then "OpenAI Online"
            elif h = "api.groq.com" then "Groq Online"
            else h + " Online"
        | Local -> "Tryb lokalny"

    // ---------- tryb lokalny (bez internetu / bez klucza) ----------

    let private stopWords =
        set [ "i"; "w"; "z"; "na"; "do"; "że"; "się"; "to"; "jest"; "nie"; "po"; "za"; "od"
              "jak"; "ale"; "czy"; "oraz"; "lub"; "przez"; "dla"; "tak"; "być"; "był"; "była"
              "the"; "and"; "for"; "that"; "with"; "was"; "are"; "this"; "from"; "have"; "not" ]

    let private splitSentences (text: string) =
        Regex.Split(text.Replace("\r\n", "\n"), @"(?<=[\.\!\?…])\s+|\n+")
        |> Array.map (fun s -> s.Trim())
        |> Array.filter (fun s -> s.Length > 0)

    let private words (s: string) =
        Regex.Matches(s.ToLowerInvariant(), @"\p{L}{3,}")
        |> Seq.map (fun m -> m.Value)
        |> Seq.filter (fun w -> not (stopWords.Contains w))
        |> Array.ofSeq

    let summarizeLocal (length: SummaryLength) (text: string) : string =
        let sentences = splitSentences text
        let n = sentences.Length

        let target =
            match length with
            | Short -> max 1 (min 3 (n / 5))
            | Medium -> max 2 (n * 3 / 10)
            | Detailed -> max 3 (n / 2)

        if n <= target then
            text.Trim()
        else
            let freq =
                sentences
                |> Array.collect words
                |> Array.countBy id
                |> Map.ofArray

            let score i (s: string) =
                let ws = words s
                if ws.Length = 0 then
                    0.0
                else
                    let sum = ws |> Array.sumBy (fun w -> float (Map.find w freq))
                    let boost = if i = 0 then 1.3 else 1.0
                    boost * sum / sqrt (float ws.Length)

            sentences
            |> Array.mapi (fun i s -> i, score i s)
            |> Array.sortByDescending snd
            |> Array.truncate target
            |> Array.map fst
            |> Array.sort
            |> Array.map (fun i -> "• " + sentences.[i])
            |> String.concat "\n"

    // ---------- wspolne dla trybu AI ----------

    let private http = new HttpClient(Timeout = TimeSpan.FromSeconds 90.0)

    let private systemPrompt (length: SummaryLength) =
        let lengthHint =
            match length with
            | Short -> "Napisz bardzo krótkie streszczenie: 1-2 zdania."
            | Medium -> "Napisz zwięzłe streszczenie w formie krótkiej listy punktów (3-5 punktów)."
            | Detailed -> "Napisz szczegółowe streszczenie w formie listy punktów, zachowując ważne fakty, liczby, daty i ustalenia."

        "Jesteś asystentem, który skraca notatki. "
        + lengthHint
        + " Odpowiadaj w tym samym języku, w którym napisana jest notatka. "
        + "Nie dodawaj wstępu ani komentarza - zwróć wyłącznie streszczenie."

    /// OpenAI zwraca bledy jako {"error": {"message": "..."}}.
    let private extractError (json: string) =
        try
            use doc = JsonDocument.Parse json
            doc.RootElement.GetProperty("error").GetProperty("message").GetString()
        with _ ->
            json

    let private send (req: HttpRequestMessage) : Task<string> =
        task {
            use! resp = http.SendAsync req
            let! json = resp.Content.ReadAsStringAsync()

            if not resp.IsSuccessStatusCode then
                return failwithf "API %d: %s" (int resp.StatusCode) (extractError json)
            else
                return json
        }

    // ---------- OpenAI (Chat Completions) ----------

    let summarizeOpenAI (length: SummaryLength) (text: string) : Task<string> =
        task {
            // Celowo bez max_tokens / temperature: rozne serwisy zgodne z API OpenAI
            // (i modele rozumujace) inaczej je traktuja, a domyslne wartosci dzialaja wszedzie.
            let body =
                JsonSerializer.Serialize(
                    {| model = openAiModel ()
                       messages =
                        [| {| role = "system"; content = systemPrompt length |}
                           {| role = "user"; content = text |} |] |}
                )

            let url = (Settings.baseUrl ()).TrimEnd('/') + "/chat/completions"
            use req = new HttpRequestMessage(HttpMethod.Post, url)
            req.Headers.Add("Authorization", "Bearer " + openAiKey ())
            req.Content <- new StringContent(body, Encoding.UTF8, "application/json")

            let! json = send req
            use doc = JsonDocument.Parse json

            let content =
                doc.RootElement
                    .GetProperty("choices")
                    .[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()

            match content with
            | null -> return failwith "OpenAI zwróciło pustą odpowiedź."
            | c -> return c.Trim()
        }

    // ---------- punkt wejscia ----------

    /// Uzywa OpenAI, gdy jest klucz. Przy bledzie API wraca do trybu lokalnego,
    /// zeby aplikacja zawsze zwrocila wynik, a blad pokazuje w statusie.
    let summarize (length: SummaryLength) (text: string) : Task<SummaryResult> =
        task {
            match activeProvider () with
            | Local ->
                return { Text = summarizeLocal length text
                         Source = "tryb lokalny (dodaj klucz przyciskiem „Klucz API”)" }
            | OpenAI ->
                try
                    let! r = summarizeOpenAI length text
                    return { Text = r; Source = $"{hostName ()} ({openAiModel ()})" }
                with ex ->
                    let msg =
                        if ex.Message.Length > 120 then ex.Message.Substring(0, 120) + "…" else ex.Message

                    return { Text = summarizeLocal length text
                             Source = $"tryb lokalny (błąd API: {msg})" }
        }
