namespace AINoteSummarizer

open Avalonia.Controls
open Avalonia.Controls.Shapes
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
        aiLabel.Text <- Summarizer.providerLabel ()

        if Summarizer.activeProvider () = Local then
            aiDot.Fill <- SolidColorBrush(Color.Parse "#FBBF24")

        inputBox.TextChanged.Add(fun _ ->
            inputCounter.Text <- charsLabel (textOf inputBox).Length)

        clearButton.Click.Add(fun _ ->
            inputBox.Text <- ""
            outputBox.Text <- ""
            inputCounter.Text <- charsLabel 0
            status.Text <- "Wyczyszczono.")

        copyButton.Click.Add(fun _ -> copy () |> ignore)
        summarizeButton.Click.Add(fun _ -> summarize () |> ignore)
