using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using BOM_API_v2.KaizenFiles.Models;
using System.Data.SqlTypes;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using JWTAuthentication.Authentication;
using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Helpers;// Adjust the namespace according to your project structure

namespace BOM_API_v2.KaizenFiles.Controllers
{
    [Route("notification")]
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

        [HttpGet]
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

                string user = ConvertGuidToBinary16(userId).ToLower();

                if (userId == null || userId.Length == 0)
                {
                    return BadRequest("Customer not found.");
                }

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    // Modify the SQL query to filter notifications by user_id
                    string sql = @"
                SELECT notif_id, user_id, message, date_created 
                FROM notification
                WHERE user_id = UNHEX(@userId)"; // Filtering by user_id

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
                                    customId = reader.IsDBNull(reader.GetOrdinal("notif_id"))? (Guid?)null: new Guid((byte[])reader["notif_id"]),
                                    userId = reader.IsDBNull(reader.GetOrdinal("user_id"))? (Guid?)null: new Guid((byte[])reader["user_id"]),
                                    Message = reader.IsDBNull("message") ? string.Empty : reader.GetString("message"),
                                    dateCreated = reader.GetDateTime("date_created")
                                };

                                notifications.Add(notif);
                            }
                        }
                    }
                }

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving notifications: {ex.Message}");
                return StatusCode(500, "Internal server error");
            }
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

                string sql = "SELECT UserId FROM users WHERE Username = @username AND Type IN (1, 2, 3, 4)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@username", username);
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        // Cast the result to byte[] since UserId is stored as binary(16)
                        byte[] userIdBytes = (byte[])result;

                        // Convert the byte[] to a hex string (without dashes)
                        string userIdHex = BitConverter.ToString(userIdBytes).Replace("-", "").ToLower();

                        // Debug.WriteLine to display the value of userIdHex
                        Debug.WriteLine($"UserId hex for username '{username}': {userIdHex}");

                        return userIdHex;
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
