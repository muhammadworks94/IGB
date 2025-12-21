using System.Text.Json.Serialization;

namespace IGB.Web.Zoom;

public sealed class ZoomCreateMeetingRequest
{
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = "Lesson";

    // 2 = scheduled meeting
    [JsonPropertyName("type")]
    public int Type { get; set; } = 2;

    // Zoom accepts ISO 8601
    [JsonPropertyName("start_time")]
    public string StartTime { get; set; } = string.Empty;

    [JsonPropertyName("duration")]
    public int DurationMinutes { get; set; } = 60;

    [JsonPropertyName("timezone")]
    public string? TimeZone { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("settings")]
    public ZoomMeetingSettings Settings { get; set; } = new();
}

public sealed class ZoomMeetingSettings
{
    [JsonPropertyName("join_before_host")]
    public bool JoinBeforeHost { get; set; } = false;

    [JsonPropertyName("waiting_room")]
    public bool WaitingRoom { get; set; } = true;

    [JsonPropertyName("mute_upon_entry")]
    public bool MuteUponEntry { get; set; } = true;
}

public sealed class ZoomCreateMeetingResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("join_url")]
    public string JoinUrl { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

public sealed class ZoomOAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresInSeconds { get; set; }
}


