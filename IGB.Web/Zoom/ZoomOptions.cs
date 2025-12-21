namespace IGB.Web.Zoom;

public sealed class ZoomOptions
{
    public bool Enabled { get; set; } = false;

    // Server-to-Server OAuth
    public string AccountId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    // Meeting host user (use "me" or an email/userId)
    public string HostUserId { get; set; } = "me";

    public string? DefaultMeetingPassword { get; set; }

    // Webhooks
    public string? WebhookSecret { get; set; }
    public bool ValidateWebhookSignature { get; set; } = false;
}


