using BillOfMaterialsAPI.Models;
using BOM_API_v2.KaizenFiles.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using BOM_API_v2.Services;
using RestSharp;
using Newtonsoft.Json;
using BillOfMaterialsAPI.Helpers;

namespace BOM_API_v2.KaizenFiles.Controllers
{
    [Route("notifications")]
    [ApiController]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<NotificationController> _logger;
        private readonly IEmailService _emailService;

        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;
        public NotificationController(IConfiguration configuration, ILogger<NotificationController> logger, IEmailService emailService, DatabaseContext context, KaizenTables kaizenTables)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;
            _emailService = emailService;
            _context = context;
            _kaizenTables = kaizenTables;

        }



        [HttpGet("/culo-api/v1/current-user/notifications")]
        [ProducesResponseType(typeof(Notif), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Artist)]
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
                string user;

                if (string.IsNullOrEmpty(userId))
                {
                    user = await GetUserIdByUsername(username);

                }else
                {
                    user = userId;
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

        private async Task<double> GetTotalPriceForIngredientsAsync(string orderId)
        {
            double totalPrice = 0;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL to retrieve price and quantity from suborders and calculate total price
                string sql = @"
        SELECT SUM(price * quantity) AS TotalPrice
        FROM suborders
        WHERE order_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Pass the orderId to the query
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            totalPrice = reader.IsDBNull(0) ? 0 : reader.GetDouble("TotalPrice");
                        }
                    }
                }
            }

            return totalPrice;
        }

        // Private method to calculate total price for the given orderId
        private async Task<double> GetTotalPriceForAddonsAsync(string orderId)
        {
            double totalPrice = 0;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL to retrieve price and quantity from suborders and calculate total price
                string sql = @"
        SELECT SUM(price * quantity) AS TotalPrice
        FROM orderaddons
        WHERE order_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Pass the orderId to the query
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            totalPrice = reader.IsDBNull(0) ? 0 : reader.GetDouble("TotalPrice");
                        }
                    }
                }
            }

            return totalPrice;
        }
        private async Task<RestResponse> GetPaymentLinkAsync(string id)
        {
            // Build the API call to PayMongo, using the passed id as a query parameter
            var options = new RestClientOptions($"https://api.paymongo.com/v1/links?reference_number={id}");
            var client = new RestClient(options);
            var request = new RestRequest();

            // Set the headers
            request.AddHeader("accept", "application/json");
            request.AddHeader("authorization", "Basic c2tfdGVzdF9hdE53NnFHbkRBZnpjWld5Tkp1cmt5Z2M6");

            // Call the API using GET method and return the raw response
            return await client.GetAsync(request);
        }

        private async Task<(string customerId, string customerName)> GetCustomerInfo(string order)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string designQuery = "SELECT customer_name, customer_id FROM orders WHERE order_id = @orderId";
                using (var designcommand = new MySqlCommand(designQuery, connection))
                {
                    designcommand.Parameters.AddWithValue("@orderId", order);
                    using (var reader = await designcommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Retrieve customer_id as byte[]
                            string customerId = reader["customer_id"].ToString();
                            string customerName = reader.GetString("customer_name");

                            return (customerId, customerName);
                        }
                        else
                        {
                            return (null, null); // No matching record found
                        }
                    }
                }
            }
        }

        private async Task NotifyAsync(string notifId, string userId, string message)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            INSERT INTO notification (notif_id, user_id, message, date_created, is_read) 
            VALUES (@notifId, @userId, @message, NOW(), 0)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add parameters for userId and message
                    command.Parameters.AddWithValue("@notifId", notifId);
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@message", message);

                    // Execute the query
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task SendNotificationsForAdminsAsync()
        {
            // Get list of all users with type 2, 3, 4
            List<string> userIds = await GetAdmins();

            // Notification message
            string message = "order is now fully paid";

            // Loop through each user and send a notification
            foreach (string userId in userIds)
            {
                // Create a new GUID for the notification ID
                Guid notId = Guid.NewGuid();
                string notifId = notId.ToString().ToLower();

                await NotifyAsync(notifId, userId, message);
            }
        }


        private async Task<List<string>> GetAdmins()
        {
            List<string> userIds = new List<string>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Type IN (3, 4)";

                using (var command = new MySqlCommand(sql, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        // Cast the result to byte array
                        byte[] userIdBytes = (byte[])reader["UserId"];

                        // Convert byte array to hexadecimal string
                        string userIdHex = BitConverter.ToString(userIdBytes).Replace("-", "").ToLower();

                        // Add the userId to the list
                        userIds.Add(userIdHex);
                    }
                }
            }

            return userIds;
        }
        private async Task InsertTransaction(string newId, string orderIdBinary, string userId, double totalAmount, double totalPaid, string status)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL INSERT query with placeholders for each value
                string sqlInsert = "INSERT INTO transactions (id, order_id, user_id, total_amount, total_paid, date, status) " +
                                   "VALUES(@id, @orderId, @userId, @totalAmount, @totalPaid, NOW(), @status)";

                using (var command = new MySqlCommand(sqlInsert, connection))
                {
                    command.Parameters.AddWithValue("@id", newId);
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@totalAmount", totalAmount);
                    command.Parameters.AddWithValue("@totalPaid", totalPaid);
                    command.Parameters.AddWithValue("@status", status);

                    // Execute the query asynchronously
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<string> ProcessOrderAsync(string orderId, string reference)
        {
            // Log the request details
            _logger.LogInformation($"Received ProcessOrder request for OrderId: {orderId}");

            try
            {
                var response = await GetPaymentLinkAsync(reference);

                // Proceed with further processing once the status is "paid"
                string orderIdBinary = orderId.ToLower();

                // Log the response status code and content
                _logger.LogInformation("API Response Status: {StatusCode}, Content: {Content}", response.StatusCode, response.Content);

                // Check if the response is successful
                if (response.IsSuccessful)
                {
                    var payMongoResponse = JsonConvert.DeserializeObject<GetResponse>(response.Content);
                    var status = payMongoResponse.data[0].attributes.status;
                    Debug.WriteLine("Initial status: " + status);

                    // Loop until the status is "paid" or until the limit of 5 loops
                    int loopCount = 0;
                    while (status != "paid" && loopCount < 6)
                    {
                        // Sleep for 10 seconds
                        await Task.Delay(10000);

                        // Fetch the payment link again to check the status
                        response = await GetPaymentLinkAsync(reference);

                        // Log the response status code and content
                        _logger.LogInformation("API Response Status: {StatusCode}, Content: {Content}", response.StatusCode, response.Content);

                        // Check if the response is successful
                        if (response.IsSuccessful)
                        {
                            payMongoResponse = JsonConvert.DeserializeObject<GetResponse>(response.Content);
                            status = payMongoResponse.data[0].attributes.status;
                            Debug.WriteLine("Updated status: " + status);
                        }
                        else
                        {
                            // Handle case where fetching the payment link fails
                            _logger.LogError("Failed to retrieve payment link: {Content}", response.Content);
                            return "Payment link not found.";
                        }

                        loopCount++;
                    }

                    double ingredientPrice = await GetTotalPriceForIngredientsAsync(orderIdBinary);
                    double addonPrice = await GetTotalPriceForAddonsAsync(orderIdBinary);
                    double totalPrice = ingredientPrice + addonPrice;
                    double indicator = payMongoResponse.data[0].attributes.amount / 100;
                    double price = totalPrice / 2;

                    Debug.WriteLine("Indicator value: " + indicator);
                    Debug.WriteLine("Price value: " + price);

                    string option;

                    // Check the option value and modify the amount accordingly
                    if (indicator == totalPrice)
                    {
                        option = "full";
                    }
                    else if (indicator == price)
                    {
                        option = "half";
                    }
                    else
                    {
                        return "Price/amount is incorrect";
                    }

                    Debug.WriteLine("Option: " + option);

                    using (var connection = new MySqlConnection(connectionstring))
                    {
                        await connection.OpenAsync();

                        // Prepare the SQL update query
                        string sqlUpdate = "UPDATE orders SET payment = 'full' WHERE order_id = @orderId";

                        // Execute the update command
                        using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                        {
                            updateCommand.Parameters.AddWithValue("@orderId", orderIdBinary);

                            int rowsAffected = await updateCommand.ExecuteNonQueryAsync();
                            if (rowsAffected == 0)
                            {
                                return "Order not found";
                            }
                        }
                    }

                    // Call the method to get customer ID and name
                    var (customerId, customerName) = await GetCustomerInfo(orderIdBinary);

                    if (customerId != null && customerId.Length > 0)
                    {
                        string userId = customerId.ToLower();
                        Debug.Write("Customer ID: " + userId);

                        // Construct the message
                        string message = ("your order has been fully paid");

                        Guid notId = Guid.NewGuid();
                        string notifId = notId.ToString().ToLower();

                        // Send the notification
                        await NotifyAsync(notifId, userId, message);

                        await SendNotificationsForAdminsAsync();

                        string transacId = payMongoResponse.data[0].id.ToLower();

                        string transactionStatus = (indicator == totalPrice) ? "paid" : "fully paid other half";

                        await InsertTransaction(transacId, orderIdBinary, userId, totalPrice, indicator, transactionStatus);

                    }
                    else
                    {
                        // Handle case where customer info is not found
                        Debug.Write("Customer not found for the given order.");
                    }

                    return response.Content;
                }
                else
                {
                    // Log an error if the request fails
                    _logger.LogError("Failed to retrieve payment link: {Content}", response.Content);
                    return "Payment link not found.";
                }
            }
            catch (Exception ex)
            {
                // Log the error
                _logger.LogError(ex, "Error processing order");
                return "An error occurred while processing the order.";
            }
        }

        [HttpPost("/culo-api/v1/current-user/{id}/half-paid/simulation")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> SimulateNotification(string id)
        {
            try
            {
                // Check if the transaction is 'half paid'
                var isHalfPaid = await IsTransactionHalfPaidAsync(id);

                var (name, email) = await GetCustomerDetailsByOrderIdAsync(id);
                Debug.WriteLine("name: " + name + "/nemail: " + email);
                double ingredientPrice = await GetTotalPriceForIngredientsAsync(id);
                double addonPrice = await GetTotalPriceForAddonsAsync(id);
                double totalPrice = ingredientPrice + addonPrice;

                double halfprice = totalPrice / 2;
                // Convert the amount to cents (PayMongo expects amounts in cents)
                var amountInCents = (int)(halfprice * 100);
                // Set a static description or customize as needed
                var description = "Payment for order";

                // Make the call to PayMongo API
                var response = await CreatePayMongoPaymentLink(amountInCents, description);

                if (response.IsSuccessful)
                {
                    var payMongoResponse = JsonConvert.DeserializeObject<PaymentRequestResponse>(response.Content);
                    var checkout_url = payMongoResponse.Data.attributes.checkout_url;
                    await _emailService.SendPaymentNoticeToEmail(name, email, checkout_url);
                    payMongoResponse.orderId = id;

                    Task.Run(async () =>
                    {
                        try
                        {
                            // Asynchronously process the order after returning the response
                            await ProcessOrderAsync(id, payMongoResponse.Data.attributes.reference_number);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing the order in background.");
                        }
                    });


                }

                if (!isHalfPaid)
                {
                    return BadRequest("Order is not paid in half.");
                }

                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("User is not authorized.");
                }

                string userId = await GetUserIdByAllUsername(name);

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

        private async Task<RestResponse> CreatePayMongoPaymentLink(int amount, string description)
        {
            var options = new RestClientOptions("https://api.paymongo.com/v1/links");
            var client = new RestClient(options);

            var request = new RestRequest("", Method.Post);
            request.AddHeader("accept", "application/json");
            request.AddHeader("authorization", "Basic c2tfdGVzdF9hdE53NnFHbkRBZnpjWld5Tkp1cmt5Z2M6");

            // Create the JSON body for the request
            var body = new
            {
                data = new
                {
                    attributes = new
                    {
                        amount = amount, // Amount in cents
                        description = description // Payment description
                    }
                }
            };

            request.AddJsonBody(body);

            // Execute the request asynchronously
            return await client.PostAsync(request);
        }

        private async Task<(string customerName, string customerEmail)> GetCustomerDetailsByOrderIdAsync(string orderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL query to retrieve customer_name and customer_id from the orders table where order_id matches
                string sql = "SELECT customer_name, customer_id FROM orders WHERE order_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add the orderId parameter to the query
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Retrieve customer_name and customer_id from the orders table
                            string customerName = reader.GetString(reader.GetOrdinal("customer_name"));
                            string customerId = reader.GetString(reader.GetOrdinal("customer_id"));

                            // Close the reader before proceeding with the next query
                            await reader.CloseAsync();

                            // After retrieving the customer_id, scan the aspnetusers table to get the Email
                            string customerEmail = await GetEmailByCustomerIdAsync(customerId, connection);

                            return (customerName, customerEmail);
                        }
                        else
                        {
                            // No order found with the given orderId, return null values
                            return (null, null);
                        }
                    }
                }
            }
        }

        // Separate method to retrieve Email from aspnetusers table using customer_id
        private async Task<string> GetEmailByCustomerIdAsync(string customerId, MySqlConnection connection)
        {
            // SQL query to retrieve Email from the aspnetusers table where Id = @customerId
            string sql = "SELECT Email FROM aspnetusers WHERE Id = @customerId";

            using (var command = new MySqlCommand(sql, connection))
            {
                // Add the customerId parameter to the query
                command.Parameters.AddWithValue("@customerId", customerId);

                // Execute the command and retrieve the Email
                var result = await command.ExecuteScalarAsync();

                if (result != null && result != DBNull.Value)
                {
                    return result.ToString(); // Return the retrieved Email as a string
                }
                else
                {
                    return null; // No matching customer_id found in aspnetusers
                }
            }
        }

        private async Task<DateTime> GetPickupDateByOrderIdAsync(string orderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL query to retrieve pickup_date from the orders table where order_id matches
                string sql = "SELECT pickup_date FROM orders WHERE order_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add the orderId parameter to the query
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // Ensure that data exists
                        if (await reader.ReadAsync())
                        {
                            // Retrieve the pickup_date as a DateTime value
                            DateTime pickupDate = reader.GetDateTime(reader.GetOrdinal("pickup_date"));
                            return pickupDate;
                        }
                        else
                        {
                            // Handle case where no matching order is found
                            throw new Exception("No order found with the specified orderId.");
                        }
                    }
                }
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

        private async Task<string> GetUserIdByUsername(string username)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Username = @username AND Type IN (2, 3, 4)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@username", username);
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        // Cast the result to byte array
                        byte[] userIdBytes = (byte[])result;

                        // Convert byte array to hexadecimal string
                        string userIdHex = BitConverter.ToString(userIdBytes).Replace("-", "").ToLower();

                        // Debug.WriteLine to display the value of userIdHex
                        Debug.WriteLine($"UserId hex for username '{username}': {userIdHex}");

                        return userIdHex; // Return the hex string
                    }
                    else
                    {
                        return null; // Employee not found or not of type 2 or 3
                    }
                }
            }
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
