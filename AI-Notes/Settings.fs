namespace AINoteSummarizer

open System
open System.IO
open System.Security.Cryptography
open System.Text

module Settings =

    let private dir =
        Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.ApplicationData, "AINoteSummarizer")

    let private file = Path.Combine(dir, "openai.key")

    let private load () : string =
        try
            if File.Exists file then
                let raw = File.ReadAllBytes file

                let bytes =
                    if OperatingSystem.IsWindows() then
                        ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser)
                    else
                        raw

                Encoding.UTF8.GetString(bytes).Trim()
            else
                ""
        with _ ->
            ""

    let mutable private savedKey = load ()

    let defaultBaseUrl = "https://api.openai.com/v1"
    let defaultModel = "gpt-5-mini"

    let private configFile = Path.Combine(dir, "config.txt")

    let private loadConfig () : string * string =
        try
            if File.Exists configFile then
                match File.ReadAllLines configFile with
                | [| u; m |] -> u.Trim(), m.Trim()
                | _ -> "", ""
            else
                "", ""
        with _ ->
            "", ""

    let mutable private savedConfig = loadConfig ()

    let private env (name: string) =
        match Environment.GetEnvironmentVariable name with
        | null -> ""
        | v -> v.Trim()

    let baseUrl () : string =
        match fst savedConfig with
        | "" ->
            match env "OPENAI_BASE_URL" with
            | "" -> defaultBaseUrl
            | u -> u
        | u -> u

    let model () : string =
        match snd savedConfig with
        | "" ->
            match env "OPENAI_MODEL" with
            | "" -> defaultModel
            | m -> m
        | m -> m

    let saveConfig (url: string, model: string) : unit =
        Directory.CreateDirectory dir |> ignore
        File.WriteAllLines(configFile, [| url.Trim(); model.Trim() |])
        savedConfig <- (url.Trim(), model.Trim())

    let private envKey () =
        match Environment.GetEnvironmentVariable "OPENAI_API_KEY" with
        | null -> ""
        | v -> v.Trim()

    let apiKey () : string = if savedKey <> "" then savedKey else envKey ()

    let keySource () : string =
        if savedKey <> "" then "zapisany w aplikacji"
        elif envKey () <> "" then "ze zmiennej OPENAI_API_KEY"
        else "brak klucza"

    let save (key: string) : unit =
        let key = key.Trim()
        Directory.CreateDirectory dir |> ignore
        let bytes = Encoding.UTF8.GetBytes key

        let data =
            if OperatingSystem.IsWindows() then
                ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)
            else
                bytes

        File.WriteAllBytes(file, data)

        if not (OperatingSystem.IsWindows()) then
            File.SetUnixFileMode(file, UnixFileMode.UserRead ||| UnixFileMode.UserWrite)

        savedKey <- key

    let clear () : unit =
        if File.Exists file then File.Delete file
        savedKey <- ""
