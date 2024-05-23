﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using CRUDFI.Models;
using System.Data.SqlTypes;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using JWTAuthentication.Authentication;

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
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> CreateOrder([FromBody] Order order, [FromQuery] string customerUsername, [FromQuery] string designName)
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
                order.Id = Guid.NewGuid();

                // Set isActive based on the type
                bool isActive = order.type.Equals("cart", StringComparison.OrdinalIgnoreCase) ? false : true;

                // Insert the order into the database
                await InsertOrder(order, customerId, designId, isActive);

                return Ok(); // Return 200 OK if the order is successfully created
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while creating the order");
                return StatusCode(500, "An error occurred while processing the request"); // Return 500 Internal Server Error
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
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("last_updated_at"))
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
                                    isActive = reader["isActive"] != DBNull.Value ? reader.GetBoolean(reader.GetOrdinal("isActive")) : false
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
                                    isActive = reader["isActive"] != DBNull.Value ? reader.GetBoolean(reader.GetOrdinal("isActive")) : false
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
        public async Task<IActionResult> ConfirmOrCancelOrder([FromQuery] string orderName, [FromQuery] string action)
        {
            try
            {
                // Check if the order with the given name exists
                byte[] orderId = await GetOrderIdByOrderName(orderName);
                if (orderId == null || orderId.Length == 0)
                {
                    return NotFound("No order found with the specified name.");
                }

                // Get the current order status
                bool isActive = await GetOrderStatus(orderId);

                // Update the order status based on the action
                if (action.Equals("confirm", StringComparison.OrdinalIgnoreCase))
                {
                    if (!isActive)
                    {
                        // Set isActive to true
                        await UpdateOrderStatus(orderId, true);

                        // Update the last_updated_at column
                        await UpdateLastUpdatedAt(orderId);
                    }
                    else
                    {
                        return BadRequest($"Order with name '{orderName}' is already confirmed.");
                    }
                }
                else if (action.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                {
                    if (isActive)
                    {
                        // Set isActive to false
                        await UpdateOrderStatus(orderId, false);
                    }
                    else
                    {
                        return BadRequest($"Order with name '{orderName}' is already canceled.");
                    }
                }
                else
                {
                    return BadRequest("Invalid action. Please choose 'confirm' or 'cancel'.");
                }

                return Ok($"Order with name '{orderName}' has been successfully {action}ed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to {action} order with name '{orderName}'.");
                return StatusCode(500, $"An error occurred while processing the request to {action} order with name '{orderName}'.");
            }
        }




        [HttpPatch("assignemployee")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AssignEmployeeToOrder([FromQuery] string orderName, [FromQuery] string employeeUsername)
        {
            try
            {
                // Check if the order with the given name exists
                byte[] orderId = await GetOrderIdByOrderName(orderName);
                if (orderId == null || orderId.Length == 0)
                {
                    return NotFound("Order does not exist. Please try another name.");
                }

                // Check if the employee with the given username exists
                byte[] employeeId = await GetEmployeeIdByUsername(employeeUsername);
                if (employeeId == null || employeeId.Length == 0)
                {
                    return NotFound($"Employee with username '{employeeUsername}' not found. Please try another name.");
                }

                // Update the order with the employee ID
                await UpdateOrderEmployeeId(orderId, employeeId);

                return Ok($"Employee with username '{employeeUsername}' has been successfully assigned to order '{orderName}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to assign employee to order '{orderName}'.");
                return StatusCode(500, $"An error occurred while processing the request to assign employee to order '{orderName}'.");
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

        private async Task InsertOrder(Order order, byte[] customerId, byte[] designId, bool isActive)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO orders (OrderId, CustomerId, EmployeeId, CreatedAt, Status, DesignId, orderName, price, quantity, last_updated_by, last_updated_at, type, isActive) 
                       VALUES (UNHEX(REPLACE(UUID(), '-', '')), @customerId, NULL, NOW(), 'Pending', @designId, @order_name, @price, @quantity, NULL, NULL, @type, @isActive)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customerId", customerId);
                    command.Parameters.AddWithValue("@designId", designId);
                    command.Parameters.AddWithValue("@order_name", order.orderName);
                    command.Parameters.AddWithValue("@price", order.price);
                    command.Parameters.AddWithValue("@quantity", order.quantity);
                    command.Parameters.AddWithValue("@type", order.type);
                    command.Parameters.AddWithValue("@isActive", isActive);

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
                                isActive = reader.GetBoolean(reader.GetOrdinal("isActive"))
                            });
                        }
                    }
                }
            }

            return orders;
        }



    }

}

