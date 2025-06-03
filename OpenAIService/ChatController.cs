using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Configuration;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public ChatController(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    [HttpPost]
    public async Task StreamChat([FromBody] ChatRequest request)
    {
        var openAiApiKey = _config["OpenAI:ApiKey"];

        var payload = new
        {
            model = "gpt-3.5-turbo",
            messages = new[] { new { role = "user", content = request.Message } },
            stream = true
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        httpRequest.Headers.Add("Authorization", $"Bearer {openAiApiKey}");
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();

        Response.ContentType = "text/event-stream";
        Response.Headers.Add("Cache-Control", "no-cache");
        Response.Headers.Add("X-Accel-Buffering", "no");

        using var stream = await response.Content.ReadAsStreamAsync(HttpContext.RequestAborted);
        using var reader = new StreamReader(stream);
        var writer = new StreamWriter(Response.Body, Encoding.UTF8, leaveOpen: true);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

            var json = line["data: ".Length..];
            if (json == "[DONE]") break;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices[0].TryGetProperty("delta", out var delta) &&
                delta.TryGetProperty("content", out var contentProp))
            {
                var content = contentProp.GetString();
                if (!string.IsNullOrEmpty(content))
                {
                    await writer.WriteAsync(content);
                    await writer.FlushAsync();
                    await Response.Body.FlushAsync();
                }
            }
        }
    }
}

public class ChatRequest
{
    public string Message { get; set; }
}
