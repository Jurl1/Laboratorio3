using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Lab3.Web.Services;

public sealed class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    /// <summary>Remitente. Si falta, se usa User.</summary>
    public string? From { get; set; }
}

public interface IEmailSender
{
    /// <summary>False si no hay SMTP configurado (Smtp__Host vacío).</summary>
    bool IsConfigured { get; }
    Task SendAsync(string to, string subject, string textBody, CancellationToken ct = default);
}

/// <summary>SMTP opcional (PLAN.md §4.5). Solo se usa para "Olvidé mi contraseña".</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _opt = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_opt.Host) && !string.IsNullOrWhiteSpace(_opt.From ?? _opt.User);

    public async Task SendAsync(string to, string subject, string textBody, CancellationToken ct = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("SMTP no configurado.");

        using var client = new SmtpClient(_opt.Host, _opt.Port) { EnableSsl = true };
        if (!string.IsNullOrWhiteSpace(_opt.User))
            client.Credentials = new NetworkCredential(_opt.User, _opt.Password);

        using var message = new MailMessage(_opt.From ?? _opt.User!, to, subject, textBody) { IsBodyHtml = false };
        await client.SendMailAsync(message, ct);
    }
}
