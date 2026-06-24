using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace BookHiveLibrary.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendReturnReminderAsync(string email, string firstName, string bookTitle, DateTime dueDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = "BookHive – Book Return Reminder";
            message.Body = new TextPart("plain")
            {
                Text = $"Hi {firstName},\n\nThis is a reminder that your borrowed book \"{bookTitle}\" is due on {dueDate:MMMM dd, yyyy hh:mm tt}.\n\nPlease return it on time to avoid penalties.\n\nBookHive Library"
            };

            using var client = new SmtpClient();
            await client.ConnectAsync("smtp.office365.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(
                _configuration["EmailSettings:SenderEmail"],
                _configuration["EmailSettings:AppPassword"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendOverdueAdviserNotificationAsync(string adviserEmail, string studentName, string bookTitle, DateTime dueDate)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(MailboxAddress.Parse(adviserEmail));
            message.Subject = "BookHive – Student Overdue Book Notice";
            message.Body = new TextPart("plain")
            {
                Text = $"Dear Adviser,\n\nThis is to inform you that your student {studentName} has not returned the book \"{bookTitle}\" which was due on {dueDate:MMMM dd, yyyy}.\n\nPlease remind your student to return the book to the library as soon as possible.\n\nBookHive Library"
            };

            using var client = new SmtpClient();
            await client.ConnectAsync("smtp.office365.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(
                _configuration["EmailSettings:SenderEmail"],
                _configuration["EmailSettings:AppPassword"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendOtpAsync(string email, string otp)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            message.To.Add(MailboxAddress.Parse(email));

            message.Subject = "BookHive Authentication Code";

            message.Body = new TextPart("plain")
            {
                Text = $"Hello,\n\nYour One-Time Password (OTP) is:\n\n{otp}\n\nThis code will expire in 5 minutes.\n\nIf you didn't request this code, please ignore this email.\n\nBookHive Library"
            };

            using var client = new SmtpClient();
            await client.ConnectAsync("smtp.office365.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(
                _configuration["EmailSettings:SenderEmail"],
                _configuration["EmailSettings:AppPassword"]
            );
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
