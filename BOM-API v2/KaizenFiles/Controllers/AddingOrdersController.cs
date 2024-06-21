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
using System.Security.Claims;

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
        public async Task<IActionResult> CreateOrder([FromBody] OrderDTO orderDto, [FromQuery] string designName, [FromQuery] string pickupTime, [FromQuery] string description, [FromQuery] string flavor, [FromQuery] string size, [FromQuery] string type)
        {
            try
            {
                // Get the design's ID using the provided design name
                byte[] designId = await GetDesignIdByDesignName(designName);
                if (designId == null || designId.Length == 0)
                {
                    return BadRequest("Design not found");
                }

                string designame = await getDesignName(designName);

                // Generate a new Guid for the Order's Id
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    orderName = orderDto.OrderName,
                    price = orderDto.Price,
                    quantity = orderDto.Quantity,
                    designName = designame,
                    type = type,
                    size = size,
                    flavor = flavor,
                    isActive = false,
                    customerName = orderDto.customerName
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
                await InsertOrder(order, designId, flavor, size);

                return Ok(); // Return 200 OK if the order is successfully created
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while creating the order");
                return StatusCode(500, "An error occurred while processing the request"); // Return 500 Internal Server Error
            }
        }

        private async Task<string> GetCustomerNameById(byte[] customerId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT DisplayName FROM users WHERE UserId = @userId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@userId", customerId);

                    return (string)await command.ExecuteScalarAsync();
                }
            }
        }

        [HttpPost("cart")]
        [Authorize(Roles = UserRoles.Customer)]
        public async Task<IActionResult> CreateCartOrder([FromQuery] string orderName, [FromQuery] double price, [FromQuery] int quantity, [FromQuery] string designName, [FromQuery] string description, [FromQuery] string flavor, [FromQuery] string size)
        {
            try
            {
                // Extract the customerUsername from the token
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                // Get the customer's ID using the extracted username
                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
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
                string designame = await getDesignName(designName);
                // Generate a new Guid for the Order's Id
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    orderName = orderName,
                    price = price,
                    designName = designName,
                    quantity = quantity,
                    type = "cart", // Automatically set the type to "cart"
                    size = size,
                    flavor = flavor,
                    isActive = false
                };

                // Set the status to pending
                order.status = "Pending";

                // Set the pickup date and time to null
                order.PickupDateTime = null;

                order.Description = description;

                // Insert the order into the database
                await InsertCart(order, customerId, designId, customerUsername, flavor, size);

                return Ok(); // Return 200 OK if the order is successfully created
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while creating the order");
                return StatusCode(500, "An error occurred while processing the request"); // Return 500 Internal Server Error
            }
        }

        private async Task InsertCart(Order order, byte[] customerId, byte[] designId, string customerName, string flavor, string size)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO orders 
            (OrderId, CustomerId, EmployeeId, CreatedAt, Status, DesignId, orderName, price, quantity, last_updated_by, last_updated_at, type, isActive, PickupDateTime, Description, Flavor, Size, CustomerName. DesignName) 
            VALUES 
            (UNHEX(REPLACE(UUID(), '-', '')), @customerId, NULL, NOW(), @status, @designId, @order_name, @price, @quantity, NULL, NULL, @type, @isActive, @pickupDateTime, @Description, @Flavor, @Size, @customerName, @DesignName)";

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
                    command.Parameters.AddWithValue("@pickupDateTime", DBNull.Value); // Set to null
                    command.Parameters.AddWithValue("@Description", order.Description);
                    command.Parameters.AddWithValue("@Flavor", flavor);
                    command.Parameters.AddWithValue("@Size", size);
                    command.Parameters.AddWithValue("@customerName", customerName); // Add customer name
                    command.Parameters.AddWithValue("@DesignName", order.designName);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }



        private async Task<byte[]> GetUserIdByAllUsername(string username)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Username = @username AND Type IN (1,2, 3, 4)";

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
                        return null; // User not found or type not matching
                    }
                }
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
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetAllOrders()
        {
            try
            {
                List<Order> orders = new List<Order>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "SELECT OrderId, CustomerId, EmployeeId, CreatedAt, Status, HEX(DesignId) as DesignId, orderName, DesignName, price, quantity, last_updated_by, last_updated_at, type, isActive, PickupDateTime, Description, Flavor, Size, CustomerName, EmployeeName FROM orders WHERE type IN ('normal', 'rush')";

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

                                Guid customerId = Guid.Empty;

                                if (!reader.IsDBNull(reader.GetOrdinal("CustomerId")))
                                {
                                    customerId = reader.GetGuid(reader.GetOrdinal("CustomerId"));
                                }

                                // Read OrderId as byte array
                                byte[] orderIdBytes = new byte[16];
                                reader.GetBytes(reader.GetOrdinal("OrderId"), 0, orderIdBytes, 0, 16);

                                // Create a Guid from byte array
                                Guid orderId = new Guid(orderIdBytes);

                                orders.Add(new Order
                                {
                                    Id = orderId,
                                    customerId = customerId,
                                    employeeId = employeeId,
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                    status = reader.GetString(reader.GetOrdinal("Status")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("DesignId"))),
                                    designName = reader.GetString(reader.GetOrdinal("DesignName")),
                                    orderName = reader.GetString(reader.GetOrdinal("orderName")),
                                    price = reader.GetDouble(reader.GetOrdinal("price")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("isActive")),
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                    size = reader.GetString(reader.GetOrdinal("Size")),
                                    PickupDateTime = reader.GetDateTime(reader.GetOrdinal("PickupDateTime")),
                                    customerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                                    employeeName = reader.GetString(reader.GetOrdinal("EmployeeName"))
                                });
                            }
                        }
                    }
                }

                if (orders.Count == 0)
                    return NotFound("No orders available");

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("{orderId}/elements")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetOrderElements(string orderId)
        {
            try
            {
                // Convert the orderId from GUID string to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                List<ElementDTOS> elements = await GetOrderElementsFromDatabase(orderIdBinary);

                if (elements == null || elements.Count == 0)
                {
                    return NotFound($"Order with ID '{orderId}' not found or has no elements.");
                }

                var orderElementsDto = new OrderElementsDTO
                {
                    Elements = elements
                };

                return Ok(orderElementsDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving elements for order with ID '{orderId}'");
                return StatusCode(500, $"An error occurred while processing the request to retrieve elements for order with ID '{orderId}'.");
            }
        }


        private async Task<List<ElementDTOS>> GetOrderElementsFromDatabase(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT i.Id AS ElementId, i.Item_name AS ElementName, i.price AS PricePerUnit, i.quantity " +
                             "FROM orders o " +
                             "JOIN order_elements oe ON o.OrderId = oe.OrderId " +
                             "JOIN Item i ON oe.ElementId = i.Id " +
                             "WHERE o.OrderId = UNHEX(@orderId) AND i.type = 'element'";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        List<ElementDTOS> elements = new List<ElementDTOS>();

                        while (await reader.ReadAsync())
                        {
                            ElementDTOS element = new ElementDTOS
                            {
                                ElementId = Convert.ToInt32(reader["ElementId"]),
                                ElementName = reader["ElementName"].ToString(),
                                PricePerUnit = Convert.ToDecimal(reader["PricePerUnit"]),
                                Quantity = Convert.ToInt32(reader["Quantity"])
                            };

                            elements.Add(element);
                        }

                        return elements;
                    }
                }
            }
        }


        [HttpGet("total-orders")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetTotalQuantities()
        {
            try
            {
                TotalOrders totalQuantities = new TotalOrders();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "SELECT SUM(quantity) AS TotalQuantity FROM orders WHERE isActive = TRUE";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        var result = await command.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            totalQuantities.Total = Convert.ToInt32(result);
                        }
                    }
                }

                return Ok(totalQuantities);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while summing the quantities.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while summing the quantities.");
            }
        }


        [HttpGet("byId/{orderIdHex}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrderByOrderId(string orderIdHex)
        {
            try
            {
                // Convert the hex string to a binary(16) formatted string
                string binary16OrderId = ConvertGuidToBinary16(orderIdHex).ToLower();

                // Fetch the specific order from the database
                Order order = await GetOrderByIdFromDatabase(binary16OrderId);

                if (order == null)
                {
                    return NotFound($"Order with orderId {orderIdHex} not found.");
                }

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving order by orderId {orderIdHex}");
                return StatusCode(500, $"An error occurred while processing the request.");
            }
        }

        private async Task<Order> GetOrderByIdFromDatabase(string orderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT * FROM orders WHERE OrderId = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            Guid employeeId = Guid.Empty; // Default value for employeeId

                            if (!reader.IsDBNull(reader.GetOrdinal("EmployeeId")))
                            {
                                // If the EmployeeId column is not null, get its value
                                employeeId = reader.GetGuid(reader.GetOrdinal("EmployeeId"));
                            }

                            return new Order
                            {
                                Id = reader.GetGuid(reader.GetOrdinal("OrderId")),
                                customerId = reader.GetGuid(reader.GetOrdinal("CustomerId")),
                                employeeId = employeeId,
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                status = reader.GetString(reader.GetOrdinal("Status")),
                                designName = reader.GetString(reader.GetOrdinal("DesignName")),
                                orderName = reader.GetString(reader.GetOrdinal("orderName")),
                                price = reader.GetDouble(reader.GetOrdinal("price")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
                                Description = reader.GetString(reader.GetOrdinal("Description")),
                                flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                size = reader.GetString(reader.GetOrdinal("Size")),
                                PickupDateTime = reader.GetDateTime(reader.GetOrdinal("PickupDateTime")),

                            };
                        }
                        else
                        {
                            return null; // Order not found
                        }
                    }
                }
            }
        }


        [HttpGet("bytype/{type}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
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
                                    price = reader.GetDouble(reader.GetOrdinal("price")),
                                    type = reader.GetString(reader.GetOrdinal("type")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    status = reader.GetString(reader.GetOrdinal("Status")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                    isActive = reader["isActive"] != DBNull.Value ? reader.GetBoolean(reader.GetOrdinal("isActive")) : false,
                                    employeeId = employeeId,
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                    size = reader.GetString(reader.GetOrdinal("Size")),
                                    customerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                                    employeeName = reader.GetString(reader.GetOrdinal("EmployeeName"))

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
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Artist + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrdersByUsername()
        {
            try
            {
                // Extract the EmployeeUsername from the token
                var EmployeeUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(EmployeeUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                // Get the Employee's ID using the extracted username
                byte[] employeeId = await GetUserIdByAllUsername(EmployeeUsername);
                if (employeeId == null || employeeId.Length == 0)
                {
                    return BadRequest("Customer not found");
                }

                // Retrieve the binary UserId from the users table
                byte[] userIdBytes = await GetUserIdByUsername(EmployeeUsername);
                if (userIdBytes == null)
                {
                    return NotFound($"User with username '{EmployeeUsername}' not found.");
                }

                // Fetch orders with EmployeeId matching the retrieved UserId
                List<Order> orders = await GetOrdersByEmployeeId(userIdBytes);

                if (orders.Count == 0)
                {
                    return NotFound($"No orders found for the user with username '{EmployeeUsername}'.");
                }

                return Ok(orders);
            }
            catch (Exception ex)
            {
                var EmployeeUsername = User.FindFirst(ClaimTypes.Name)?.Value;
                _logger.LogError(ex, $"An error occurred while fetching orders for username '{EmployeeUsername}'");
                return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred while fetching orders for username '{EmployeeUsername}': {ex.Message}");
            }
        }

        private async Task<string> GetLastupdater(string username)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT Username FROM users WHERE Username = @username AND Type IN(3,4)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@username", username);
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        // Return the binary value directly
                        string user = (string)result;

                        // Debug.WriteLine to display the value of userIdBytes
                        Debug.WriteLine($"username: '{username}'");

                        return user;
                    }
                    else
                    {
                        return null; // Employee not found or not of type 2 or 3
                    }
                }
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
                                orderName = reader.GetString(reader.GetOrdinal("orderName")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("isActive")),
                                price = reader.GetDouble(reader.GetOrdinal("price")),
                                type = reader.GetString(reader.GetOrdinal("type")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                status = reader.GetString(reader.GetOrdinal("Status")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                Description = reader.GetString(reader.GetOrdinal("Description")),
                                flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                size = reader.GetString(reader.GetOrdinal("Size")),
                                customerName = reader.GetString(reader.GetOrdinal("CustomerName")),

                            });
                        }
                    }
                }
            }

            return orders;
        }



        [HttpGet("bycustomerusername/{customerUsername}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Artist + "," + UserRoles.Manager)]
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
                                    price = reader.GetDouble(reader.GetOrdinal("price")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    status = reader.GetString(reader.GetOrdinal("Status")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                    type = reader.GetString(reader.GetOrdinal("type")),
                                    isActive = reader["isActive"] != DBNull.Value ? reader.GetBoolean(reader.GetOrdinal("isActive")) : false,
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                    size = reader.GetString(reader.GetOrdinal("Size")),
                                    customerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                                    employeeName = reader.GetString(reader.GetOrdinal("EmployeeName"))

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
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Artist + "," + UserRoles.Manager)]
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
                                    price = reader.GetDouble(reader.GetOrdinal("price")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    status = reader.GetString(reader.GetOrdinal("Status")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                    type = reader.GetString(reader.GetOrdinal("type")),
                                    isActive = reader["isActive"] != DBNull.Value ? reader.GetBoolean(reader.GetOrdinal("isActive")) : false,
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                    size = reader.GetString(reader.GetOrdinal("Size")),
                                    customerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                                    employeeName = reader.GetString(reader.GetOrdinal("EmployeeName"))

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

        [HttpGet("inactive")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)] // Adjust authorization as needed
        public async Task<IActionResult> GetInactiveOrders()
        {
            try
            {
                List<Order> orders = await GetInactiveOrdersFromDatabase();

                if (orders == null || orders.Count == 0)
                    return NotFound("No inactive orders found");

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching inactive orders");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }

        private async Task<List<Order>> GetInactiveOrdersFromDatabase()
        {
            List<Order> orders = new List<Order>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT * FROM orders WHERE isActive = @isActive AND type IN ('normal', 'rush')";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@isActive", false);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            orders.Add(new Order
                            {
                                Id = reader.GetGuid(reader.GetOrdinal("OrderId")),
                                customerId = reader.GetGuid(reader.GetOrdinal("CustomerId")),
                                employeeId = reader.IsDBNull(reader.GetOrdinal("EmployeeId")) ? Guid.Empty : reader.GetGuid(reader.GetOrdinal("EmployeeId")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                status = reader.GetString(reader.GetOrdinal("Status")),
                                designId = reader["DesignId"] as byte[],
                                orderName = reader.GetString(reader.GetOrdinal("orderName")),
                                price = reader.GetDouble(reader.GetOrdinal("price")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("isActive")),
                                Description = reader.GetString(reader.GetOrdinal("Description")),
                                flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                size = reader.GetString(reader.GetOrdinal("Size")),
                                PickupDateTime = reader.GetDateTime(reader.GetOrdinal("PickupDateTime")),
                                customerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                                employeeName = reader.GetString(reader.GetOrdinal("EmployeeName"))

                            });
                        }
                    }
                }
            }

            return orders;
        }

        [HttpGet("active")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)] // Adjust authorization as needed
        public async Task<IActionResult> GetActiveOrdersFromDatabase()
        {
            try
            {
                List<Order> orders = await GetOnlyActiveOrdersFromDatabase();

                if (orders == null || orders.Count == 0)
                    return NotFound("No active orders found");

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching inactive orders");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }

        private async Task<List<Order>> GetOnlyActiveOrdersFromDatabase()
        {
            List<Order> orders = new List<Order>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT * FROM orders WHERE isActive = @isActive";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@isActive", true);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            orders.Add(new Order
                            {
                                Id = reader.GetGuid(reader.GetOrdinal("OrderId")),
                                customerId = reader.GetGuid(reader.GetOrdinal("CustomerId")),
                                employeeId = reader.IsDBNull(reader.GetOrdinal("EmployeeId")) ? Guid.Empty : reader.GetGuid(reader.GetOrdinal("EmployeeId")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                status = reader.GetString(reader.GetOrdinal("Status")),
                                designId = reader["DesignId"] as byte[],
                                orderName = reader.GetString(reader.GetOrdinal("orderName")),
                                price = reader.GetDouble(reader.GetOrdinal("price")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("isActive")),
                                Description = reader.GetString(reader.GetOrdinal("Description")),
                                flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                size = reader.GetString(reader.GetOrdinal("Size")),
                                PickupDateTime = reader.GetDateTime(reader.GetOrdinal("PickupDateTime")),
                                customerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                                employeeName = reader.GetString(reader.GetOrdinal("EmployeeName"))

                            });
                        }
                    }
                }
            }

            return orders;
        }

        [HttpGet("cart")]
        [Authorize(Roles = UserRoles.Customer)]
        public async Task<IActionResult> GetCartOrdersForUser()
        {
            try
            {
                // Extract the customerUsername from the token
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                // Retrieve the orders of type 'cart' for the logged-in user
                List<Order> cartOrders = await GetCartOrdersFromDatabase(customerUsername);

                if (cartOrders.Count == 0)
                {
                    return NotFound("No cart orders found for the user.");
                }

                return Ok(cartOrders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cart orders for user");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        private async Task<List<Order>> GetCartOrdersFromDatabase(string customerUsername)
        {
            List<Order> orders = new List<Order>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT * FROM orders WHERE type = 'cart' AND customerId = (SELECT UserId FROM users WHERE Username = @customerUsername)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customerUsername", customerUsername);

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
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                                designId = reader["DesignId"] as byte[],
                                orderName = reader.GetString(reader.GetOrdinal("orderName")),
                                price = reader.GetDouble(reader.GetOrdinal("price")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
                                Description = reader.GetString(reader.GetOrdinal("Description")),
                                flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                size = reader.GetString(reader.GetOrdinal("Size")),
                                customerName = reader.GetString(reader.GetOrdinal("CustomerName")),
                                employeeName = reader.GetString(reader.GetOrdinal("EmployeeName"))

                            });
                        }
                    }
                }
            }

            return orders;
        }


        [HttpPatch("updatePrice")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> UpdateOrderPrice([FromQuery] string orderIdHex, [FromQuery] decimal newPrice)
        {
            try
            {
                // Ensure the user is authorized
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("User is not authorized");
                }

                // Fetch the user ID of the user performing the update
                string lastUpdatedBy = await GetLastupdater(username);
                if (lastUpdatedBy == null)
                {
                    return Unauthorized("Username not found");
                }

                // Convert the hexadecimal orderId to binary(16) format with '0x' prefix for MySQL UNHEX function
                string orderIdBinary = ConvertGuidToBinary16(orderIdHex).ToLower();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    // Check if the order exists with the given OrderId
                    string sqlCheck = "SELECT COUNT(*) FROM orders WHERE OrderId = UNHEX(@orderId)";
                    using (var checkCommand = new MySqlCommand(sqlCheck, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@orderId", orderIdBinary);

                        int orderCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                        if (orderCount == 0)
                        {
                            Debug.Write(orderIdBinary);
                            return NotFound("Order not found");
                        }
                    }

                    // Update the price of the order in the database
                    string sqlUpdate = "UPDATE orders SET price = @newPrice, last_updated_by = @lastUpdatedBy, last_updated_at = @lastUpdatedAt WHERE OrderId = UNHEX(@orderId)";
                    using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                    {
                        updateCommand.Parameters.AddWithValue("@newPrice", newPrice);
                        updateCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                        updateCommand.Parameters.AddWithValue("@lastUpdatedBy", lastUpdatedBy);
                        updateCommand.Parameters.AddWithValue("@lastUpdatedAt", DateTime.UtcNow);

                        int rowsAffected = await updateCommand.ExecuteNonQueryAsync();
                        if (rowsAffected == 0)
                        {
                            return NotFound("Order not found");
                        }
                    }
                }

                return Ok("Order price updated successfully");
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while updating the order price");
                return StatusCode(500, "An error occurred while processing the request");
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



        [HttpPatch("confirmation")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
        public async Task<IActionResult> ConfirmOrCancelOrder([FromQuery] string orderIdHex, [FromQuery] string action)
        {
            try
            {
                // Convert the GUID string to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderIdHex).ToLower();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    // Check if the order exists with the given OrderId
                    string sqlCheck = "SELECT COUNT(*) FROM orders WHERE OrderId = UNHEX(@orderId)";
                    using (var checkCommand = new MySqlCommand(sqlCheck, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@orderId", orderIdBinary);

                        int orderCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                        if (orderCount == 0)
                        {
                            return NotFound("Order not found");
                        }
                    }

                    // Get the current order status
                    bool isActive = await GetOrderStatus(orderIdBinary);

                    // Update the order status based on the action
                    if (action.Equals("confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isActive)
                        {
                            // Set isActive to true
                            await UpdateOrderStatus(orderIdBinary, true);
                            await UpdateStatus(orderIdBinary, "confirmed");
                            // Update the last_updated_at column
                            await UpdateLastUpdatedAt(orderIdBinary);
                        }
                        else
                        {
                            return BadRequest($"Order with ID '{orderIdHex}' is already confirmed.");
                        }
                    }
                    else if (action.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isActive)
                        {
                            // Set isActive to false
                            await UpdateOrderStatus(orderIdBinary, false);
                            await UpdateStatus(orderIdBinary, "cancelled");
                        }
                        else
                        {
                            return BadRequest($"Order with ID '{orderIdHex}' is already canceled.");
                        }
                    }
                    else
                    {
                        return BadRequest("Invalid action. Please choose 'confirm' or 'cancel'.");
                    }

                    return Ok($"Order with ID '{orderIdHex}' has been successfully {action}ed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to {action} order with ID '{orderIdHex}'.");
                return StatusCode(500, $"An error occurred while processing the request to {action} order with ID '{orderIdHex}'.");
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
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Artist)]
        public async Task<IActionResult> PatchOrderStatus([FromQuery] string orderId, [FromQuery] string action)
        {
            try
            {
                // Convert the orderId from GUID string to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Update the order status based on the action
                if (action.Equals("send", StringComparison.OrdinalIgnoreCase))
                {
                    await UpdateOrderStatus(orderIdBinary, true); // Set isActive to true
                    await UpdateStatus(orderIdBinary, "for pick up");
                    await UpdateLastUpdatedAt(orderIdBinary);
                }
                else if (action.Equals("done", StringComparison.OrdinalIgnoreCase))
                {
                    await ProcessOrderCompletion(orderIdBinary);

                    // Update the status in the database
                    await UpdateOrderStatus(orderIdBinary, false); // Set isActive to false
                    await UpdateStatus(orderIdBinary, "done");

                    // Update the last_updated_at column
                    await UpdateLastUpdatedAt(orderIdBinary);
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

        private async Task ProcessOrderCompletion(string orderIdBinary)
        {
            try
            {
                // Retrieve order details and insert into sales table
                var forSalesDetails = await GetOrderDetailsAndInsertIntoSales(orderIdBinary);

                if (forSalesDetails != null)
                {
                    _logger.LogInformation($"Order details inserted into sales table: {forSalesDetails.name}");
                }
                else
                {
                    _logger.LogWarning($"No details found for order with ID '{orderIdBinary}'");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing order completion for '{orderIdBinary}'.");
                throw; // Re-throw the exception to propagate it to the calling method
            }
        }

        private async Task<forSales> GetOrderDetailsAndInsertIntoSales(string orderIdBytes)
        {
            try
            {
                // Retrieve order details from the orders table
                var forSalesDetails = await GetOrderDetails(orderIdBytes);

                // If order details found, insert into the sales table
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

                    return forSalesDetails;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while retrieving order details and inserting into sales table for '{orderIdBytes}'.");
                throw; // Re-throw the exception to propagate it to the calling method
            }
        }

        private async Task<forSales> GetOrderDetails(string orderIdBytes)
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
                                    WHERE o.OrderId = UNHEX(@orderId)";
                    command.Parameters.AddWithValue("@orderId", orderIdBytes);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            var name = reader.GetString("orderName");
                            var cost = reader.GetDouble("price");
                            var contact = reader.GetString("Contact").Trim(); // Adjust for CHAR(10)
                            var email = reader.GetString("Email");
                            var date = reader.GetDateTime("CreatedAt");
                            var total = reader.GetInt32("quantity");

                            // Debugging output using Debug.WriteLine
                            Debug.WriteLine($"Order Details:");
                            Debug.WriteLine($"  Name: {name}");
                            Debug.WriteLine($"  Cost: {cost}");
                            Debug.WriteLine($"  Contact: {contact}");
                            Debug.WriteLine($"  Email: {email}");
                            Debug.WriteLine($"  Date: {date}");
                            Debug.WriteLine($"  Total: {total}");

                            return new forSales
                            {
                                name = name,
                                cost = cost,
                                contact = contact,
                                email = email,
                                date = date,
                                total = total
                            };
                        }
                    }
                }
            }
            return null;
        }


        private async Task InsertIntoSalesTable(forSales forSalesDetails)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    // Adjust column names based on your actual schema
                    command.CommandText = @"INSERT INTO sales (Name, Cost, Date, Contact, Email, Total) 
                                    VALUES (@name, @cost, @date, @contact, @email, @total)";
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



        [HttpPatch("updateorder")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> PatchOrderTypeAndPickupDate([FromQuery] string orderId, [FromQuery] string type, [FromQuery] string pickupTime)
        {
            try
            {
                // Convert the orderId from GUID string to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Validate type
                if (!type.Equals("normal", StringComparison.OrdinalIgnoreCase) &&
                    !type.Equals("rush", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Invalid type. Please choose 'normal' or 'rush'.");
                }

                // Parse the pickup time string to DateTime
                if (!DateTime.TryParseExact(pickupTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                {
                    return BadRequest("Invalid pickup time format.");
                }

                // Fetch the count of confirmed orders
                int confirmedOrderCount = await GetConfirmedOrderCount();

                // Determine the pickup date based on order type and confirmed orders count
                DateTime pickupDate;
                if (confirmedOrderCount < 5)
                {
                    pickupDate = type == "rush" ? DateTime.Today.AddDays(3) : DateTime.Today.AddDays(7);
                }
                else
                {
                    pickupDate = type == "rush" ? DateTime.Today.AddDays(4) : DateTime.Today.AddDays(8);
                }

                // Combine the pickup date and parsed time into a single DateTime object
                DateTime pickupDateTime = new DateTime(pickupDate.Year, pickupDate.Month, pickupDate.Day, parsedTime.Hour, parsedTime.Minute, 0);

                // Update the type and pickup date in the database
                await UpdateOrderTypeAndPickupDate(orderIdBinary, type, pickupDateTime);

                return Ok($"Order with ID '{orderId}' has been successfully updated with type '{type}' and pickup date '{pickupDateTime.ToString("yyyy-MM-dd HH:mm")}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to update order with ID '{orderId}'.");
                return StatusCode(500, $"An error occurred while processing the request to update order with ID '{orderId}'.");
            }
        }


        private async Task UpdateOrderTypeAndPickupDate(string orderIdBinary, string type, DateTime pickupDateTime)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "UPDATE orders SET type = @type, PickupDateTime = @pickupDate WHERE OrderId = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@type", type);
                    command.Parameters.AddWithValue("@pickupDate", pickupDateTime);
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
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

        private async Task UpdateTotalInSalesTable(string orderName, int newTotal)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "UPDATE sales SET total = @newTotal WHERE orderName = @orderName";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@newTotal", newTotal);
                    command.Parameters.AddWithValue("@orderName", orderName);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        private async Task UpdateStatus(string orderIdBinary, string status)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "UPDATE orders SET Status = @status WHERE OrderId = UNHEX(@orderId)";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@status", status);
                        command.Parameters.AddWithValue("@orderId", orderIdBinary);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while updating status for order with ID '{orderIdBinary}'.");
                throw;
            }
        }

        [HttpPatch("updateOrderElements")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> PatchOrderElements([FromQuery] string orderId, [FromBody] OrderElementsUpdateDTO elementsUpdate)
        {
            try
            {
                // Convert the orderId from GUID string to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Fetch the order from the database to ensure it exists
                Order order = await GetOrderByIdFromDatabase(orderIdBinary);
                if (order == null)
                {
                    return NotFound($"Order with orderId {orderId} not found.");
                }

                // Process additions and removals
                await UpdateOrderElements(orderIdBinary, elementsUpdate);

                return Ok($"Order with ID '{orderId}' has been successfully updated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to update elements for order with ID '{orderId}'.");
                return StatusCode(500, $"An error occurred while processing the request to update elements for order with ID '{orderId}'.");
            }
        }

        private async Task UpdateOrderElements(string orderIdBinary, OrderElementsUpdateDTO elementsUpdate)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // Add new elements
                        if (elementsUpdate.ElementsToAdd != null && elementsUpdate.ElementsToAdd.Any())
                        {
                            foreach (var element in elementsUpdate.ElementsToAdd)
                            {
                                // Fetch the item ID from the item name
                                int itemId = await GetItemIdByName(connection, transaction, element.Name);
                                if (itemId == 0)
                                {
                                    throw new Exception($"Item with name '{element.Name}' not found.");
                                }

                                string sqlInsert = "INSERT INTO order_elements (OrderId, Id, quantity) VALUES (UNHEX(@orderId), @itemId, @quantity)";
                                using (var command = new MySqlCommand(sqlInsert, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                                    command.Parameters.AddWithValue("@itemId", itemId);
                                    command.Parameters.AddWithValue("@quantity", element.Quantity);
                                    await command.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        // Remove elements
                        if (elementsUpdate.ElementsToRemove != null && elementsUpdate.ElementsToRemove.Any())
                        {
                            foreach (var elementName in elementsUpdate.ElementsToRemove)
                            {
                                // Fetch the item ID from the item name
                                int itemId = await GetItemIdByName(connection, transaction, elementName);
                                if (itemId == 0)
                                {
                                    throw new Exception($"Item with name '{elementName}' not found.");
                                }

                                string sqlDelete = "DELETE FROM order_elements WHERE OrderId = UNHEX(@orderId) AND Id = @itemId";
                                using (var command = new MySqlCommand(sqlDelete, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                                    command.Parameters.AddWithValue("@itemId", itemId);
                                    await command.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        private async Task<int> GetItemIdByName(MySqlConnection connection, MySqlTransaction transaction, string itemName)
        {
            string sql = "SELECT Id FROM items WHERE Name = @name";
            using (var command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@name", itemName);
                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToInt32(result);
                }
                else
                {
                    return 0; // Item not found
                }
            }
        }


        [HttpPatch("assignemployee")]
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AssignEmployeeToOrder([FromQuery] string orderId, [FromQuery] string employeeUsername)
        {
            try
            {
                // Convert the orderId from GUID string to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Check if the order with the given ID exists
                bool orderExists = await CheckOrderExists(orderIdBinary);
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

                // Update the order with the employee ID and employee name
                await UpdateOrderEmployeeId(orderIdBinary, employeeId, employeeUsername);

                return Ok($"Employee with username '{employeeUsername}' has been successfully assigned to order with ID '{orderId}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to assign employee to order with ID '{orderId}'.");
                return StatusCode(500, $"An error occurred while processing the request to assign employee to order with ID '{orderId}'.");
            }
        }

        private async Task<bool> CheckOrderExists(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM orders WHERE OrderId = UNHEX(@orderId)";
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

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



        private async Task UpdateOrderEmployeeId(string orderIdBinary, byte[] employeeId, string employeeUsername)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "UPDATE orders SET EmployeeId = @employeeId, EmployeeName = @employeeName WHERE OrderId = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@employeeId", employeeId);
                    command.Parameters.AddWithValue("@employeeName", employeeUsername);
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<bool> GetOrderStatus(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT isActive FROM orders WHERE OrderId = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
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


        private async Task UpdateOrderStatus(string orderIdBinary, bool isActive)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "UPDATE orders SET isActive = @isActive WHERE OrderId = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@isActive", isActive);
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        private async Task UpdateLastUpdatedAt(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "UPDATE orders SET last_updated_at = NOW() WHERE OrderId = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<string> getDesignName(string design)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string designQuery = "SELECT DisplayName FROM designs WHERE DisplayName = @displayName";
                using(var designcommand = new MySqlCommand(designQuery, connection))
                {
                    designcommand.Parameters.AddWithValue("@displayName", design);
                    object result = await designcommand.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        return (string)result;
                    }
                    else
                    {
                        return null; // Design not found
                    }
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

        private async Task InsertOrder(Order order, byte[] designId, string flavor, string size)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO orders (OrderId, CustomerId, CustomerName, EmployeeId, CreatedAt, Status, DesignId, orderName, price, quantity, last_updated_by, last_updated_at, type, isActive, PickupDateTime, Description, Flavor, Size, DesignName) 
                       VALUES (UNHEX(REPLACE(UUID(), '-', '')), NULL, @CustomerName, NULL, NOW(), @status, @designId, @order_name, @price, @quantity, NULL, NULL, @type, @isActive, @pickupDateTime, @Description, @Flavor, @Size, @DesignName)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@CustomerName", order.customerName);
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
                    command.Parameters.AddWithValue("@DesignName", order.designName);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }


    }

}


