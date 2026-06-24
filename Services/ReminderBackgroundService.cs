using BookHiveLibrary.Data;
using BookHiveLibrary.Models;
using Microsoft.EntityFrameworkCore;

namespace BookHiveLibrary.Services
{
    public class ReminderBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderBackgroundService> _logger;

        public ReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ReminderBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await ProcessRemindersAsync(); }
                catch (Exception ex) { _logger.LogError(ex, "Reminder service error"); }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        private async Task ProcessRemindersAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var email = scope.ServiceProvider.GetRequiredService<EmailService>();
            var sms = scope.ServiceProvider.GetRequiredService<SmsService>();

            var cutoff = DateTime.Now.AddHours(12);

            // Send 12-hour reminders for books due soon
            var dueSoon = await context.BookReservations
                .Include(r => r.User)
                .Include(r => r.Book)
                .Where(r => r.Status == "PickedUp"
                            && !r.ReminderSent
                            && r.DueDate != null
                            && r.DueDate <= cutoff
                            && r.DueDate > DateTime.Now)
                .ToListAsync();

            foreach (var r in dueSoon)
            {
                if (r.User == null || r.Book == null) continue;

                if (!string.IsNullOrEmpty(r.User.Email))
                    await email.SendReturnReminderAsync(r.User.Email, r.User.FirstName, r.Book.Title, r.DueDate!.Value);

                if (r.User.PhoneVerified && !string.IsNullOrEmpty(r.User.PhoneNumber))
                    await sms.SendReturnReminderAsync(r.User.PhoneNumber, r.Book.Title, r.DueDate!.Value);

                r.ReminderSent = true;
            }

            // Mark past-due borrows as Overdue and notify adviser
            var overdue = await context.BookReservations
                .Include(r => r.User)
                .Include(r => r.Book)
                .Where(r => r.Status == "PickedUp" && r.DueDate < DateTime.Now)
                .ToListAsync();

            foreach (var r in overdue)
            {
                r.Status = "Overdue";

                // Notify adviser if student has one configured
                if (r.User != null && r.Book != null && !string.IsNullOrEmpty(r.User.AdviserEmail))
                {
                    try
                    {
                        string studentName = $"{r.User.FirstName} {r.User.LastName}";
                        await email.SendOverdueAdviserNotificationAsync(
                            r.User.AdviserEmail, studentName, r.Book.Title, r.DueDate!.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send adviser overdue notification for reservation {Id}", r.Id);
                    }
                }
            }

            if (dueSoon.Any() || overdue.Any())
                await context.SaveChangesAsync();
        }
    }
}
