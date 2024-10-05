using System;
using System.Threading;
using System.Threading.Tasks;
using BOM_API_v2.Data; // Update with your actual namespace for data access
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BOM_API_v2.Services
{
    public interface IDataAccess
    {
        Task<DateTime?> GetPickupDateAsync(string orderIdBinary);
    }
    public class NotificationService : IHostedService
    {
        private readonly ILogger<NotificationService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory; // Use IServiceScopeFactory to create scopes

        public NotificationService(ILogger<NotificationService> logger, IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory; // Dependency injection for service scope factory
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Notification Service is starting.");

            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Replace with your logic to determine the orderIdBinary and userId
                    await CheckAndSchedulePickupNotification("exampleOrderId", "exampleUserId"); //did not use,, not enough time to learn this shit
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken); // Wait before the next check
                }
            }, cancellationToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Notification Service is stopping.");
            return Task.CompletedTask;
        }

        public async Task CheckAndSchedulePickupNotification(string orderIdBinary, string userId)
        {
            try
            {
                using (var scope = _serviceScopeFactory.CreateScope()) // Create a new scope for the operation
                {
                    var dataAccess = scope.ServiceProvider.GetRequiredService<IDataAccess>(); // Get scoped IDataAccess
                    await SchedulePickupNotification(dataAccess, orderIdBinary, userId); // Pass it to the method
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling pickup notification for order {OrderId} and user {UserId}", orderIdBinary, userId);
                throw; // Rethrow or handle as necessary
            }
        }

        private async Task SchedulePickupNotification(IDataAccess dataAccess, string orderIdBinary, string userId)
        {
            DateTime? pickupDate = await GetPickupDate(dataAccess, orderIdBinary);

            if (pickupDate.HasValue)
            {
                if (pickupDate.Value.Date == DateTime.UtcNow.Date.AddDays(3))
                {
                    string message = "Your order has a remaining balance to be paid.";
                    Guid notId = Guid.NewGuid();
                    string notifId = notId.ToString().ToLower();
                    await NotifyAsync(notifId, userId, message);
                }
            }
        }

        private async Task<DateTime?> GetPickupDate(IDataAccess dataAccess, string orderIdBinary)
        {
            return await dataAccess.GetPickupDateAsync(orderIdBinary);
        }

        private async Task NotifyAsync(string notificationId, string userId, string message)
        {
            // Logic to send notification
            await Task.CompletedTask; // Replace with actual notification logic
        }
    }
}
