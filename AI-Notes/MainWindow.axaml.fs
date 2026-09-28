namespace AINoteSummarizer

open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Input.Platform
open Avalonia.Markup.Xaml
open Avalonia.Media

type MainWindow() as this =
    inherit Window()

    do AvaloniaXamlLoader.Load(this)

    let inputBox = this.FindControl<TextBox>("InputTextBox")
    let outputBox = this.FindControl<TextBox>("OutputTextBox")
    let inputCounter = this.FindControl<TextBlock>("InputCounter")
    let status = this.FindControl<TextBlock>("StatusTextBlock")
    let summarizeButton = this.FindControl<Button>("SummarizeButton")
    let clearButton = this.FindControl<Button>("ClearButton")
    let copyButton = this.FindControl<Button>("CopyButton")
    let lengthBox = this.FindControl<ComboBox>("LengthComboBox")
    let aiLabel = this.FindControl<TextBlock>("AiStatusText")
    let aiDot = this.FindControl<Ellipse>("AiStatusDot")

    // panel klucza API
    let keyButton = this.FindControl<Button>("KeyButton")
    let keyPanel = this.FindControl<Border>("KeyPanel")
    let keyBox = this.FindControl<TextBox>("ApiKeyTextBox")
    let keyStatus = this.FindControl<TextBlock>("KeyStatusText")
    let saveKeyButton = this.FindControl<Button>("SaveKeyButton")
    let removeKeyButton = this.FindControl<Button>("RemoveKeyButton")
    let providerBox = this.FindControl<ComboBox>("ProviderComboBox")
    let urlBox = this.FindControl<TextBox>("BaseUrlTextBox")
    let modelBox = this.FindControl<TextBox>("ModelTextBox")
    let keyHint = this.FindControl<TextBlock>("KeyHintText")

    let mutable loading = true

    let textOf (box: TextBox) =
        match box.Text with
        | null -> ""
        | t -> t

    let charsLabel n =
        let word =
            if n = 1 then "znak"
            elif n % 10 >= 2 && n % 10 <= 4 && not (n % 100 >= 12 && n % 100 <= 14) then "znaki"
            else "znaków"
        $"{n} {word}"

    let refreshAiStatus () =
        aiLabel.Text <- Summarizer.providerLabel ()

        let color =
            if Summarizer.activeProvider () = Local then "#FBBF24" else "#4ADE80"

        aiDot.Fill <- SolidColorBrush(Color.Parse color) :> IBrush
        keyStatus.Text <- $"Źródło klucza: {Settings.keySource ()}"

    let updateHint () =
        keyHint.Text <-
            match providerBox.SelectedIndex with
            | 0 -> "Klucz OpenAI: platform.openai.com/api-keys (konto API wymaga doładowania środków)."
            | 1 -> "Darmowy klucz bez karty: console.groq.com/keys (wystarczy e-mail). Nazwę modelu możesz zmienić."
            | _ -> "Wpisz adres serwisu zgodnego z API OpenAI (kończący się na /v1) oraz nazwę modelu."

    let saveSettings () =
        let key = (textOf keyBox).Trim()
        let url = (textOf urlBox).Trim()
        let model = (textOf modelBox).Trim()

        if not (url.StartsWith "http://" || url.StartsWith "https://") then
            keyStatus.Text <- "Adres API musi zaczynać się od http:// lub https://"
        elif model.Length = 0 then
            keyStatus.Text <- "Podaj nazwę modelu."
        else
            try
                Settings.saveConfig (url, model)
                if key.Length > 0 then Settings.save key
                keyBox.Text <- ""
                refreshAiStatus ()

                if Settings.apiKey () = "" then
                    keyStatus.Text <- "Ustawienia zapisane. Dodaj jeszcze klucz API."
                else
                    status.Text <- "Ustawienia API zapisane."
            with ex ->
                keyStatus.Text <- $"Nie udało się zapisać: {ex.Message}"

    let removeKey () =
        try
            Settings.clear ()
            keyBox.Text <- ""
            refreshAiStatus ()
            status.Text <- "Zapisany klucz usunięty."
        with ex ->
            keyStatus.Text <- $"Nie udało się usunąć klucza: {ex.Message}"

    let summarize () =
        task {
            let text = (textOf inputBox).Trim()

            if text.Length = 0 then
                status.Text <- "Wpisz lub wklej notatkę."
            else
                summarizeButton.IsEnabled <- false
                status.Text <- "Skracam notatkę…"

                let length =
                    match lengthBox.SelectedIndex with
                    | 1 -> Medium
                    | 2 -> Detailed
                    | _ -> Short

                try
                    let! result = Summarizer.summarize length text
                    outputBox.Text <- result.Text
                    status.Text <- $"Gotowe • {result.Source}"
                with ex ->
                    status.Text <- $"Błąd: {ex.Message}"

                summarizeButton.IsEnabled <- true
        }

    let copy () =
        task {
            let text = textOf outputBox

            if text.Trim().Length = 0 then
                status.Text <- "Brak wyniku do skopiowania."
            else
                match this.Clipboard with
                | null -> status.Text <- "Schowek jest niedostępny."
                | clipboard ->
                    do! clipboard.SetTextAsync(text)
                    status.Text <- "Skopiowano do schowka."
        }

    do
        refreshAiStatus ()

        inputBox.TextChanged.Add(fun _ ->
            inputCounter.Text <- charsLabel (textOf inputBox).Length)

        clearButton.Click.Add(fun _ ->
            inputBox.Text <- ""
            outputBox.Text <- ""
            inputCounter.Text <- charsLabel 0
            status.Text <- "Wyczyszczono.")

        copyButton.Click.Add(fun _ -> copy () |> ignore)
        summarizeButton.Click.Add(fun _ -> summarize () |> ignore)

        keyButton.Click.Add(fun _ ->
            keyPanel.IsVisible <- not keyPanel.IsVisible
            refreshAiStatus ()
            if keyPanel.IsVisible then keyBox.Focus() |> ignore)

        saveKeyButton.Click.Add(fun _ -> saveSettings ())
        removeKeyButton.Click.Add(fun _ -> removeKey ())

        keyBox.KeyDown.Add(fun e ->
            if e.Key = Key.Enter then
                saveSettings ()
                e.Handled <- true)

        providerBox.SelectionChanged.Add(fun _ ->
            if not loading then
                match providerBox.SelectedIndex with
                | 0 ->
                    urlBox.Text <- "https://api.openai.com/v1"
                    modelBox.Text <- "gpt-5-mini"
                | 1 ->
                    urlBox.Text <- "https://api.groq.com/openai/v1"
                    modelBox.Text <- "openai/gpt-oss-120b"
                | _ -> ()

                updateHint ())

        // stan poczatkowy pol z zapisanych ustawien
        let url = Settings.baseUrl ()
        urlBox.Text <- url
        modelBox.Text <- Settings.model ()

        providerBox.SelectedIndex <-
            if url.Contains "api.openai.com" then 0
            elif url.Contains "api.groq.com" then 1
            else 2

        loading <- false
        updateHint ()
