using FraisMission.Configuration;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;

namespace FraisMission.Services
{
    public class EmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var email = new MimeMessage();

            email.From.Add(new MailboxAddress(
                "Gestion Frais Mission",
                _emailSettings.Email
            ));

            email.To.Add(new MailboxAddress(
                "",
                toEmail
            ));

            email.Subject = subject;

            email.Body = new TextPart("html")
            {
                Text = body
            };


            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                _emailSettings.Host,
                _emailSettings.Port,
                MailKit.Security.SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                _emailSettings.Email,
                _emailSettings.Password
            );

            await smtp.SendAsync(email);

            await smtp.DisconnectAsync(true);
        }
    }
}