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
using BillOfMaterialsAPI.Schemas;

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

        [HttpPost("manual_ordering")]
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
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
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
            (OrderId, CustomerId, EmployeeId, CreatedAt, Status, DesignId, orderName, price, quantity, last_updated_by, last_updated_at, type, isActive, PickupDateTime, Description, Flavor, Size, CustomerName, DesignName) 
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

        [HttpGet("all_orders_by_customer")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrdersByCustomerIdSummary()
        {
            try
            {
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("No valid customer username found.");
                }

                List<OrderSummary> orders = new List<OrderSummary>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
                SELECT 
                    OrderId, Status, DesignName, orderName, price, quantity, type, 
                    Description, Flavor, Size, PickupDateTime
                FROM orders 
                WHERE CustomerId = (SELECT UserId FROM users WHERE Username = @customerUsername)
                AND type IN ('normal', 'rush')";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerUsername", customerUsername);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                byte[] orderIdBytes = new byte[16];
                                reader.GetBytes(reader.GetOrdinal("OrderId"), 0, orderIdBytes, 0, 16);

                                // Create a Guid from byte array
                                Guid orderId = new Guid(orderIdBytes);

                                orders.Add(new OrderSummary
                                {
                                    Id = orderId,
                                    Status = reader.GetString(reader.GetOrdinal("Status")),
                                    DesignName = reader.GetString(reader.GetOrdinal("DesignName")),
                                    OrderName = reader.GetString(reader.GetOrdinal("orderName")),
                                    Price = reader.GetDouble(reader.GetOrdinal("price")),
                                    Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    Type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    Flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                    Size = reader.GetString(reader.GetOrdinal("Size")),
                                    PickupDateTime = reader.GetDateTime(reader.GetOrdinal("PickupDateTime"))
                                });
                            }
                        }
                    }
                }

                if (orders.Count == 0)
                    return NotFound("No orders available for this customer");

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders by customer ID");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("for_confirmation_orders_by_customer")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrdersByCustomerId()
        {
            try
            {
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("No valid customer username found.");
                }

                List<OrderSummary> orders = new List<OrderSummary>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
                SELECT 
                   OrderId, Status, DesignName, orderName, price, quantity, type, 
                    Description, Flavor, Size, PickupDateTime
                FROM orders 
                WHERE CustomerId = (SELECT UserId FROM users WHERE Username = @customerUsername)
                AND status = 'for confirmation' ";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerUsername", customerUsername);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                byte[] orderIdBytes = new byte[16];
                                reader.GetBytes(reader.GetOrdinal("OrderId"), 0, orderIdBytes, 0, 16);

                                // Create a Guid from byte array
                                Guid orderId = new Guid(orderIdBytes);

                                orders.Add(new OrderSummary
                                {
                                    Id = orderId,
                                    Status = reader.GetString(reader.GetOrdinal("Status")),
                                    DesignName = reader.GetString(reader.GetOrdinal("DesignName")),
                                    OrderName = reader.GetString(reader.GetOrdinal("orderName")),
                                    Price = reader.GetDouble(reader.GetOrdinal("price")),
                                    Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    Type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    Flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                    Size = reader.GetString(reader.GetOrdinal("Size")),
                                    PickupDateTime = reader.GetDateTime(reader.GetOrdinal("PickupDateTime"))
                                });
                            }
                        }
                    }
                }

                if (orders.Count == 0)
                    return NotFound("No orders available for this customer");

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders by customer ID");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        [HttpGet("assign_employees")]
        public async Task<IActionResult> GetEmployeesOfType2()
        {
            try
            {
                List<employee> employees = new List<employee>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"SELECT DisplayName AS Name FROM users WHERE Type = 2";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                employee employee = new employee
                                {
                                    name = reader.GetString("Name")
                                };

                                employees.Add(employee);
                            }
                        }
                    }
                }

                return Ok(employees);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to retrieve employees: {ex.Message}");
            }
        }

        [HttpPost("add_ons_table")]
        public async Task<IActionResult> AddAddOn([FromBody] AddOnDetails addOnDetails)
        {
            try
            {
                // Create AddOns object for database insertion
                var addOns = new AddOns
                {
                    name = addOnDetails.name,
                    pricePerUnit = addOnDetails.pricePerUnit,
                    quantity = addOnDetails.quantity,
                    size = addOnDetails.size,
                    DateAdded = DateTime.UtcNow,  // Current UTC time as DateAdded
                    LastModifiedDate = null,      // Initial value for LastModifiedDate
                    IsActive = true               // Set IsActive to true initially
                };

                // Insert into database
                int newAddOnsId = await InsertAddOnIntoDatabase(addOns);

                // Optionally, you can return the new AddOnsId or a success message
                return Ok($"Add-On '{addOns.name}' added with ID '{newAddOnsId}'");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting Add-On into database.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        private async Task<int> InsertAddOnIntoDatabase(AddOns addOns)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL INSERT statement with measure and ingredient_type
                string sql = @"INSERT INTO AddOns (name, price, quantity, size, measurement, ingredient_type, date_added, last_modified_date, IsActive)
                       VALUES (@Name, @PricePerUnit, @Quantity, @Size, @Measure, @IngredientType, @DateAdded, @LastModifiedDate, @IsActive);
                       SELECT LAST_INSERT_ID();";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Name", addOns.name);
                    command.Parameters.AddWithValue("@PricePerUnit", addOns.pricePerUnit);
                    command.Parameters.AddWithValue("@Quantity", addOns.quantity);
                    command.Parameters.AddWithValue("@Size", addOns.size);
                    command.Parameters.AddWithValue("@Measure", "piece");
                    command.Parameters.AddWithValue("@IngredientType", "element");
                    command.Parameters.AddWithValue("@DateAdded", addOns.DateAdded);
                    command.Parameters.AddWithValue("@LastModifiedDate", addOns.LastModifiedDate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@IsActive", addOns.IsActive);

                    // Execute scalar to get the inserted ID
                    int newAddOnsId = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return newAddOnsId;
                }
            }
        }



        [HttpGet("add_ons_table")]
        public async Task<IActionResult> GetAllAddOns()
        {
            try
            {
                var addOns = await GetAddOnDSOSFromDatabase2();

                if (addOns == null || addOns.Count == 0)
                {
                    return NotFound("No Add-Ons found.");
                }

                return Ok(addOns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all Add-Ons.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        private async Task<List<AddOnDS2>> GetAddOnDSOSFromDatabase2()
        {
            List<AddOnDS2> addOnDSOSList = new List<AddOnDS2>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT  name, price FROM AddOns";

                using (var command = new MySqlCommand(sql, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var addOnDSOS = new AddOnDS2
                            {
                                AddOnName = reader.GetString("name"),
                                PricePerUnit = reader.GetDouble("price")
                            };

                            addOnDSOSList.Add(addOnDSOS);
                        }
                    }
                }
            }

            return addOnDSOSList;
        }


        [HttpGet("design/{designId}")]
        public async Task<IActionResult> GetAddOnsByDesignId(string designId)
        {
            try
            {
                // Convert the designId from Base64 string to binary(16) format
                string designIdHex = ConvertBase64ToBinary16(designId).ToLower();

                Debug.Write(designIdHex);

                List<AddOnDPOS> addOns = await GetDesignAddOnsFromDatabase2(designIdHex);

                if (addOns == null || addOns.Count == 0)
                {
                    return NotFound($"No add-ons found for DesignId '{designId}'.");
                }

                return Ok(addOns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving add-ons for DesignId '{designId}'");
                return StatusCode(500, $"An error occurred while processing the request to retrieve add-ons for DesignId '{designId}'.");
            }
        }

        private string ConvertBase64ToBinary16(string base64String)
        {
            byte[] bytes = Convert.FromBase64String(base64String);
            string hexString = BitConverter.ToString(bytes).Replace("-", "");
            return hexString;
        }


        [HttpGet("{orderId}/add_ons")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetAddOnsByOrderId(string orderId)
        {
            try
            {
                // Convert orderId to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Fetch the Base64 representation of DesignId for the given orderId
                string designIdBase64 = await GetDesignIdByOrderId(orderIdBinary);

                if (string.IsNullOrEmpty(designIdBase64))
                {
                    return NotFound($"No DesignId found for order with ID '{orderId}'.");
                }

                // Convert Base64 string to binary(16) format
                string designIdHex = ConvertBase64ToBinary16(designIdBase64);

                // Fetch DesignAddOns based on DesignId (converted to binary(16))
                var designAddOns = await GetDesignAddOnsFromDatabase2(designIdHex);

                Debug.Write(designAddOns);
                Debug.Write(orderIdBinary);

                if (designAddOns == null || designAddOns.Count == 0)
                {
                    return NotFound($"No add-ons found for order with ID '{orderId}'.");
                }

                return Ok(designAddOns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving add-ons for order with ID '{orderId}'");
                return StatusCode(500, $"An error occurred while retrieving add-ons for order with ID '{orderId}'.");
            }
        }


        private async Task<string> GetDesignIdByOrderId(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT DesignId FROM Orders WHERE OrderId = UNHEX(@orderId)";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    var result = await command.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        byte[] designIdBinary = (byte[])result;
                        return Convert.ToBase64String(designIdBinary);
                    }
                    else
                    {
                        return null; // Order not found or does not have a design associated
                    }
                }
            }
        }

        private async Task<List<AddOnDPOS>> GetDesignAddOnsFromDatabase2(string designIdHex)
        {
            var addOns = new List<AddOnDPOS>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT Quantity, AddOnName, Price " +
                             "FROM DesignAddOns " +
                             "WHERE DesignId = UNHEX(@designId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@designId", designIdHex);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var addOn = new AddOnDPOS
                            {
                                Quantity = reader.GetInt32("Quantity"),
                                AddOnName = reader.GetString(reader.GetOrdinal("AddOnName")),
                                PricePerUnit = reader.GetInt32("Price"),

                            };

                            addOns.Add(addOn);
                        }
                    }
                }
            }

            return addOns;
        }



        [HttpGet("total_orders")]
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


        [HttpGet("final_order_details/{orderIdHex}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrderByOrderId(string orderIdHex)
        {
            try
            {
                // Convert the hex string to a binary(16) formatted string
                string binary16OrderId = ConvertGuidToBinary16(orderIdHex).ToLower();

                // Fetch the specific order and its addons from the database
                FinalOrder finalOrder = await GetFinalOrderByIdFromDatabase(binary16OrderId);

                if (finalOrder == null)
                {
                    return NotFound($"Order with orderId {orderIdHex} not found.");
                }

                // Calculate the total from orderaddons
                double totalFromOrderAddons = await GetTotalFromOrderAddons(binary16OrderId);

                // Calculate allTotal as sum of Price and totalFromOrderAddons
                finalOrder.allTotal = finalOrder.Price + totalFromOrderAddons;

                return Ok(finalOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving order by orderId {orderIdHex}");
                return StatusCode(500, $"An error occurred while processing the request.");
            }
        }

        private async Task<double> GetTotalFromOrderAddons(string orderIdBinary)
        {
            double totalSum = 0.0;

            string getTotalSql = @"
SELECT SUM(Total) AS TotalSum
FROM orderaddons
WHERE OrderId = UNHEX(@orderId)";

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = new MySqlCommand(getTotalSql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    object totalSumObj = await command.ExecuteScalarAsync();
                    if (totalSumObj != DBNull.Value && totalSumObj != null)
                    {
                        totalSum = Convert.ToDouble(totalSumObj);
                    }
                }
            }

            return totalSum;
        }

        private async Task<FinalOrder> GetFinalOrderByIdFromDatabase(string orderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string orderSql = @"
    SELECT orderName, DesignName, price, quantity, Size, Flavor, type, Description, PickupDateTime
    FROM orders
    WHERE OrderId = UNHEX(@orderId)";

                string addOnsSql = @"
    SELECT name, quantity, Price, Total
    FROM orderaddons
    WHERE OrderId = UNHEX(@orderId)";

                FinalOrder finalOrder = null;

                using (var orderCommand = new MySqlCommand(orderSql, connection))
                {
                    orderCommand.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await orderCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            finalOrder = new FinalOrder
                            {
                                OrderName = reader.GetString("orderName"),
                                designName = reader.GetString("DesignName"),
                                Price = reader.GetDouble("price"),
                                Quantity = reader.GetInt32("quantity"),
                                Description = reader.GetString("Description"),
                                size = reader.GetString("Size"),
                                flavor = reader.GetString("Flavor"),
                                type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString("type"),
                                PickupDateTime = reader.GetDateTime("PickupDateTime"),
                                AddOns = new List<AddOnDetails2>()
                            };
                        }
                    }
                }

                if (finalOrder != null)
                {
                    using (var addOnsCommand = new MySqlCommand(addOnsSql, connection))
                    {
                        addOnsCommand.Parameters.AddWithValue("@orderId", orderId);

                        using (var reader = await addOnsCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                finalOrder.AddOns.Add(new AddOnDetails2
                                {
                                    name = reader.GetString("name"),
                                    quantity = reader.GetInt32("quantity"),
                                    pricePerUnit = reader.GetDouble("Price"),
                                    total = reader.GetDouble("Total") // Assuming Total is of type double as per previous context
                                });
                            }
                        }
                    }
                }

                return finalOrder;
            }
        }


        [HttpGet("by_type/{type}")]
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

        [HttpGet("by_employee_username")]
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



        [HttpGet("by_customer_username/{customerUsername}")]
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


        [HttpGet("by_type/{type}/{username}")]
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
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
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
                                employeeName = reader.GetString(reader.GetOrdinal("EmployeeName")),
                                designName = reader.GetString(reader.GetOrdinal("DesignName"))

                            });
                        }
                    }
                }
            }

            return orders;
        }


        [HttpPatch("update_price")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> UpdateOrderAddon([FromQuery] string orderIdHex, [FromQuery] string name, [FromQuery] decimal price)
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

                // Add "custom " prefix to the name
                string customName = "custom " + name;

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

                    // Calculate the total price based on quantity and price
                    decimal total = price * 1; // Assuming quantity is always 1 for simplicity

                    // Insert the new addon into the orderaddons table with calculated total
                    string sqlInsert = "INSERT INTO orderaddons (OrderId, name, price, quantity, Total) VALUES (UNHEX(@orderId), @name, @price, @quantity, @total)";
                    using (var insertCommand = new MySqlCommand(sqlInsert, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                        insertCommand.Parameters.AddWithValue("@name", customName); // Use customName with "custom " prefix
                        insertCommand.Parameters.AddWithValue("@price", price);
                        insertCommand.Parameters.AddWithValue("@quantity", 1); // Hardcoded quantity as 1 for simplicity
                        insertCommand.Parameters.AddWithValue("@total", total);

                        await insertCommand.ExecuteNonQueryAsync();
                    }

                    // Update the status of the order in the database
                    string sqlUpdate = "UPDATE orders SET Status = 'confirmation', last_updated_by = @lastUpdatedBy, last_updated_at = @lastUpdatedAt WHERE OrderId = UNHEX(@orderId)";
                    using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                    {
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

                return Ok("Order addon added and order status updated to confirmation successfully");
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while adding the order addon");
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
                            // Retrieve all add-ons for this order from orderaddons table
                            string sqlGetOrderAddOns = @"SELECT name, quantity FROM orderaddons WHERE OrderId = UNHEX(@orderId)";
                            List<(string AddOnName, int Quantity)> orderAddOnsList = new List<(string, int)>();

                            using (var getOrderAddOnsCommand = new MySqlCommand(sqlGetOrderAddOns, connection))
                            {
                                getOrderAddOnsCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                                using (var reader = await getOrderAddOnsCommand.ExecuteReaderAsync())
                                {
                                    while (await reader.ReadAsync())
                                    {
                                        string addOnName = reader.GetString(0);
                                        int quantity = reader.GetInt32(1);
                                        orderAddOnsList.Add((addOnName, quantity));
                                    }
                                }
                            }

                            // Update AddOns quantities for each entry in orderaddons
                            foreach (var (AddOnName, Quantity) in orderAddOnsList)
                            {
                                string sqlUpdateAddOns = "UPDATE AddOns SET quantity = quantity - @Quantity WHERE name = @AddOnName";
                                using (var updateAddOnsCommand = new MySqlCommand(sqlUpdateAddOns, connection))
                                {
                                    updateAddOnsCommand.Parameters.AddWithValue("@Quantity", Quantity);
                                    updateAddOnsCommand.Parameters.AddWithValue("@AddOnName", AddOnName);
                                    await updateAddOnsCommand.ExecuteNonQueryAsync();
                                }
                            }

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


        [HttpPatch("order_status_artist")]
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



        [HttpPatch("update_cart_customer")]
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

        [HttpPatch("{orderId}/manage_add_ons")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> ManageAddOnsByOrderId(string orderId, [FromBody] ManageAddOnsRequest request)
        {
            try
            {
                _logger.LogInformation($"Starting ManageAddOnsByOrderId for orderId: {orderId}");

                // Convert orderId to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Fetch the Base64 representation of DesignId for the given orderId
                string designIdBase64 = await GetDesignIdByOrderId(orderIdBinary);

                if (string.IsNullOrEmpty(designIdBase64))
                {
                    return NotFound($"No DesignId found for order with ID '{orderId}'.");
                }

                // Convert Base64 string to binary(16) format
                string designIdHex = ConvertBase64ToBinary16(designIdBase64).ToLower();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // Retrieve existing order add-ons
                            List<OrderAddOn> existingOrderAddOns = await GetOrderAddOns(connection, transaction, orderIdBinary);

                            // Fetch all design addons for the designId
                            List<DesignAddOn> designAddOns = await GetAllDesignAddOns(connection, transaction, designIdHex);

                            // Dictionary to track which design addons are already added to orderaddons
                            var addedAddOns = new HashSet<string>(existingOrderAddOns.Select(a => a.AddOnName));

                            // Process each action in the request
                            foreach (var action in request.Actions)
                            {
                                if (action.ActionType.ToLower() == "setquantity")
                                {
                                    // Check if the add-on exists in designaddons
                                    var designAddOn = designAddOns.FirstOrDefault(da => da.AddOnName == action.AddOnName);
                                    if (designAddOn != null)
                                    {
                                        // Insert or update quantity for the specified add-on in orderaddons
                                        await SetOrUpdateAddOn(connection, transaction, orderIdBinary, designAddOn.DesignAddOnId, action.AddOnName, action.Quantity);

                                        // Mark as added
                                        addedAddOns.Add(action.AddOnName);
                                    }
                                    else
                                    {
                                        return BadRequest($"Add-on '{action.AddOnName}' not found in designaddons for order with ID '{orderId}'.");
                                    }
                                }
                                else if (action.ActionType.ToLower() == "remove")
                                {
                                    // Set quantity to 0 for the specified add-on
                                    await SetOrUpdateAddOn(connection, transaction, orderIdBinary, 0, action.AddOnName, 0);

                                    // Mark as added (or removed, handled in SetOrUpdateAddOn)
                                    addedAddOns.Add(action.AddOnName);
                                }
                            }

                            // Insert remaining design addons that are not already added
                            foreach (var designAddOn in designAddOns)
                            {
                                if (!addedAddOns.Contains(designAddOn.AddOnName))
                                {
                                    await SetOrUpdateAddOn(connection, transaction, orderIdBinary, designAddOn.DesignAddOnId, designAddOn.AddOnName, designAddOn.Quantity);
                                }
                            }

                            // Calculate the total from orderaddons
                            double totalFromOrderAddons = await GetTotalFromOrderAddons(connection, transaction, orderIdBinary);

                            // Update the price in orders table
                            await UpdateOrderPrice(connection, transaction, orderIdBinary, totalFromOrderAddons);

                            await transaction.CommitAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Transaction failed, rolling back");
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }

                return Ok("Add-ons quantities successfully managed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error managing add-ons for order with ID '{orderId}'");
                return StatusCode(500, $"An error occurred while managing add-ons for order with ID '{orderId}'.");
            }
        }


        private async Task<double> GetTotalFromOrderAddons(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary)
        {
            string getTotalSql = @"SELECT SUM(Total) AS TotalSum
                           FROM orderaddons
                           WHERE OrderId = UNHEX(@orderId)";

            using (var command = new MySqlCommand(getTotalSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@orderId", orderIdBinary);

                object totalSumObj = await command.ExecuteScalarAsync();
                double totalSum = totalSumObj == DBNull.Value ? 0.0 : Convert.ToDouble(totalSumObj);

                return totalSum;
            }
        }

        private async Task UpdateOrderPrice(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary, double totalFromOrderAddons)
        {
            string updatePriceSql = @"UPDATE orders
                              SET price = price + @totalFromOrderAddons
                              WHERE OrderId = UNHEX(@orderId)";

            using (var command = new MySqlCommand(updatePriceSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@totalFromOrderAddons", totalFromOrderAddons);
                command.Parameters.AddWithValue("@orderId", orderIdBinary);

                await command.ExecuteNonQueryAsync();

                _logger.LogInformation($"Updated price in orders table for order with ID '{orderIdBinary}'");
            }
        }

        private async Task<List<DesignAddOn>> GetAllDesignAddOns(MySqlConnection connection, MySqlTransaction transaction, string designIdHex)
        {
            List<DesignAddOn> designAddOns = new List<DesignAddOn>();

            string sql = @"SELECT DesignAddOnId, AddOnName, Quantity, Price
                   FROM designaddons 
                   WHERE DesignId = UNHEX(@designId)";

            using (var command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@designId", designIdHex);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        DesignAddOn designAddOn = new DesignAddOn
                        {
                            DesignAddOnId = reader.GetInt32("DesignAddOnId"),
                            AddOnName = reader.GetString("AddOnName"),
                            Quantity = reader.GetInt32("Quantity"),
                            Price = reader.GetDouble("Price")
                        };

                        designAddOns.Add(designAddOn);
                    }
                }
            }

            return designAddOns;
        }


        private async Task<List<OrderAddOn>> GetOrderAddOns(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary)
        {
            List<OrderAddOn> orderAddOns = new List<OrderAddOn>();

            string sql = @"SELECT addOnsId, name, quantity, Price 
                   FROM orderaddons 
                   WHERE OrderId = UNHEX(@orderId)";

            using (var command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@orderId", orderIdBinary);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        OrderAddOn addOn = new OrderAddOn
                        {
                            // Handle nullable addOnsId
                            AddOnId = reader.IsDBNull("addOnsId") ? (int?)null : reader.GetInt32("addOnsId"),
                            AddOnName = reader.GetString("name"),
                            Quantity = reader.GetInt32("quantity"),
                            Price = reader.GetDouble("Price")
                        };

                        orderAddOns.Add(addOn);
                    }
                }
            }

            return orderAddOns;
        }


        private async Task SetOrUpdateAddOn(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary, int? designAddOnId, string addOnName, int quantity)
        {
            // Check if the quantity is 0 to handle removal
            if (quantity == 0)
            {
                // Delete the add-on from orderaddons if it exists
                string deleteSql = @"DELETE FROM orderaddons 
                             WHERE OrderId = UNHEX(@orderId) AND name = @addOnName";

                using (var deleteCommand = new MySqlCommand(deleteSql, connection, transaction))
                {
                    deleteCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                    deleteCommand.Parameters.AddWithValue("@addOnName", addOnName);

                    _logger.LogInformation($"Deleting add-on '{addOnName}' from orderaddons");

                    await deleteCommand.ExecuteNonQueryAsync();
                }

                // Exit the method since no insertion or update is needed
                return;
            }

            // Fetch the price from designaddons based on designAddOnId and addOnName
            string getPriceSql = @"SELECT Price 
                           FROM designaddons 
                           WHERE DesignAddOnId = @designAddOnId AND AddOnName = @addOnName";

            double price = 0.0; // Initialize price

            using (var getPriceCommand = new MySqlCommand(getPriceSql, connection, transaction))
            {
                getPriceCommand.Parameters.AddWithValue("@designAddOnId", designAddOnId);
                getPriceCommand.Parameters.AddWithValue("@addOnName", addOnName);

                object priceResult = await getPriceCommand.ExecuteScalarAsync();
                if (priceResult != null && priceResult != DBNull.Value)
                {
                    price = Convert.ToDouble(priceResult);
                }
                else
                {
                    throw new Exception($"Price not found for add-on '{addOnName}' with DesignAddOnId '{designAddOnId}'.");
                }
            }

            // Calculate total price
            double total = quantity * price;

            // Check if the add-on already exists in orderaddons
            string selectSql = @"SELECT COUNT(*) 
                         FROM orderaddons 
                         WHERE OrderId = UNHEX(@orderId) AND name = @addOnName";

            using (var selectCommand = new MySqlCommand(selectSql, connection, transaction))
            {
                selectCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                selectCommand.Parameters.AddWithValue("@addOnName", addOnName);

                int count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                if (count == 0)
                {
                    // Insert new add-on into orderaddons
                    string insertSql = @"INSERT INTO orderaddons (OrderId, addOnsId, name, quantity, Price, Total)
                                 VALUES (UNHEX(@orderId), @designAddOnId, @addOnName, @quantity, @price, @total)";

                    using (var insertCommand = new MySqlCommand(insertSql, connection, transaction))
                    {
                        insertCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                        insertCommand.Parameters.AddWithValue("@designAddOnId", designAddOnId.HasValue ? (object)designAddOnId : DBNull.Value);
                        insertCommand.Parameters.AddWithValue("@addOnName", addOnName);
                        insertCommand.Parameters.AddWithValue("@quantity", quantity);
                        insertCommand.Parameters.AddWithValue("@price", price);
                        insertCommand.Parameters.AddWithValue("@total", total);

                        _logger.LogInformation($"Inserting add-on '{addOnName}' with quantity '{quantity}', price '{price}', and total '{total}' into orderaddons");

                        await insertCommand.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    // Update quantity and total for existing add-on in orderaddons
                    string updateSql = @"UPDATE orderaddons 
                                 SET quantity = @quantity, Total = @total
                                 WHERE OrderId = UNHEX(@orderId) AND name = @addOnName";

                    using (var updateCommand = new MySqlCommand(updateSql, connection, transaction))
                    {
                        updateCommand.Parameters.AddWithValue("@quantity", quantity);
                        updateCommand.Parameters.AddWithValue("@total", total);
                        updateCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                        updateCommand.Parameters.AddWithValue("@addOnName", addOnName);

                        _logger.LogInformation($"Updating quantity for add-on '{addOnName}' to '{quantity}', and total to '{total}' in orderaddons");

                        await updateCommand.ExecuteNonQueryAsync();
                    }
                }
            }
        }



        [HttpPatch("{orderId}/add_new_add_ons")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> AddNewAddOnToOrder(string orderId, [FromBody] AddNewAddOnRequest request)
        {
            try
            {
                _logger.LogInformation($"Starting AddNewAddOnToOrder for orderId: {orderId}");

                // Convert orderId to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Retrieve add-ons from the AddOns table
                List<AddOnDSOS> addOnDSOSList = await GetAddOnDSOSFromDatabase();

                // Find the add-on in the retrieved list
                var addOnDSOS = addOnDSOSList.FirstOrDefault(a => a.AddOnName == request.AddOnName);
                if (addOnDSOS == null)
                {
                    return BadRequest($"Add-on '{request.AddOnName}' not found in the AddOns table.");
                }

                // Calculate total price
                double total = request.Quantity * addOnDSOS.PricePerUnit;

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // Check if the add-on already exists in orderaddons
                            string selectSql = @"SELECT COUNT(*) 
                                         FROM orderaddons 
                                         WHERE OrderId = UNHEX(@orderId) AND name = @addOnName";

                            using (var selectCommand = new MySqlCommand(selectSql, connection, transaction))
                            {
                                selectCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                                selectCommand.Parameters.AddWithValue("@addOnName", request.AddOnName);

                                int count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                                if (count == 0)
                                {
                                    // Insert new add-on into orderaddons
                                    string insertSql = @"INSERT INTO orderaddons (OrderId, addOnsId, name, quantity, Price, Total)
                                                 VALUES (UNHEX(@orderId), @addOnsId, @addOnName, @quantity, @price, @total)";

                                    using (var insertCommand = new MySqlCommand(insertSql, connection, transaction))
                                    {
                                        insertCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                                        insertCommand.Parameters.AddWithValue("@addOnsId", addOnDSOS.AddOnId);  // Corrected to use addOnDSOS.AddOnId
                                        insertCommand.Parameters.AddWithValue("@addOnName", request.AddOnName);
                                        insertCommand.Parameters.AddWithValue("@quantity", request.Quantity);
                                        insertCommand.Parameters.AddWithValue("@price", addOnDSOS.PricePerUnit);
                                        insertCommand.Parameters.AddWithValue("@total", total);

                                        _logger.LogInformation($"Inserting add-on '{request.AddOnName}' with quantity '{request.Quantity}', price '{addOnDSOS.PricePerUnit}', and total '{total}' into orderaddons");

                                        await insertCommand.ExecuteNonQueryAsync();
                                    }
                                }
                                else
                                {
                                    // Update existing add-on in orderaddons
                                    string updateSql = @"UPDATE orderaddons 
                                                 SET quantity = @quantity, Total = @total 
                                                 WHERE OrderId = UNHEX(@orderId) AND name = @addOnName";

                                    using (var updateCommand = new MySqlCommand(updateSql, connection, transaction))
                                    {
                                        updateCommand.Parameters.AddWithValue("@quantity", request.Quantity);
                                        updateCommand.Parameters.AddWithValue("@total", total);
                                        updateCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                                        updateCommand.Parameters.AddWithValue("@addOnName", request.AddOnName);

                                        _logger.LogInformation($"Updating add-on '{request.AddOnName}' to quantity '{request.Quantity}' and total '{total}' in orderaddons");

                                        await updateCommand.ExecuteNonQueryAsync();
                                    }
                                }
                            }

                            await transaction.CommitAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Transaction failed, rolling back");
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }

                return Ok("Add-on successfully added or updated in the order.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding or updating add-on to order with ID '{orderId}'");
                return StatusCode(500, $"An error occurred while adding or updating add-on to order with ID '{orderId}'.");
            }
        }


        private async Task<List<AddOnDSOS>> GetAddOnDSOSFromDatabase()
        {
            List<AddOnDSOS> addOnDSOSList = new List<AddOnDSOS>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT addOnsId, name, price FROM AddOns";  // Ensure addOnsId is fetched

                using (var command = new MySqlCommand(sql, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var addOnDSOS = new AddOnDSOS
                            {
                                AddOnId = reader.GetInt32("addOnsId"),  // Added this line to fetch addOnsId
                                AddOnName = reader.GetString("name"),
                                PricePerUnit = reader.GetDouble("price")
                            };

                            addOnDSOSList.Add(addOnDSOS);
                        }
                    }
                }
            }

            return addOnDSOSList;
        }

        [HttpPatch("{orderId}/update_order_details")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> UpdateOrderDetails(string orderId, [FromBody] UpdateOrderDetailsRequest request)
        {
            try
            {
                _logger.LogInformation($"Starting UpdateOrderDetails for orderId: {orderId}");

                // Convert orderId to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // Update order details in the orders table
                            await UpdateOrderDetailsInDatabase(connection, transaction, orderIdBinary, request);

                            await transaction.CommitAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Transaction failed, rolling back");
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }

                return Ok("Order details successfully updated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating order details for order with ID '{orderId}'");
                return StatusCode(500, $"An error occurred while updating order details for order with ID '{orderId}'.");
            }
        }

        private async Task UpdateOrderDetailsInDatabase(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary, UpdateOrderDetailsRequest request)
        {
            // Prepare SQL statement for updating orders table
            string updateSql = @"UPDATE orders 
                         SET Description = @description, 
                             quantity = @quantity,
                             Size = @size,
                             Flavor = @flavor
                         WHERE OrderId = UNHEX(@orderId)";

            using (var command = new MySqlCommand(updateSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@description", request.Description);
                command.Parameters.AddWithValue("@quantity", request.Quantity);
                command.Parameters.AddWithValue("@size", request.Size);
                command.Parameters.AddWithValue("@flavor", request.Flavor);
                command.Parameters.AddWithValue("@orderId", orderIdBinary);

                await command.ExecuteNonQueryAsync();

                _logger.LogInformation($"Updated order details in orders table for order with ID '{orderIdBinary}'");
            }
        }


        [HttpPatch("assign_employee")]
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
                using (var designcommand = new MySqlCommand(designQuery, connection))
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


