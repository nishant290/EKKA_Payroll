using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace PayrollNZ.Web.Services;

public class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromName { get; set; } = "PayrollNZ";
    public string FromAddress { get; set; } = "";
}

public interface IEmailService
{
    Task SendAsync(string toAddress, string toName, string subject, string htmlBody,
                   string? attachmentName = null, byte[]? attachment = null);
}

public class EmailService : IEmailService
{
    private readonly SmtpSettings _s;

    public EmailService(IOptions<SmtpSettings> options) => _s = options.Value;

    public async Task SendAsync(string toAddress, string toName, string subject, string htmlBody,
                                string? attachmentName = null, byte[]? attachment = null)
    {
        if (string.IsNullOrWhiteSpace(_s.Host) || string.IsNullOrWhiteSpace(_s.FromAddress))
            throw new InvalidOperationException(
                "Email is not configured. Set the Smtp section in appsettings (or user-secrets).");

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_s.FromName, _s.FromAddress));
        msg.To.Add(new MailboxAddress(toName, toAddress));
        msg.Subject = subject;

        var body = new BodyBuilder { HtmlBody = htmlBody };
        if (attachment != null && attachmentName != null)
            body.Attachments.Add(attachmentName, attachment, ContentType.Parse("application/pdf"));
        msg.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        var socket = _s.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(_s.Host, _s.Port, socket);
        if (!string.IsNullOrWhiteSpace(_s.Username))
            await client.AuthenticateAsync(_s.Username, _s.Password);
        await client.SendAsync(msg);
        await client.DisconnectAsync(true);
    }
}
