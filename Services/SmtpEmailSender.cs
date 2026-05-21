using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace GolfAssociationCommunity.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly IWebHostEnvironment _environment;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var fromAddress = _configuration["Email:FromAddress"];
        var fromName = _configuration["Email:FromName"];
        var host = _configuration["Email:SmtpHost"];
        var username = _configuration["Email:SmtpUsername"];
        var password = _configuration["Email:SmtpPassword"];

        var portText = _configuration["Email:SmtpPort"];
        var enableSslText = _configuration["Email:EnableSsl"];

        if (string.IsNullOrWhiteSpace(fromAddress) || string.IsNullOrWhiteSpace(host))
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning(
                    "Email not sent because SMTP settings are missing. Subject: {Subject}; To: {To}; Body: {Body}",
                    subject,
                    email,
                    htmlMessage);
                return;
            }

            throw new InvalidOperationException("Email settings are missing. Configure Email:FromAddress and Email:SmtpHost.");
        }

        var port = 587;
        if (!string.IsNullOrWhiteSpace(portText) && int.TryParse(portText, out var parsedPort))
        {
            port = parsedPort;
        }

        var enableSsl = true;
        if (!string.IsNullOrWhiteSpace(enableSslText) && bool.TryParse(enableSslText, out var parsedEnableSsl))
        {
            enableSsl = parsedEnableSsl;
        }

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            client.Credentials = new NetworkCredential(username, password ?? string.Empty);
        }

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };

        message.To.Add(email);

        await client.SendMailAsync(message);
        _logger.LogInformation("Email sent to {Email} with subject {Subject}", email, subject);
    }
}
