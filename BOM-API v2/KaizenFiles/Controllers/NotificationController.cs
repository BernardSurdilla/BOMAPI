using BillOfMaterialsAPI.Models;
using BOM_API_v2.KaizenFiles.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;

namespace BOM_API_v2.KaizenFiles.Controllers
{
    [Route("notifications")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<NotificationController> _logger;

        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        public NotificationController(IConfiguration configuration, ILogger<NotificationController> logger, DatabaseContext context, KaizenTables kaizenTables)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;

            _context = context;
            _kaizenTables = kaizenTables;


        }


        [HttpGet("/culo-api/v1/current-user/notifications")]
        [ProducesResponseType(typeof(Notif), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetNotifications()
        {
            List<Notif> notifications = new List<Notif>();

            try
            {
                // Retrieve the username from the user claims
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("User is not authorized.");
                }

                // Retrieve userId from the username
                string userId = await GetUserIdByAllUsername(username);
                string user = userId.ToLower();

                if (userId == null || userId.Length == 0)
                {
                    return BadRequest("Customer not found.");
                }

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    // Modify the SQL query to filter notifications by user_id
                    string sql = @"
            SELECT notif_id, message, date_created, is_read
            FROM notification
            WHERE user_id = @userId
            ORDER BY date_created DESC";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        // Add the userId parameter to the query
                        command.Parameters.AddWithValue("@userId", user);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var notif = new Notif
                                {
                                    notifId = reader.GetString(reader.GetOrdinal("notif_id")),
                                    message = reader.IsDBNull("message") ? string.Empty : reader.GetString("message"),
                                    dateCreated = reader.GetDateTime("date_created"),
                                    isRead = reader.GetBoolean(reader.GetOrdinal("is_read"))
                                };

                                notifications.Add(notif);
                            }
                        }
                    }
                }

                // Get the unread notification count
                int unreadNotificationCount = await CountUnreadNotificationsAsync(user);

                // Create a Notification object to return
                var response = new Notification
                {
                    unread = unreadNotificationCount,
                    notifs = notifications
                };

                // Return the response with the unread count and notifications
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving notifications: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }



        [HttpPost("/culo-api/v1/current-user/notifications/{notifId}/mark-as-read")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> MarkNotificationAsRead(string notifId)
        {
            try
            {
                string notif = notifId.ToLower();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
                    UPDATE notification
                    SET is_read = 1
                    WHERE notif_id = @notifId";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@notifId", notif);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok("Notification marked as read."+ notif);
                        }
                        else
                        {
                            return NotFound("Notification not found or already marked as read." + notif);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error marking notification as read: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task<bool> IsTransactionHalfPaidAsync(string orderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT 1 FROM transactions WHERE order_id = @orderId AND status = 'half paid' LIMIT 1";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);

                    // Execute the command and check if any record is found
                    object result = await command.ExecuteScalarAsync();

                    // If result is not null, it means the transaction exists with 'half paid' status
                    return result != null;
                }
            }
        }


        [HttpPost("/culo-api/v1/current-user/{id}/half-paid/simulation")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> SimulateNotification([FromBody] NotifReq request, string id)
        {
            try
            {
                // Check if the transaction is 'half paid'
                var isHalfPaid = await IsTransactionHalfPaidAsync(id);

                if (!isHalfPaid)
                {
                    return BadRequest("Order is not paid in half.");
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("User is not authorized.");
                }

                string userId = await GetUserIdByAllUsername(username);

                if (userId == null)
                {
                    return BadRequest("User not found.");
                }

                // Construct the message
                string message = "Pay remaining balance or the order will be considered cancelled.";

                // Ensure userId is in the correct format (GUID or as it is)
                string userBinary = userId.ToLower(); // No conversion since userId is varchar(255) in the database

                // Proceed to insert notification into the database
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    // Insert the notification
                    string sql = @"
                INSERT INTO notification (notif_id, user_id, message, date_created, is_read)
                VALUES (@notifId, @userId, @message, NOW(), false)";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        // Generate a new notification ID
                        string notifId = Guid.NewGuid().ToString();

                        command.Parameters.AddWithValue("@notifId", notifId);
                        command.Parameters.AddWithValue("@userId", userBinary);
                        command.Parameters.AddWithValue("@message", message);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected > 0)
                        {
                            return Ok("Notification sent successfully.");
                        }
                        else
                        {
                            return StatusCode(500, "Failed to send notification.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending notification: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
        }


        private async Task<int> CountUnreadNotificationsAsync(string userId)
        {
            int unreadCount = 0;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT COUNT(*) 
            FROM notification 
            WHERE user_id = @userId AND is_read = 0";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add the userId parameter to the query
                    command.Parameters.AddWithValue("@userId", userId);

                    // Execute the query and get the count of unread notifications
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        unreadCount = Convert.ToInt32(result);
                    }
                }
            }

            // Return the total unread notification count
            return unreadCount;
        }

        private string ConvertGuidToBinary16(string guidString)
        {
            // Parse the input GUID string
            if (!Guid.TryParse(guidString, out Guid guid))
            {
                throw new ArgumentException("Invalid GUID format", nameof(guidString));
            }

            // Convert the GUID to a byte array and then to a formatted binary(16) string
            byte[] guidBytes = guid.ToByteArray();
            string binary16String = BitConverter.ToString(guidBytes).Replace("-", "");

            return binary16String;
        }

        private async Task<string> GetUserIdByAllUsername(string username)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT customer_id FROM suborders WHERE customer_name = @username";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@username", username);
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        // Directly cast the result to string since customer_id is stored as varchar
                        string userId = result.ToString(); // Use ToString() to get the GUID format

                        // Debug.WriteLine to display the value of userId
                        Debug.WriteLine($"UserId for username '{username}': {userId}");

                        return userId; // Return the customer_id in string format
                    }
                    else
                    {
                        return null; // User not found or type not matching
                    }
                }
            }
        }

    }
}
