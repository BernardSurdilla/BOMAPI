﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BOM_API_v2.KaizenFiles.Models;
using BillOfMaterialsAPI.Helpers;// Adjust the namespace according to your project structure
using BillOfMaterialsAPI.Models;
using CRUDFI.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using MySql.Data.MySqlClient;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;
using System;

namespace BOM_API_v2.KaizenFiles.Controllers
{
    [Route("payments")]
    [ApiController]
    public class PaymongoController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<PaymongoController> _logger;

        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;

        public PaymongoController(IConfiguration configuration, ILogger<PaymongoController> logger, DatabaseContext context, KaizenTables kaizenTables)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;

            _context = context;
            _kaizenTables = kaizenTables;


        }

        [HttpPost("payment")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> CreatePaymentLink([FromQuery] string orderId, [FromBody] PaymentRequest paymentRequest)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return BadRequest("Query parameter 'orderId' is required.");
            }
            else
            {

                try
                {
                    // Validate the option and amount
                    if (paymentRequest == null || string.IsNullOrWhiteSpace(paymentRequest.option))
                    {
                        return BadRequest("Payment option is required.");
                    }

                    // Check the option value and modify the amount accordingly
                    double updatedAmount;
                    if (paymentRequest.option.ToLower().Trim() == "full")
                    {
                        updatedAmount = paymentRequest.amount;
                    }
                    else if (paymentRequest.option.ToLower().Trim() == "half")
                    {
                        updatedAmount = paymentRequest.amount / 2;
                    }
                    else
                    {
                        return BadRequest("Invalid option. Choose either 'full' or 'half'.");
                    }

                    // Convert the amount to cents (PayMongo expects amounts in cents)
                    var amountInCents = (int)(updatedAmount * 100);

                    // Set a static description or customize as needed
                    var description = "Payment for order";

                    // Make the call to PayMongo API
                    var response = await CreatePayMongoPaymentLink(amountInCents, description);
                    _logger.LogInformation("PayMongo response: {0}", response.Content);

                    // If the response is successful, deserialize it into our new object model
                    if (response.IsSuccessful)
                    {
                        // Deserialize the response content into the new PaymentRequestResponse class
                        var payMongoResponse = JsonConvert.DeserializeObject<PaymentRequestResponse>(response.Content);

                        string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                        // Check if the order exists
                        bool orderExists = await CheckIfOrderExistsAsync(orderIdBinary);
                        if (!orderExists)
                        {
                            Debug.Write(orderIdBinary);
                            return NotFound("Order not found");
                        }

                        using (var connection = new MySqlConnection(connectionstring))
                        {
                            await connection.OpenAsync();

                            // Prepare the SQL update query
                            string sqlUpdate = "UPDATE orders SET status = 'assigning artist', payment = @option, is_active = 1, last_updated_at = @lastUpdatedAt WHERE order_id = UNHEX(@orderId)";

                            // Execute the update command
                            using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                                updateCommand.Parameters.AddWithValue("@option", paymentRequest.option);
                                updateCommand.Parameters.AddWithValue("@lastUpdatedAt", DateTime.UtcNow);

                                int rowsAffected = await updateCommand.ExecuteNonQueryAsync();
                                if (rowsAffected == 0)
                                {
                                    return NotFound("Order not found");
                                }
                            }
                        }

                        // Retrieve suborder IDs
                        List<byte[]> suborderIds = await GetSuborderId(orderIdBinary);
                        if (suborderIds == null || suborderIds.Count == 0)
                        {
                            return NotFound("No suborder ID found for the given order ID.");
                        }

                        // Update each suborder status
                        foreach (var suborderId in suborderIds)
                        {
                            Debug.WriteLine(BitConverter.ToString(suborderId));
                            await UpdateSuborderStatus(suborderId);
                        }

                        // Call the method to get customer ID and name
                        var (customerId, customerName) = await GetCustomerInfo(orderIdBinary);

                        if (!string.IsNullOrEmpty(customerId))
                        {
                            // Convert the customerId to the binary format needed
                            string userId = ConvertGuidToBinary16(customerId).ToLower();

                            // Construct the message
                            string message = ((customerName ?? "Unknown") + " your order has been approved; assigning artist");

                            // Send the notification
                            await NotifyAsync(userId, message);
                        }
                        else
                        {
                            // Handle case where customer info is not found
                            Debug.Write("Customer not found for the given order.");
                        }

                        // Return the deserialized PayMongo response
                        return Ok(payMongoResponse);
                    }
                    else
                    {
                        _logger.LogError("Failed to create PayMongo link: {Content}, Status Code: {StatusCode}", response.Content, response.StatusCode);
                        return StatusCode((int)response.StatusCode, response.Content);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing the payment link.");
                    return StatusCode(500, "Internal server error.");
                }
            }
        }

        private async Task NotifyAsync(string userId, string message)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            INSERT INTO notification (notif_id, user_id, message, date_created, is_read) 
            VALUES (UNHEX(REPLACE(UUID(), '-', '')), UNHEX(@userId), @message, NOW(), 0)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add parameters for userId and message
                    command.Parameters.AddWithValue("@userId", userId);
                    command.Parameters.AddWithValue("@message", message);

                    // Execute the query
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<(string customerId, string customerName)> GetCustomerInfo(string order)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string designQuery = "SELECT customer_name, customer_id FROM orders WHERE order_id = UNHEX(@orderId)";
                using (var designcommand = new MySqlCommand(designQuery, connection))
                {
                    designcommand.Parameters.AddWithValue("@orderId", order);
                    using (var reader = await designcommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string customerId = reader.GetString("customer_id");
                            string customerName = reader.GetString("customer_name");
                            return (customerId, customerName);  // Return both customer ID and name
                        }
                        else
                        {
                            return (null, null); // Design not found
                        }
                    }
                }
            }
        }

        private async Task<bool> CheckIfOrderExistsAsync(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL query to check if the order exists
                string sqlCheck = "SELECT COUNT(*) FROM orders WHERE order_id = UNHEX(@orderId)";
                using (var checkCommand = new MySqlCommand(sqlCheck, connection))
                {
                    checkCommand.Parameters.AddWithValue("@orderId", orderIdBinary);

                    // Execute the query and return whether the order exists
                    int orderCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                    return orderCount > 0;
                }
            }
        }


        async Task<List<byte[]>> GetSuborderId(string orderIdBinary)
        {
            List<byte[]> suborderIds = new List<byte[]>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Specify the columns you want to select
                string sql = "SELECT suborder_id FROM suborders WHERE order_id = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    // Use ExecuteReaderAsync to execute the SELECT query
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Return the binary value of suborder_id directly
                            byte[] suborderIdBytes = (byte[])reader["suborder_id"];

                            // Debug.WriteLine to display the value of suborderIdBytes
                            Debug.WriteLine($"Suborder ID bytes for order ID '{orderIdBinary}': {BitConverter.ToString(suborderIdBytes)}");

                            // Add each suborder_id to the list
                            suborderIds.Add(suborderIdBytes);
                        }
                    }
                }
            }

            // Return the list of suborder_id byte arrays
            return suborderIds;
        }

        private async Task UpdateSuborderStatus(byte[] orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sqlUpdate = "UPDATE suborders SET status = 'assigning artist' WHERE suborder_id = @orderId";

                using (var command = new MySqlCommand(sqlUpdate, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        // Private method to calculate total price for the given orderId
        /*private async Task<decimal> GetTotalPriceForOrderAsync(string orderId)
        {
            decimal totalPrice = 0;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL to retrieve price and quantity from suborders and calculate total price
                string sql = @"
        SELECT SUM(price * quantity) AS TotalPrice
        FROM suborders
        WHERE order_id = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Pass the orderId to the query
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            totalPrice = reader.IsDBNull(0) ? 0 : reader.GetDecimal("TotalPrice");
                        }
                    }
                }
            }

            return totalPrice;
        }*/

        // Private method to make the RestSharp request to PayMongo
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

    }
}
