namespace AINoteSummarizer

open Avalonia.Controls
open Avalonia.Interactivity
open Avalonia.Markup.Xaml

type MainWindow() as this =
    inherit Window()

    let mutable inputTextBox : TextBox = null
    let mutable outputTextBox : TextBox = null
    let mutable inputCounter : TextBlock = null
    let mutable statusTextBlock : TextBlock = null
    let mutable summarizeButton : Button = null
    let mutable clearButton : Button = null
    let mutable copyButton : Button = null
    let mutable lengthComboBox : ComboBox = null

    do
        AvaloniaXamlLoader.Load(this)

        inputTextBox <-
            this.FindControl<TextBox>("InputTextBox")

        outputTextBox <-
            this.FindControl<TextBox>("OutputTextBox")

        inputCounter <-
            this.FindControl<TextBlock>("InputCounter")

        statusTextBlock <-
            this.FindControl<TextBlock>("StatusTextBlock")

        summarizeButton <-
            this.FindControl<Button>("SummarizeButton")

        clearButton <-
            this.FindControl<Button>("ClearButton")

        copyButton <-
            this.FindControl<Button>("CopyButton")

        lengthComboBox <-
            this.FindControl<ComboBox>("LengthComboBox")

        inputTextBox.TextChanged.Add(
            fun _ ->
                let count =
                    inputTextBox.Text.Length

                inputCounter.Text <-
                    $"{count} znaków"
        )

        clearButton.Click.Add(
            fun _ ->
                inputTextBox.Text <- ""
                outputTextBox.Text <- ""

                inputCounter.Text <-
                    "0 znaków"

                statusTextBlock.Text <-
                    "Wyczyszczono."
        )

        copyButton.Click.Add(
            fun _ ->
                if not (
                    System.String.IsNullOrWhiteSpace(
                        outputTextBox.Text
                    )
                ) then

                    statusTextBlock.Text <-
                        "Gotowe do skopiowania."

        )

        summarizeButton.Click.Add(
            fun _ ->

                statusTextBlock.Text <-
                    "Funkcja AI zostanie dodana za chwilę."
        )
