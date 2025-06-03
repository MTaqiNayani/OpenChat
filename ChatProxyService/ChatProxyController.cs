using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

[ApiController]
[Route("api/[controller]")]
public class ChatProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public ChatProxyController(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    [HttpPost]
    public async Task StreamChat([FromBody] ChatRequest request)
    {
        var finalApiUrl = _config["FinalApi:Endpoint"]; // e.g., https://localhost:7103/api/chat

        var payload = JsonSerializer.Serialize(request);
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, finalApiUrl)
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(HttpContext.RequestAborted);
        using var reader = new StreamReader(stream);

        HttpContext.Response.Headers.Add("Cache-Control", "no-cache");
        HttpContext.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        HttpContext.Response.ContentType = "text/plain";
        await HttpContext.Response.Body.FlushAsync(); // send headers

        char[] buffer = new char[1024];
        int charsRead;
        while ((charsRead = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            var bytes = Encoding.UTF8.GetBytes(buffer, 0, charsRead);
            await HttpContext.Response.Body.WriteAsync(bytes, 0, bytes.Length, HttpContext.RequestAborted);
            await HttpContext.Response.Body.FlushAsync();
        }
    }
}

public class ChatRequest
{
    public string Message { get; set; }
}
