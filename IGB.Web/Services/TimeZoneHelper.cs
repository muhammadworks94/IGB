namespace IGB.Web.Services;

public static class TimeZoneHelper
{
    // Minimal Windows->IANA mapping for common deployments.
    // If a stored TimeZoneId is already IANA (contains "/"), we use it directly.
    private static readonly IReadOnlyDictionary<string, string> WindowsToIana = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["UTC"] = "UTC",
        ["GMT Standard Time"] = "Europe/London",
        ["W. Europe Standard Time"] = "Europe/Berlin",
        ["Central Europe Standard Time"] = "Europe/Budapest",
        ["Romance Standard Time"] = "Europe/Paris",
        ["Arabian Standard Time"] = "Asia/Dubai",
        ["Egypt Standard Time"] = "Africa/Cairo",
        ["Turkey Standard Time"] = "Europe/Istanbul",
        ["India Standard Time"] = "Asia/Kolkata",
        ["Pakistan Standard Time"] = "Asia/Karachi",
        ["China Standard Time"] = "Asia/Shanghai",
        ["Tokyo Standard Time"] = "Asia/Tokyo",
        ["AUS Eastern Standard Time"] = "Australia/Sydney",
        ["Eastern Standard Time"] = "America/New_York",
        ["Central Standard Time"] = "America/Chicago",
        ["Mountain Standard Time"] = "America/Denver",
        ["Pacific Standard Time"] = "America/Los_Angeles",
    };

    public static TimeZoneInfo? TryGetServerTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch { return null; }
    }

    public static string GetCalendarTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) return "local";
        if (timeZoneId.Contains("/")) return timeZoneId; // assume IANA
        return WindowsToIana.TryGetValue(timeZoneId, out var iana) ? iana : "local";
    }

    public static DateTimeOffset ToUserTime(DateTimeOffset utc, string? userTimeZoneId)
    {
        var tz = TryGetServerTimeZone(userTimeZoneId);
        return tz == null ? utc : TimeZoneInfo.ConvertTime(utc, tz);
    }
}


