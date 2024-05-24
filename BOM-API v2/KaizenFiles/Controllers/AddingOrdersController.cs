using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using CRUDFI.Models;
using System.Data.SqlTypes;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using JWTAuthentication.Authentication;
using System.Data;
using System.Globalization;

namespace CRUDFI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AddingOrdersController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<AddingOrdersController> _logger;

        public AddingOrdersController(IConfiguration configuration, ILogger<AddingOrdersController> logger)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> CreateOrder([FromBody] OrderDTO orderDto, [FromQuery] string customerUsername, [FromQuery] string designName, [FromQuery] string pickupTime, [FromQuery] string description, [FromQuery] string flavor, [FromQuery] string size)
        {
            try
            {
                // Get the customer's ID using the provided username
                byte[] customerId = await GetUserIdByCustomerUsername(customerUsername);
                if (customerId == null || customerId.Length == 0)
                {
                    return BadRequest("Customer not found");
                }

                // Get the design's ID using the provided design name
                byte[] designId = await GetDesignIdByDesignName(designName);
                if (designId == null || designId.Length == 0)
                {
                    return BadRequest("Design not found");
                }

                // Generate a new Guid for the Order's Id
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    orderName = orderDto.OrderName,
                    price = orderDto.Price,
                    quantity = orderDto.Quantity,
                    // Assign other properties from orderDto as needed
                    size = size,
                    flavor = flavor,
                    isActive = false
                };

                // Set isActive to false for all orders created

                // Set the status to pending
                order.status = "Pending";

                // Fetch the count of confirmed orders
                int confirmedOrderCount = await GetConfirmedOrderCount();

                // Determine the pickup date based on order type and confirmed orders count
                DateTime pickupDate;
                if (confirmedOrderCount < 5)
                {
                    pickupDate = order.type == "rush" ? DateTime.Today.AddDays(3) : DateTime.Today.AddDays(7);
                }
                else
                {
                    pickupDate = order.type == "rush" ? DateTime.Today.AddDays(4) : DateTime.Today.AddDays(8);
                }

                // Parse the pickup time string to get the hour, minute, and AM/PM values
                DateTime parsedTime = DateTime.ParseExact(pickupTime, "h:mm tt", CultureInfo.InvariantCulture);

                // Combine the pickup date and parsed time into a single DateTime object
                DateTime pickupDateTime = new DateTime(pickupDate.Year, pickupDate.Month, pickupDate.Day, parsedTime.Hour, parsedTime.Minute, 0);

                // Set the combined pickup date and time
                order.PickupDateTime = pickupDateTime;

                order.Description = description;

                // Insert the order into the database
                await InsertOrder(order, customerId, designId, flavor, size);

                return Ok(); // Return 200 OK if the order is successfully created
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while creating the order");
                return StatusCode(500, "An error occurred while processing the request"); // Return 500 Internal Server Error
            }
        }



        private async Task<int> GetConfirmedOrderCount()
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT COUNT(*) FROM orders WHERE Status = 'Confirmed'";

                using (var command = new MySqlCommand(sql, connection))
                {
                    object result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }


        [HttpGet]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> GetAllOrders()
        {
            try
            {
                List<Order> orders = await GetAllOrdersFromDatabase();

                if (orders.Count == 0)
                    return NotFound("No orders available");

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        [HttpGet("bytype/{type}")]
        [Authorize(Roles = UserRoles.Admin)]
        public IActionResult GetOrdersByType(string type)
        {
            try
            {
                List<Order> orders = new List<Order>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    connection.Open();

                    string sql = "SELECT * FROM orders WHERE type = @type";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@type", type);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Guid employeeId = Guid.Empty; // Initialize to empty Guid

                                // Check for DBNull value before casting to Guid
                                if (reader["EmployeeId"] != DBNull.Value)
                                {
                                    employeeId = (Guid)reader["EmployeeId"];
                                }

                                orders.Add(new Order
                                {
                                    Id = reader.GetGuid(reader.GetOrdinal("OrderId")),
                                    orderName = reader.GetString(reader.GetOrdinal("orderName")),
                                    customerId = reader.GetGuid(reader.GetOrdinal("customerId")),
                                    designId = (byte[])reader["DesignId"],
                                    price = reader.GetDecimal(reader.GetOrdinal("price")),
                                    type = reader.GetString(reader.GetOrdinal("type")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    status = reader.GetString(reader.GetOrdinal("Status")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                    isActive = reader["isActive"] != DBNull.Value ? reader.GetBoolean(reader.GetOrdinal("isActive")) : false,
                                    employeeId = employeeId,
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                    size = reader.GetString(reader.GetOrdinal("Size"))
                                });
                            }
                        }
                    }
                }

                if (orders.Count == 0)
                {
                    return NotFound("No orders found for the specified type.");
                }

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while fetching orders by type: {ex.Message}");
            }
        }

        [HttpGet("byemployeeusername")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Artist)]
        public async Task<IActionResult> GetOrdersByUsername([FromQuery] string username)
        {
            try
            {
                // Retrieve the binary UserId from the users table
                byte[] userIdBytes = await GetUserIdByUsername(username);

                if (userIdBytes == null)
                {
                    // If user not found or not of type 2 or 3, return appropriate message
                    return NotFound($"Employee with username '{username}' not found or does not have the required type.");
                }

                // Fetch orders with EmployeeId matching the retrieved UserId
                List<Order> orders = await GetOrdersByEmployeeId(userIdBytes);

                if (orders.Count == 0)
                {
                    return NotFound($"No orders found for the employee with username '{username}'.");
                }

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while fetching orders for username '{username}'");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while fetching orders for username '{username}': {ex.Message}");
            }
        }


        private async Task<byte[]> GetUserIdByUsername(string username)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Username = @username AND Type IN (2, 3)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@username", username);
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        // Return the binary value directly
                        byte[] userIdBytes = (byte[])result;

                        // Debug.WriteLine to display the value of userIdBytes
                        Debug.WriteLine($"UserId bytes for username '{username}': {BitConverter.ToString(userIdBytes)}");

                        return userIdBytes;
                    }
                    else
                    {
                        return null; // Employee not found or not of type 2 or 3
                    }
                }
            }
        }




        private async Task<List<Order>> GetOrdersByEmployeeId(byte[] employeeIdBytes)
        {
            List<Order> orders = new List<Order>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT * FROM orders WHERE EmployeeId = @employeeId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@employeeId", employeeIdBytes);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            orders.Add(new Order
                            {
                                Id = reader.GetGuid(reader.GetOrdinal("OrderId")),
                                customerId = reader.GetGuid(reader.GetOrdinal("CustomerId")),
                                designId = reader.IsDBNull(reader.GetOrdinal("DesignId")) ? null : reader["DesignId"] as byte[],
                                employeeId = reader.IsDBNull(reader.GetOrdinal("EmployeeId")) ? (Guid?)null : reader.GetGuid(reader.GetOrdinal("EmployeeId")),
                                orderName = reader.GetString(reader.GetOrdinal("orderName")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("isActive")),
                                price = reader.GetDecimal(reader.GetOrdinal("price")),
                                type = reader.GetString(reader.GetOrdinal("type")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                status = reader.GetString(reader.GetOrdinal("Status")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                Description = reader.GetString(reader.GetOrdinal("Description")),
                                flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                size = reader.GetString(reader.GetOrdinal("Size"))
                            });
                        }
                    }
                }
            }

            return orders;
        }



        [HttpGet("bycustomerusername/{customerUsername}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> GetOrdersByCustomerUsername(string customerUsername)
        {
            try
            {
                // Get the user ID based on the provided customer username
                byte[] userIdBytes = await GetUserIdByCustomerUsername(customerUsername);

                // If the user ID is empty, return NotFound
                if (userIdBytes == null)
                {
                    return NotFound($"User with username '{customerUsername}' not found.");
                }

                List<Order> orders = new List<Order>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "SELECT * FROM orders WHERE CustomerId = @userId";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@userId", userIdBytes);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                orders.Add(new Order
                                {
                                    Id = reader.GetGuid(reader.GetOrdinal("OrderId")),
                                    customerId = reader.GetGuid(reader.GetOrdinal("CustomerId")),
                                    orderName = reader.GetString(reader.GetOrdinal("orderName")),
                                    price = reader.GetDecimal(reader.GetOrdinal("price")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    status = reader.GetString(reader.GetOrdinal("Status")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                    type = reader.GetString(reader.GetOrdinal("type")),
                                    isActive = reader["isActive"] != DBNull.Value ? reader.GetBoolean(reader.GetOrdinal("isActive")) : false,
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                    size = reader.GetString(reader.GetOrdinal("Size"))
                                });
                            }
                        }
                    }
                }

                if (orders.Count == 0)
                {
                    return NotFound($"No orders found for the user with username '{customerUsername}'.");
                }

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while fetching orders for username '{customerUsername}'");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while fetching orders for username '{customerUsername}': {ex.Message}");
            }
        }


        private async Task<byte[]> GetUserIdByCustomerUsername(string customerUsername)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Username = @username AND Type <= 1";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@username", customerUsername);
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        // Return the binary value directly
                        byte[] userIdBytes = (byte[])result;

                        // Debug.WriteLine to display the value of userIdBytes
                        Debug.WriteLine($"UserId bytes for username '{customerUsername}': {BitConverter.ToString(userIdBytes)}");

                        return userIdBytes;
                    }
                    else
                    {
                        return null; // Customer not found or type not matching
                    }
                }
            }
        }


        [HttpGet("bytype/{type}/{username}")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> GetOrdersByTypeAndUsername(string type, string username)
        {
            try
            {
                // Check if the provided type is valid
                if (!IsValidOrderType(type))
                {
                    return BadRequest($"Invalid order type '{type}'. Allowed types are 'normal', 'rush', and 'cart'.");
                }

                // Get the user ID based on the provided customer username
                byte[] userIdBytes = await GetUserIdByCustomerUsername(username);

                // If the user ID is empty, return NotFound
                if (userIdBytes == null)
                {
                    return NotFound($"User with username '{username}' not found.");
                }

                List<Order> orders = new List<Order>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "SELECT * FROM orders WHERE type = @type AND CustomerId = @userId";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@type", type);
                        command.Parameters.AddWithValue("@userId", userIdBytes);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                orders.Add(new Order
                                {
                                    Id = reader.GetGuid(reader.GetOrdinal("OrderId")),
                                    customerId = reader.GetGuid(reader.GetOrdinal("CustomerId")),
                                    orderName = reader.GetString(reader.GetOrdinal("orderName")),
                                    price = reader.GetDecimal(reader.GetOrdinal("price")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    status = reader.GetString(reader.GetOrdinal("Status")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                    type = reader.GetString(reader.GetOrdinal("type")),
                                    isActive = reader["isActive"] != DBNull.Value ? reader.GetBoolean(reader.GetOrdinal("isActive")) : false,
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                    size = reader.GetString(reader.GetOrdinal("Size"))
                                });
                            }
                        }
                    }
                }

                if (orders.Count == 0)
                {
                    return NotFound($"No orders found for the user with username '{username}' and type '{type}'.");
                }

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while fetching orders for username '{username}' and type '{type}'");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while fetching orders for username '{username}' and type '{type}': {ex.Message}");
            }
        }


        private bool IsValidOrderType(string type)
        {
            // Define valid order types
            List<string> validOrderTypes = new List<string> { "normal", "rush", "cart" };

            // Check if the provided type exists in the valid order types list
            return validOrderTypes.Contains(type.ToLower());
        }



        [HttpPatch("confirmation")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> ConfirmOrCancelOrder([FromQuery] string orderIdHex, [FromQuery] string action)
        {
            byte[] orderId = null;

            try
            {

                // Convert the hexadecimal string to a byte array
                orderId = FromHexString(orderIdHex);

                // Get the current order status
                bool isActive = await GetOrderStatus(orderId);


                // Update the order status based on the action
                if (action.Equals("confirm", StringComparison.OrdinalIgnoreCase))
                {
                    if (!isActive)
                    {
                        // Set isActive to true
                        await UpdateOrderStatus(orderId, true);
                        await UpdateStatus(orderId, "confirmed");
                        // Update the last_updated_at column
                        await UpdateLastUpdatedAt(orderId);
                    }
                    else
                    {
                        return BadRequest($"Order with ID '{BitConverter.ToString(orderId)}' is already confirmed.");
                    }
                }
                else if (action.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                {
                    if (isActive)
                    {
                        // Set isActive to false
                        await UpdateOrderStatus(orderId, false);
                        await UpdateStatus(orderId, "cancelled");
                    }
                    else
                    {
                        return BadRequest($"Order with ID '{BitConverter.ToString(orderId)}' is already canceled.");
                    }
                }
                else
                {
                    return BadRequest("Invalid action. Please choose 'confirm' or 'cancel'.");
                }

                return Ok($"Order with ID '{BitConverter.ToString(orderId)}' has been successfully {action}ed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to {action} order with ID '{BitConverter.ToString(orderId)}'.");
                return StatusCode(500, $"An error occurred while processing the request to {action} order with ID '{BitConverter.ToString(orderId)}'.");
            }
        }

        private byte[] FromHexString(string hexString)
        {
            // Remove the leading "0x" if present
            if (hexString.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hexString = hexString.Substring(2);
            }

            // Convert the hexadecimal string to a byte array
            byte[] bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            return bytes;
        }


        [HttpPatch("orderstatus")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> PatchOrderStatus([FromQuery] string orderId, [FromQuery] string action)
        {
            try
            {
                // Convert the orderId from string to byte array
                byte[] orderIdBytes = FromHexString(orderId);

                // Update the order status based on the action
                if (action.Equals("send", StringComparison.OrdinalIgnoreCase))
                {
                    await UpdateOrderStatus(orderIdBytes, true); // Set isActive to true
                    await UpdateStatus(orderIdBytes, "for pick up");
                    await UpdateLastUpdatedAt(orderIdBytes);
                }
                else if (action.Equals("done", StringComparison.OrdinalIgnoreCase))
                {

                    await UpdateSalesTable(orderIdBytes);

                    // Update the status in the database
                    await UpdateOrderStatus(orderIdBytes, false); // Set isActive to false
                    await UpdateStatus(orderIdBytes, "done");

                    // Update the last_updated_at column
                    await UpdateLastUpdatedAt(orderIdBytes);
                }
                else
                {
                    return BadRequest("Invalid action. Please choose 'send' or 'done'.");
                }

                return Ok($"Order with ID '{orderId}' has been successfully updated to '{action}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to update order status for '{orderId}'.");
                return StatusCode(500, $"An error occurred while processing the request to update order status for '{orderId}'.");
            }
        }


        private async Task UpdateSalesTable(byte[] orderIdBytes)
        {
            // Retrieve order details from the orders table
            var forSalesDetails = await GetOrderDetails(orderIdBytes);

            // If order details found, update the sales table
            if (forSalesDetails != null)
            {
                var existingTotal = await GetExistingTotal(forSalesDetails.name);

                if (existingTotal.HasValue)
                {
                    // If the orderName already exists, update the Total
                    await UpdateTotalInSalesTable(forSalesDetails.name, existingTotal.Value + forSalesDetails.total);
                }
                else
                {
                    // If the orderName doesn't exist, insert a new record
                    await InsertIntoSalesTable(forSalesDetails);
                }
            }
        }

        private async Task<forSales> GetOrderDetails(byte[] orderIdBytes)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT o.orderName, o.price, o.EmployeeId, o.CreatedAt, o.quantity, 
                                        u.Contact, u.Email 
                                        FROM orders o
                                        JOIN users u ON o.EmployeeId = u.UserId
                                        WHERE o.OrderId = @orderId";
                    command.Parameters.AddWithValue("@orderId", orderIdBytes);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            return new forSales
                            {
                                name = reader.GetString("orderName"),
                                cost = reader.GetDouble("price"),
                                contact = reader.GetInt32("Contact"),
                                email = reader.GetString("Email"),
                                date = reader.GetDateTime("CreatedAt"),
                                total = reader.GetInt32("quantity"),

                            };
                        }
                    }
                }
            }
            return null;
        }

        private async Task<int?> GetExistingTotal(string orderName)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Total FROM sales WHERE Name = @orderName";
                    command.Parameters.AddWithValue("@orderName", orderName);

                    var result = await command.ExecuteScalarAsync();
                    return result != null ? Convert.ToInt32(result) : (int?)null;
                }
            }
        }

        private async Task UpdateTotalInSalesTable(string orderName, int total)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE sales SET Total = @total WHERE Name = @orderName";
                    command.Parameters.AddWithValue("@total", total);
                    command.Parameters.AddWithValue("@orderName", orderName);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task InsertIntoSalesTable(forSales forSalesDetails)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "INSERT INTO sales (Name, Cost, Date, Contact, Email, Total) VALUES (@name, @cost, @date, @contact, @email, @total)";
                    command.Parameters.AddWithValue("@name", forSalesDetails.name);
                    command.Parameters.AddWithValue("@cost", forSalesDetails.cost);
                    command.Parameters.AddWithValue("@date", forSalesDetails.date);
                    command.Parameters.AddWithValue("@contact", forSalesDetails.contact);
                    command.Parameters.AddWithValue("@email", forSalesDetails.email);
                    command.Parameters.AddWithValue("@total", forSalesDetails.total);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        private async Task UpdateStatus(byte[] orderId, string status)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "UPDATE orders SET Status = @status WHERE OrderId = @orderId";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@status", status);
                        command.Parameters.AddWithValue("@orderId", orderId);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while updating status for order with ID '{orderId}'.");
                throw;
            }
        }



        [HttpPatch("assignemployee")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AssignEmployeeToOrder([FromQuery] string orderId, [FromQuery] string employeeUsername)
        {
            try
            {
                // Convert the orderId from string to byte array
                byte[] orderIdBytes = FromHexString(orderId);

                // Check if the order with the given ID exists
                bool orderExists = await CheckOrderExists(orderIdBytes);
                if (!orderExists)
                {
                    return NotFound("Order does not exist. Please try another ID.");
                }

                // Check if the employee with the given username exists
                byte[] employeeId = await GetEmployeeIdByUsername(employeeUsername);
                if (employeeId == null || employeeId.Length == 0)
                {
                    return NotFound($"Employee with username '{employeeUsername}' not found. Please try another name.");
                }

                // Update the order with the employee ID
                await UpdateOrderEmployeeId(orderIdBytes, employeeId);

                return Ok($"Employee with username '{employeeUsername}' has been successfully assigned to order with ID '{orderId}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to assign employee to order with ID '{orderId}'.");
                return StatusCode(500, $"An error occurred while processing the request to assign employee to order with ID '{orderId}'.");
            }
        }

        private async Task<bool> CheckOrderExists(byte[] orderIdBytes)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM orders WHERE OrderId = @orderId";
                    command.Parameters.AddWithValue("@orderId", orderIdBytes);

                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result) > 0;
                }
            }
        }

        private async Task<byte[]> GetEmployeeIdByUsername(string username)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Username = @username AND Type = 2";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@username", username);
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        return (byte[])result;
                    }
                    else
                    {
                        return null; // Employee not found
                    }
                }
            }
        }



        private async Task UpdateOrderEmployeeId(byte[] orderId, byte[] employeeId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "UPDATE orders SET EmployeeId = @employeeId WHERE OrderId = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@employeeId", employeeId);
                    command.Parameters.AddWithValue("@orderId", orderId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }



        private async Task<byte[]> GetOrderIdByOrderName(string orderName)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT OrderId FROM orders WHERE orderName = @orderName";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderName", orderName);
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        return (byte[])result;
                    }
                    else
                    {
                        return null; // Order not found
                    }
                }
            }
        }


        private async Task<bool> GetOrderStatus(byte[] orderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT isActive FROM orders WHERE OrderId = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        return (bool)result;
                    }
                    else
                    {
                        return false; // Order not found or isActive is null
                    }
                }
            }
        }


        private async Task UpdateOrderStatus(byte[] orderId, bool isActive)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "UPDATE orders SET isActive = @isActive WHERE OrderId = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@isActive", isActive);
                    command.Parameters.AddWithValue("@orderId", orderId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        private async Task UpdateLastUpdatedAt(byte[] orderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "UPDATE orders SET last_updated_at = NOW() WHERE OrderId = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }



        private async Task<byte[]> GetDesignIdByDesignName(string designName)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string designIdQuery = "SELECT DesignId FROM designs WHERE DisplayName = @DisplayName";
                using (var designIdCommand = new MySqlCommand(designIdQuery, connection))
                {
                    designIdCommand.Parameters.AddWithValue("@DisplayName", designName);
                    object result = await designIdCommand.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        return (byte[])result;
                    }
                    else
                    {
                        return null; // Design not found
                    }
                }
            }
        }

        private async Task InsertOrder(Order order, byte[] customerId, byte[] designId, string flavor, string size)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO orders (OrderId, CustomerId, EmployeeId, CreatedAt, Status, DesignId, orderName, price, quantity, last_updated_by, last_updated_at, type, isActive, PickupDateTime, Description, Flavor, Size) 
                       VALUES (UNHEX(REPLACE(UUID(), '-', '')), @customerId, NULL, NOW(), @status, @designId, @order_name, @price, @quantity, NULL, NULL, @type, @isActive, @pickupDateTime, @Description, @Flavor, @Size)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customerId", customerId);
                    command.Parameters.AddWithValue("@designId", designId);
                    command.Parameters.AddWithValue("@status", order.status);
                    command.Parameters.AddWithValue("@order_name", order.orderName);
                    command.Parameters.AddWithValue("@price", order.price);
                    command.Parameters.AddWithValue("@quantity", order.quantity);
                    command.Parameters.AddWithValue("@type", order.type);
                    command.Parameters.AddWithValue("@isActive", order.isActive);
                    command.Parameters.AddWithValue("@pickupDateTime", order.PickupDateTime);
                    command.Parameters.AddWithValue("@Description", order.Description);
                    command.Parameters.AddWithValue("@Flavor", flavor);
                    command.Parameters.AddWithValue("@Size", size);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        private async Task<List<Order>> GetAllOrdersFromDatabase()
        {
            List<Order> orders = new List<Order>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT * FROM orders";

                using (var command = new MySqlCommand(sql, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Guid employeeId = Guid.Empty; // Default value for employeeId

                            if (!reader.IsDBNull(reader.GetOrdinal("EmployeeId")))
                            {
                                // If the EmployeeId column is not null, get its value
                                employeeId = reader.GetGuid(reader.GetOrdinal("EmployeeId"));
                            }

                            orders.Add(new Order
                            {
                                Id = reader.GetGuid(reader.GetOrdinal("OrderId")),
                                customerId = reader.GetGuid(reader.GetOrdinal("CustomerId")),
                                employeeId = employeeId,
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                status = reader.GetString(reader.GetOrdinal("Status")),
                                designId = reader["DesignId"] as byte[],
                                orderName = reader.GetString(reader.GetOrdinal("orderName")),
                                price = reader.GetDecimal(reader.GetOrdinal("price")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("isActive")),
                                Description = reader.GetString(reader.GetOrdinal("Description")),
                                flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                size = reader.GetString(reader.GetOrdinal("Size"))
                            });
                        }
                    }
                }
            }

            return orders;
        }



    }

}


