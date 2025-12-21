using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace IGB.Web.Zoom;

public sealed class ZoomClient : IZoomClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IZoomTokenService _tokenService;
    private readonly IOptionsMonitor<ZoomOptions> _options;
    private readonly ILogger<ZoomClient> _logger;

    public ZoomClient(
        IHttpClientFactory httpClientFactory,
        IZoomTokenService tokenService,
        IOptionsMonitor<ZoomOptions> options,
        ILogger<ZoomClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenService = tokenService;
        _options = options;
        _logger = logger;
    }

    public async Task<ZoomCreateMeetingResponse?> CreateMeetingAsync(ZoomCreateMeetingRequest request, CancellationToken cancellationToken = default)
    {
        var opt = _options.CurrentValue;
        if (!opt.Enabled) return null;

        var token = await _tokenService.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token)) return null;

        var api = _httpClientFactory.CreateClient("ZoomApi");
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        request.Password ??= opt.DefaultMeetingPassword;
        var host = string.IsNullOrWhiteSpace(opt.HostUserId) ? "me" : opt.HostUserId;
        var path = $"users/{Uri.EscapeDataString(host)}/meetings";

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var res = await api.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken);

        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Zoom CreateMeeting failed: {Status} {Body}", res.StatusCode, body);
            return null;
        }

        var responseJson = await res.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<ZoomCreateMeetingResponse>(responseJson, JsonOptions);
    }

    public async Task<bool> DeleteMeetingAsync(string meetingId, CancellationToken cancellationToken = default)
    {
        var opt = _options.CurrentValue;
        if (!opt.Enabled) return false;

        if (string.IsNullOrWhiteSpace(meetingId)) return false;

        var token = await _tokenService.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token)) return false;

        var api = _httpClientFactory.CreateClient("ZoomApi");
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var path = $"meetings/{Uri.EscapeDataString(meetingId)}";
        using var res = await api.DeleteAsync(path, cancellationToken);
        if (res.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK) return true;
        if (res.StatusCode == HttpStatusCode.NotFound) return true; // already gone

        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("Zoom DeleteMeeting failed: {Status} {Body}", res.StatusCode, body);
        return false;
    }
}


