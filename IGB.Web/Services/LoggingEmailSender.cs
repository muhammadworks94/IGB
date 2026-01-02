using Serilog;

namespace IGB.Web.Services;

public sealed class LoggingEmailSender : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        Log.Information("EMAIL (stub) To={To} Subject={Subject} Body={Body}", toEmail, subject, htmlBody);
        return Task.CompletedTask;
    }
}


