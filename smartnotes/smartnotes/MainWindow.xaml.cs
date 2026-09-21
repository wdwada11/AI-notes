using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace AINoteSummarizer
{
    public partial class MainWindow : Window
    {
        private const string ApiKey = "TWOJ_KLUCZ_OPENAI_API";

        private static readonly HttpClient _httpClient = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void InputTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
        }

        private async void SummarizeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string note = InputTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(note))
            {
                MessageBox.Show(
                    "Wklej najpierw notatkę.",
                    "Brak tekstu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (ApiKey == "TWOJ_KLUCZ_OPENAI_API")
            {
                MessageBox.Show(
                    "Uzupełnij klucz API OpenAI.",
                    "Brak klucza API",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            SummarizeButton.IsEnabled = false;
            OutputTextBox.Text = "";
            StatusTextBlock.Text = "AI analizuje notatkę...";

            try
            {
                string length = GetSelectedLength();

                string summary =
                    await SummarizeWithAI(note, length);

                OutputTextBox.Text = summary;
                StatusTextBlock.Text = "Gotowe!";
            }
            catch (Exception ex)
            {
                OutputTextBox.Text =
                    "Wystąpił błąd podczas komunikacji z AI.";

                StatusTextBlock.Text = "Błąd";

                MessageBox.Show(
                    ex.Message,
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SummarizeButton.IsEnabled = true;
            }
        }

        private void ClearButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            InputTextBox.Clear();
            OutputTextBox.Clear();

            StatusTextBlock.Text = "Wyczyszczono.";
        }

        private void CopyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(OutputTextBox.Text))
            {
                MessageBox.Show(
                    "Nie ma podsumowania do skopiowania.",
                    "Brak tekstu",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            Clipboard.SetText(OutputTextBox.Text);

            StatusTextBlock.Text = "Skopiowano do schowka.";
        }

        private string GetSelectedLength()
        {
            if (LengthComboBox.SelectedItem is ComboBoxItem item)
            {
                return item.Content?.ToString() ?? "Krótka";
            }

            return "Krótka";
        }

        private async Task<string> SummarizeWithAI(
            string note,
            string length)
        {
            string prompt = $"""
                Jesteś asystentem do nauki.

                Skróć poniższą notatkę w języku polskim.

                Wymagana długość: {length}.

                Zachowaj:
                - najważniejsze informacje,
                - definicje,
                - daty,
                - wzory,
                - kluczowe pojęcia.

                Usuń:
                - powtórzenia,
                - niepotrzebne przykłady,
                - długie opisy.

                Nie dodawaj informacji, których nie ma w notatce.
                Zwróć wyłącznie gotowy skrót.

                NOTATKA:
                {note}
                """;

            var requestBody = new
            {
                model = "gpt-4o-mini",

                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content =
                            "Jesteś pomocnym asystentem edukacyjnym."
                    },

                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },

                temperature = 0.3
            };

            string json =
                JsonSerializer.Serialize(requestBody);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/chat/completions");

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer",
                    ApiKey);

            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            using HttpResponseMessage response =
                await _httpClient.SendAsync(request);

            string responseText =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"API Error: {response.StatusCode}\n\n" +
                    responseText);
            }

            using JsonDocument document =
                JsonDocument.Parse(responseText);

            JsonElement choices =
                document.RootElement.GetProperty("choices");

            if (choices.GetArrayLength() == 0)
            {
                throw new Exception(
                    "API nie zwróciło żadnej odpowiedzi.");
            }

            string result = choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            if (string.IsNullOrWhiteSpace(result))
            {
                throw new Exception(
                    "API zwróciło pustą odpowiedź.");
            }

            return result.Trim();
        }
    }
}
