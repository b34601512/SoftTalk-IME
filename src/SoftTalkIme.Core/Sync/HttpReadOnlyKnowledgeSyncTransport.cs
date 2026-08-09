using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace SoftTalkIme.Core.Sync;

public sealed class HttpReadOnlyKnowledgeSyncTransport : IReadOnlyKnowledgeSyncTransport
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _clientVersion;

    public HttpReadOnlyKnowledgeSyncTransport(
        HttpClient httpClient,
        string token,
        string clientVersion = "0.1.0")
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _token = string.IsNullOrWhiteSpace(token) ? throw new ArgumentException("同步令牌不能为空。", nameof(token)) : token.Trim();
        _clientVersion = string.IsNullOrWhiteSpace(clientVersion) ? "0.1.0" : clientVersion.Trim();
    }

    public Task<JsonDocument> FetchHeadAsync(
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken = default)
    {
        return PostJsonAsync(
            SyncConstants.HeadPath,
            new { scopes = scopes.ToArray() },
            cancellationToken);
    }

    public Task<JsonDocument> FetchCurrentStateAsync(
        string scope,
        long afterSequence,
        string? pageCursor,
        long? syncSequence,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["scope"] = scope,
            ["after_seq"] = afterSequence,
            ["include_totals"] = false,
            ["limit"] = SyncConstants.DefaultPageLimit,
        };
        if (!string.IsNullOrWhiteSpace(pageCursor))
        {
            payload["page_cursor"] = pageCursor;
        }
        if (syncSequence.HasValue)
        {
            payload["sync_seq"] = syncSequence.Value;
        }

        return PostJsonAsync(SyncConstants.CurrentStatePath, payload, cancellationToken);
    }

    private async Task<JsonDocument> PostJsonAsync(
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.TryAddWithoutValidation("X-SoftTalk-Client-Version", _clientVersion);
        request.Headers.TryAddWithoutValidation("X-SoftTalk-Sync-Protocol-Version", SyncConstants.ProtocolVersion);
        request.Headers.TryAddWithoutValidation("X-SoftTalk-Sync-Schema-Version", SyncConstants.SchemaVersion);

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"只读同步请求失败：HTTP {(int)response.StatusCode}，{responseBody}");
        }

        return JsonDocument.Parse(responseBody);
    }
}
