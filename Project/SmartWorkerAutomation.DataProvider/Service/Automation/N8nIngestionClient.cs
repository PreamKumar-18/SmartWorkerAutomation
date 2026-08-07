using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmartWorkerAutomation.Common.Automation;
using Microsoft.Extensions.Configuration;

namespace SmartWorkerAutomation.DataProvider.Automation;

public class N8nIngestionClient
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;

    public N8nIngestionClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _webhookUrl = configuration["N8n:GenericIngestionWebhookUrl"]
            ?? "https://n8n.devautomation.tech/webhook-test/generic-ingestion";
    }

    public async Task<N8nIngestionResponse> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string userId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _webhookUrl);

        request.Headers.Add("X-User-Id", userId);
        request.Headers.Add("X-File-Name", fileName);

        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        content.Add(streamContent, "file", fileName);
        request.Content = content;

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize<N8nIngestionResponse>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Empty response from ingestion webhook.");
    }
}
