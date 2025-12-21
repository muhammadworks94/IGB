using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace IGB.Web.Zoom;

public interface IZoomTokenService
{
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class ZoomTokenService : IZoomTokenService
{
    private const string CacheKey = "zoom:access_token:v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<ZoomOptions> _options;
    private readonly ILogger<ZoomTokenService> _logger;

    public ZoomTokenService(
        IDistributedCache cache,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<ZoomOptions> options,
        ILogger<ZoomTokenService> logger)
    {
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var opt = _options.CurrentValue;
        if (!opt.Enabled) return null;
        if (string.IsNullOrWhiteSpace(opt.AccountId) || string.IsNullOrWhiteSpace(opt.ClientId) || string.IsNullOrWhiteSpace(opt.ClientSecret))
        {
            _logger.LogWarning("Zoom is enabled but missing OAuth configuration.");
            return null;
        }

        var cachedJson = await _cache.GetStringAsync(CacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cachedJson))
        {
            var cached = JsonSerializer.Deserialize<CachedToken>(cachedJson, JsonOptions);
            if (cached != null && cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2) && !string.IsNullOrWhiteSpace(cached.AccessToken))
            {
                return cached.AccessToken;
            }
        }

        try
        {
            var tokenClient = _httpClientFactory.CreateClient("ZoomOAuth");
            var url = $"oauth/token?grant_type=account_credentials&account_id={Uri.EscapeDataString(opt.AccountId)}";

            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opt.ClientId}:{opt.ClientSecret}"));
            tokenClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded");

            using var res = await tokenClient.SendAsync(req, cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Zoom token request failed: {Status} {Body}", res.StatusCode, body);
                return null;
            }

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            var token = JsonSerializer.Deserialize<ZoomOAuthTokenResponse>(json, JsonOptions);
            if (token == null || string.IsNullOrWhiteSpace(token.AccessToken)) return null;

            var expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(token.ExpiresInSeconds, 60));
            var cache = new CachedToken(token.AccessToken, expiresAt);
            await _cache.SetStringAsync(
                CacheKey,
                JsonSerializer.Serialize(cache, JsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt },
                cancellationToken);

            return token.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting Zoom access token");
            return null;
        }
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAtUtc);
}


