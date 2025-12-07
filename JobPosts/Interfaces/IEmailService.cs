public interface IEmailService
{
    Task SendEmailAsync(string recipientEmail, string subject, string plainTextContent, string htmlContent);
    Task SendConfirmationEmailAsync(string recipientEmail, string verificationCode);
}