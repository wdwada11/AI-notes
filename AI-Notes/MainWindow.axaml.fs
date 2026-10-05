namespace AINoteSummarizer

open System
open System.Diagnostics
open System.IO
open System.Text
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Input
open Avalonia.Input.Platform
open Avalonia.Markup.Xaml
open Avalonia.Media
open Avalonia.Platform.Storage

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
    let saveButton = this.FindControl<Button>("SaveButton")
    let printButton = this.FindControl<Button>("PrintButton")
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

    let save () =
        task {
            let text = textOf outputBox

            if text.Trim().Length = 0 then
                status.Text <- "Brak wyniku do zapisania."
            else
                let topLevel = TopLevel.GetTopLevel this

                match topLevel with
                | null -> status.Text <- "Nie można otworzyć okna zapisu."
                | topLevel ->
                    let options = FilePickerSaveOptions()
                    options.Title <- "Zapisz skróconą notatkę"
                    options.SuggestedFileName <- "skrocona-notatka.txt"
                    options.DefaultExtension <- "txt"

                    options.FileTypeChoices <-
                        ResizeArray(
                            [ FilePickerFileType("Plik tekstowy", Patterns = ResizeArray [ "*.txt" ])
                              FilePickerFileType("Markdown", Patterns = ResizeArray [ "*.md" ]) ]
                        )

                    let! file = topLevel.StorageProvider.SaveFilePickerAsync options

                    match file with
                    | null -> status.Text <- "Zapis anulowany."
                    | file ->
                        try
                            use! stream = file.OpenWriteAsync()
                            use writer = new StreamWriter(stream, Encoding.UTF8)
                            do! writer.WriteAsync text
                            status.Text <- $"Zapisano: {file.Name}"
                        with ex ->
                            status.Text <- $"Nie udało się zapisać pliku: {ex.Message}"
        }

    let print () =
        task {
            let text = textOf outputBox

            if text.Trim().Length = 0 then
                status.Text <- "Brak wyniku do wydrukowania."
            else
                try
                    let html =
                        let body =
                            text
                                .Replace("&", "&amp;")
                                .Replace("<", "&lt;")
                                .Replace(">", "&gt;")
                                .Replace("\n", "<br/>")

                        $"""<!DOCTYPE html>
<html lang="pl"><head><meta charset="utf-8"/><title>Skrócona notatka</title>
<style>
  body {{ font-family: Segoe UI, Arial, sans-serif; font-size: 14pt; line-height: 1.5; margin: 40px; white-space: pre-wrap; }}
  h1 {{ font-size: 16pt; }}
</style></head>
<body>
<h1>Skrócona notatka</h1>
<div>{body}</div>
<script>window.onload = function() {{ window.print(); }};</script>
</body></html>"""

                    let path = Path.Combine(Path.GetTempPath(), $"notatka-{Guid.NewGuid():N}.html")
                    File.WriteAllText(path, html, Encoding.UTF8)

                    let psi = ProcessStartInfo(path, UseShellExecute = true)
                    Process.Start psi |> ignore

                    status.Text <- "Otworzono w przeglądarce — wydrukuj przez Ctrl+P."
                with ex ->
                    status.Text <- $"Nie udało się otworzyć wydruku: {ex.Message}"
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
        saveButton.Click.Add(fun _ -> save () |> ignore)
        printButton.Click.Add(fun _ -> print () |> ignore)
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

                )

        // stan poczatkowy pol z zapisanych ustawien
        let url = Settings.baseUrl ()
        urlBox.Text <- url
        modelBox.Text <- Settings.model ()

        providerBox.SelectedIndex <-
            if url.Contains "api.openai.com" then 0
            elif url.Contains "api.groq.com" then 1
            else 2

        loading <- false
