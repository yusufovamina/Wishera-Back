using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace user_service.Services
{
    public class BirthdayNotificationHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BirthdayNotificationHostedService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

        public BirthdayNotificationHostedService(
            IServiceProvider serviceProvider,
            ILogger<BirthdayNotificationHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Birthday Notification Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    // Run at 6 AM UTC every day
                    var now = DateTime.UtcNow;
                    var nextRun = now.Date.AddHours(6);
                    if (nextRun <= now)
                    {
                        nextRun = nextRun.AddDays(1);
                    }

                    var delay = nextRun - now;
                    _logger.LogInformation($"Next birthday check scheduled for {nextRun:yyyy-MM-dd HH:mm:ss} UTC");

                    await Task.Delay(delay, stoppingToken);

                    if (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Starting birthday reminder check");
                        await notificationService.CreateBirthdayRemindersAsync();
                        await notificationService.DeleteExpiredNotificationsAsync();
                        _logger.LogInformation("Birthday reminder check completed");
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    _logger.LogInformation("Birthday notification service is being cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking for birthday reminders");
                    // Wait before retrying, but check for cancellation
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Birthday notification service is being cancelled during retry delay");
                        break;
                    }
                }
            }

            _logger.LogInformation("Birthday Notification Service stopped");
        }
    }
}


