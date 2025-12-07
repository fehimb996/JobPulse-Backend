using JobPosts.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Text;

namespace JobPosts.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;

    public EmailService(IOptions<EmailSettings> emailOptions)
    {
        _emailSettings = emailOptions.Value;
    }

    public async Task SendEmailAsync(string recipientEmail, string subject, string plainTextContent, string htmlContent)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_emailSettings.SenderName ?? "Peria Pulse Support", _emailSettings.SenderEmail));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            TextBody = plainTextContent,
            HtmlBody = htmlContent
        };

        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);

            await client.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.SenderPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to send email to {recipientEmail}. Error: {ex.Message}", ex);
        }
    }

    public async Task SendConfirmationEmailAsync(string recipientEmail, string confirmationLink)
    {
        var subject = "Confirm your email";
        var plainTextContent = $"Please confirm your account by clicking this link: {confirmationLink}";
        var templatePath = Path.Combine(AppContext.BaseDirectory, "EmailTemplates", "EmailConfirmationTemplate.html");

        string htmlContent;

        try
        {
            htmlContent = await File.ReadAllTextAsync(templatePath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            throw new FileNotFoundException($"Failed to read email confirmation template at path: {templatePath}", ex);
        }

        htmlContent = htmlContent
            .Replace("{{ConfirmationLink}}", confirmationLink)
            .Replace("{{Email}}", recipientEmail)
            .Replace("{{Year}}", DateTime.UtcNow.Year.ToString());

        await SendEmailAsync(recipientEmail, subject, plainTextContent, htmlContent);
    }
}