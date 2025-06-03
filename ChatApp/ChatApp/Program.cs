using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        using var client = new HttpClient();

        while (true)
        {
            Console.Write("You: ");
            string userMessage = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(userMessage))
                break;

            await SendMessageToApiStreamingAsync(client, userMessage);
        }
    }

    static async Task SendMessageToApiStreamingAsync(HttpClient client, string message)
    {
        var payload = new { message = message };
        string json = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        string apiUrl = "https://localhost:7201/api/chatproxy"; // your API URL

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = content
            };

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            Console.Write("Bot: ");
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("data: "))
                {
                    var contentPart = line.Substring("data: ".Length);
                    if (contentPart == "[DONE]") break;

                    // Parse JSON content to extract "content" field
                    using var doc = JsonDocument.Parse(contentPart);
                    var choices = doc.RootElement.GetProperty("choices");
                    var delta = choices[0].GetProperty("delta");

                    if (delta.TryGetProperty("content", out var contentProp))
                    {
                        var contentText = contentProp.GetString();
                        if (!string.IsNullOrEmpty(contentText))
                        {
                            foreach (var ch in contentText)
                            {
                                Console.Write(ch);
                                Console.Out.Flush();
                                await Task.Delay(30); // Adjust typing speed here
                            }
                        }
                    }
                }
                else
                {
                    Console.Write(line);
                }
            }
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
