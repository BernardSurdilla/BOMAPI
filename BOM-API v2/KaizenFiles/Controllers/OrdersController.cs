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
using BOM_API_v2.Helpers;
using System.Text.Json;

namespace BOM_API_v2.KaizenFiles.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IConfiguration configuration, ILogger<OrdersController> logger)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;
        }

        [HttpPost("customer/add-to-cart")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> CreateOrder([FromBody] OrderDTO orderDto, [FromQuery] string designName, [FromQuery] string pickupDate, [FromQuery] string pickupTime, [FromQuery] string description, [FromQuery] string flavor, [FromQuery] string size, [FromQuery] string type)
        {
            try
            {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
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
                    price = orderDto.Price,
                    quantity = orderDto.Quantity,
                    designName = designame,
                    type = type,
                    size = size,
                    flavor = flavor,
                    isActive = false,
                    customerName = customerUsername // Set customer name from authenticated user
                };

                // Set isActive to false for all orders created

                // Set the status to pending
                order.status = "pending";

                // Validate and parse the pickup date string to DateTime
                if (!DateTime.TryParseExact(pickupDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    return BadRequest("Invalid pickup date format. Use 'yyyy-MM-dd'.");
                }

                // Validate and parse the pickup time string to DateTime
                if (!DateTime.TryParseExact(pickupTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                {
                    return BadRequest("Invalid pickup time format. Use 'h:mm tt'.");
                }

                // Combine the pickup date and time into a single DateTime object
                DateTime pickupDateTime = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, parsedTime.Hour, parsedTime.Minute, 0);

                // Set the combined pickup date and time
                order.PickupDateTime = pickupDateTime;

                order.Description = description;

                // Convert designId to hex string
                string designIdHex = BitConverter.ToString(designId).Replace("-", "").ToLower();


                // Get the pastry material ID using just the design ID
                string subersId = await GetPastryMaterialIdByDesignIds(designIdHex);

                // Get the pastry material ID using the design ID and size
                string pastryMaterialId = await GetPastryMaterialIdBySubersIdAndSize(subersId, size);

                // Get the pastry material sub-variant ID using the pastry material ID and size
                string subVariantId = await GetPastryMaterialSubVariantId(subersId, size);

                // Determine the appropriate pastryId to use
                string pastryId = !string.IsNullOrEmpty(subVariantId) ? subVariantId : pastryMaterialId;

                // Insert the order into the database using the determined pastryId
                await InsertOrder(order, designId, flavor, size, pastryId);

                return Ok(); // Return 200 OK if the order is successfully created
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while creating the order");
                return StatusCode(500, "An error occurred while processing the request"); // Return 500 Internal Server Error
            }
        }


        private async Task<string> GetPastryMaterialIdByDesignIds(string designIdHex)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT pastry_material_id FROM pastrymaterials WHERE design_id = UNHEX(@designId)";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@designId", designIdHex);

                    var result = await command.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value ? result.ToString() : null;
                }
            }
        }

        private async Task<string> GetPastryMaterialIdBySubersIdAndSize(string subersId, string size)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT pastry_material_id FROM pastrymaterials WHERE pastry_material_id = @subersId AND main_variant_name = @size";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@subersId", subersId);
                    command.Parameters.AddWithValue("@size", size);

                    var result = await command.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value ? result.ToString() : null;
                }
            }
        }


        /*[HttpPost("current-user/add-to-cart")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> CreateCartOrder([FromQuery] double price, [FromQuery] int quantity, [FromQuery] string designName, [FromQuery] string description, [FromQuery] string flavor, [FromQuery] string size)
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
                    price = price,
                    designName = designName,
                    quantity = quantity,
                    type = "cart", // Automatically set the type to "cart"
                    size = size,
                    flavor = flavor,
                    isActive = false
                };

                // Set the status to pending
                order.status = "pending";

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
            (OrderId, CustomerId, EmployeeId, CreatedAt, Status, DesignId, price, quantity, last_updated_by, last_updated_at, type, isActive, PickupDateTime, Description, Flavor, Size, CustomerName, DesignName) 
            VALUES 
            (UNHEX(REPLACE(UUID(), '-', '')), @customerId, NULL, NOW(), @status, @designId, @order_name, @price, @quantity, NULL, NULL, @type, @isActive, @pickupDateTime, @Description, @Flavor, @Size, @customerName, @DesignName)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customerId", customerId);
                    command.Parameters.AddWithValue("@designId", designId);
                    command.Parameters.AddWithValue("@status", order.status);
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
        */


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

                    string sql = "SELECT OrderId, CustomerId, EmployeeId, CreatedAt, Status, HEX(DesignId) as DesignId, DesignName, price, quantity, last_updated_by, last_updated_at, type, isActive, PickupDateTime, Description, Flavor, Size, CustomerName, EmployeeName FROM orders WHERE type IN ('normal', 'rush')";

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

        [HttpGet("current-user/all-orders")]
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
                    OrderId, Status, DesignName, price, quantity, type, 
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

        [HttpGet("current-user/for-confirmation-orders")]
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
            OrderId, Status, DesignName, price, quantity, type, 
            Description, Flavor, Size, PickupDateTime
        FROM orders 
        WHERE CustomerId = (SELECT UserId FROM users WHERE Username = @customerUsername)
        AND status = 'confirmation' ";

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
                                string orderIdBinary = ConvertGuidToBinary16(orderId.ToString()).ToLower();

                                // Retrieve DesignId and Size
                                var designIdAndSize = await GetDesignIdAndSizeByOrderId(orderIdBinary);

                                // Initialize OrderSummary with the fetched details
                                var orderSummary = new OrderSummary
                                {
                                    Id = orderId,
                                    Status = reader.GetString(reader.GetOrdinal("Status")),
                                    DesignName = reader.GetString(reader.GetOrdinal("DesignName")),
                                    Price = reader.GetDouble(reader.GetOrdinal("price")),
                                    Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    Type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString(reader.GetOrdinal("type")),
                                    Description = reader.GetString(reader.GetOrdinal("Description")),
                                    Flavor = reader.GetString(reader.GetOrdinal("Flavor")),
                                    Size = reader.GetString(reader.GetOrdinal("Size")),
                                    PickupDateTime = reader.GetDateTime(reader.GetOrdinal("PickupDateTime"))
                                };

                                if (designIdAndSize.designIdHex != null && designIdAndSize.size != null)
                                {
                                    // Retrieve PastryMaterialId using DesignId
                                    string pastryMaterialId = await GetPastryMaterialIdByDesignId(designIdAndSize.designIdHex);
                                    if (pastryMaterialId != null)
                                    {
                                        // Set PastryMaterialId
                                        orderSummary.PastryMaterialId = pastryMaterialId;

                                        // Retrieve variantId
                                        orderSummary.variantId = await GetVariantIdByPastryMaterialIdAndSize(pastryMaterialId, designIdAndSize.size);
                                    }
                                }

                                orders.Add(orderSummary);
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


        [HttpGet("admin/employees-name")]
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

        [HttpGet("current-user/{orderId}/add-ons")] //might remove this 
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetAddOnsByOrderId(string orderId)
        {
            try
            {
                // Convert orderId to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Fetch DesignId and Size for the given orderId
                var (designIdHex, size) = await GetDesignIdAndSizeByOrderId(orderIdBinary);

                if (string.IsNullOrEmpty(designIdHex))
                {
                    return NotFound($"No DesignId found for order with ID '{orderId}'.");
                }

                // Fetch pastry_material_id using DesignId
                string pastryMaterialId = await GetPastryMaterialIdByDesignId(designIdHex);

                if (pastryMaterialId == null)
                {
                    return NotFound($"No pastry material found for designId '{designIdHex}'.");
                }

                // Retrieve list of add_ons_id and amount from pastymaterialaddons table
                var mainVariantAddOns = await GetMainVariantAddOns(pastryMaterialId, size);

                // Retrieve pastry_material_sub_variant_id from pastrymaterialsubvariants table
                string subVariantId = await GetPastryMaterialSubVariantId(pastryMaterialId, size);

                // Retrieve list of add_ons_id and amount from pastrymaterialsubvariantaddons table
                var subVariantAddOns = subVariantId != null ? await GetSubVariantAddOns(subVariantId) : new List<(int, int)>();

                var allAddOns = new Dictionary<int, int>();
                foreach (var (addOnsId, amount) in mainVariantAddOns)
                {
                    if (allAddOns.ContainsKey(addOnsId))
                    {
                        allAddOns[addOnsId] += amount;
                    }
                    else
                    {
                        allAddOns[addOnsId] = amount;
                    }
                }

                foreach (var (addOnsId, amount) in subVariantAddOns)
                {
                    if (allAddOns.ContainsKey(addOnsId))
                    {
                        allAddOns[addOnsId] += amount;
                    }
                    else
                    {
                        allAddOns[addOnsId] = amount;
                    }
                }

                var addOns = new List<AddOnDPOS>();

                foreach (var addOnsId in allAddOns.Keys)
                {
                    var details = await GetAddOnsDetailsByAddOnsId(addOnsId);
                    foreach (var detail in details)
                    {
                        detail.Quantity = allAddOns[addOnsId]; // Set quantity from the combined total
                        detail.AddOnId = addOnsId; // Set the AddOnId
                        addOns.Add(detail);
                    }
                }

                if (addOns.Count == 0)
                {
                    return NotFound($"No add-ons found for pastry material ID '{pastryMaterialId}' with Size '{size}'.");
                }

                // Prepare the response object
                var response = new orderAddons
                {
                    pastryId = pastryMaterialId,
                    addOnDPOs = addOns
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving add-ons for order with ID '{orderId}'");
                return StatusCode(500, $"An error occurred while retrieving add-ons for order with ID '{orderId}'.");
            }
        }



        private async Task<(string designIdHex, string size)> GetDesignIdAndSizeByOrderId(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT DesignId, Size FROM orders WHERE OrderId = UNHEX(@orderId)";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            byte[] designIdBinary = (byte[])reader["DesignId"];
                            string designIdHex = BitConverter.ToString(designIdBinary).Replace("-", "").ToLower();
                            string size = reader.GetString("Size");

                            return (designIdHex, size);
                        }
                        else
                        {
                            return (null, null); // Order not found or does not have a design associated
                        }
                    }
                }
            }
        }

        private async Task<string> GetPastryMaterialIdByDesignId(string designIdHex)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT pastry_material_id FROM pastrymaterials WHERE design_id = UNHEX(@designId)";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@designId", designIdHex);

                    var result = await command.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value ? result.ToString() : null;
                }
            }
        }

        private async Task<List<(int addOnsId, int quantity)>> GetMainVariantAddOns(string pastryMaterialId, string size)
        {
            var addOns = new List<(int, int)>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT pma.add_ons_id, pma.amount
            FROM pastymaterialaddons pma
            JOIN pastrymaterials pm ON pm.pastry_material_id = pma.pastry_material_id
            WHERE pma.pastry_material_id = @pastryMaterialId
              AND pm.main_variant_name = @size";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@pastryMaterialId", pastryMaterialId);
                    command.Parameters.AddWithValue("@size", size);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var addOnsId = reader.GetInt32("add_ons_id");
                            var quantity = reader.GetInt32("amount");
                            addOns.Add((addOnsId, quantity));
                        }
                    }
                }
            }

            return addOns;
        }

        private async Task<string> GetPastryMaterialSubVariantId(string pastryMaterialId, string size)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT pastry_material_sub_variant_id
            FROM pastrymaterialsubvariants
            WHERE pastry_material_id = @pastryMaterialId
              AND sub_variant_name = @size";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@pastryMaterialId", pastryMaterialId);
                    command.Parameters.AddWithValue("@size", size);

                    var subVariantId = await command.ExecuteScalarAsync();
                    return subVariantId?.ToString();
                }
            }
        }

        private async Task<List<(int addOnsId, int quantity)>> GetSubVariantAddOns(string subVariantId)
        {
            var addOns = new List<(int, int)>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT add_ons_id, amount
            FROM pastrymaterialsubvariantaddons
            WHERE pastry_material_sub_variant_id = @subVariantId";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@subVariantId", subVariantId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var addOnsId = reader.GetInt32("add_ons_id");
                            var quantity = reader.GetInt32("amount");
                            addOns.Add((addOnsId, quantity));
                        }
                    }
                }
            }

            return addOns;
        }

        private async Task<List<AddOnDPOS>> GetAddOnsDetailsByAddOnsId(int addOnsId)
        {
            var addOns = new List<AddOnDPOS>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT name, price FROM addons WHERE addOnsId = @addOnsId";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@addOnsId", addOnsId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var addOn = new AddOnDPOS
                            {
                                AddOnId = addOnsId, // Set AddOnId here
                                AddOnName = reader.GetString("name"),
                                PricePerUnit = reader.GetDouble("price"),
                                Quantity = 0 // Placeholder, will be set in the main method
                            };

                            addOns.Add(addOn);
                        }
                    }
                }
            }

            return addOns;
        }



        [HttpGet("admin/total-orders")]
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


        [HttpGet("customer/final-order-details/{orderId}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrderByOrderId(string orderId)
        {
            try
            {
                // Convert the hex string to a binary(16) formatted string
                string binary16OrderId = ConvertGuidToBinary16(orderId).ToLower();

                // Fetch the specific order and its addons from the database
                FinalOrder finalOrder = await GetFinalOrderByIdFromDatabase(binary16OrderId);

                if (finalOrder == null)
                {
                    return NotFound($"Order with orderId {orderId} not found.");
                }

                // Retrieve DesignId and Size
                var designIdAndSize = await GetDesignIdAndSizeByOrderId(binary16OrderId);
                if (designIdAndSize.designIdHex != null && designIdAndSize.size != null)
                {
                    // Retrieve PastryMaterialId using DesignId
                    finalOrder.PastryMaterialId = await GetPastryMaterialIdByDesignId(designIdAndSize.designIdHex);

                    if (finalOrder.PastryMaterialId != null)
                    {
                        // Retrieve variantId
                        finalOrder.variantId = await GetVariantIdByPastryMaterialIdAndSize(finalOrder.PastryMaterialId, designIdAndSize.size);
                    }
                }

                // Calculate the total from orderaddons
                double totalFromOrderAddons = await GetTotalFromOrderAddons(binary16OrderId);

                // Calculate allTotal as sum of Price and totalFromOrderAddons
                finalOrder.allTotal = finalOrder.Price + totalFromOrderAddons;

                return Ok(finalOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving order by orderId {orderId}");
                return StatusCode(500, $"An error occurred while processing the request.");
            }
        }


        private async Task<string> GetVariantIdByPastryMaterialIdAndSize(string pastryMaterialId, string size)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Check in pastrymaterials table
                string mainVariantSql = @"
            SELECT pastry_material_id
            FROM pastrymaterials
            WHERE pastry_material_id = @pastryMaterialId AND main_variant_name = @size";

                using (var mainVariantCommand = new MySqlCommand(mainVariantSql, connection))
                {
                    mainVariantCommand.Parameters.AddWithValue("@pastryMaterialId", pastryMaterialId);
                    mainVariantCommand.Parameters.AddWithValue("@size", size);

                    var result = await mainVariantCommand.ExecuteScalarAsync();
                    if (result != null)
                    {
                        return pastryMaterialId; // Return the pastry_material_id as variantId
                    }
                }

                // Check in pastrymaterialsubvariants table
                string subVariantSql = @"
            SELECT pastry_material_sub_variant_id
            FROM pastrymaterialsubvariants
            WHERE pastry_material_id = @pastryMaterialId AND sub_variant_name = @size";

                using (var subVariantCommand = new MySqlCommand(subVariantSql, connection))
                {
                    subVariantCommand.Parameters.AddWithValue("@pastryMaterialId", pastryMaterialId);
                    subVariantCommand.Parameters.AddWithValue("@size", size);

                    var result = await subVariantCommand.ExecuteScalarAsync();
                    if (result != null)
                    {
                        return result.ToString(); // Return the pastry_material_sub_variant_id as variantId
                    }
                }
            }

            return null; // Return null if no matching variant is found
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

                // Query to get order details
                string orderSql = @"
        SELECT DesignName, price, quantity, Size, Flavor, type, Description, PickupDateTime
        FROM orders
        WHERE OrderId = UNHEX(@orderId)";

                // Query to get add-on IDs, quantity, and total from orderaddons
                string addOnsSql = @"
        SELECT addOnsId, quantity, Total
        FROM orderaddons
        WHERE OrderId = UNHEX(@orderId)";

                FinalOrder finalOrder = null;

                // Get order details
                using (var orderCommand = new MySqlCommand(orderSql, connection))
                {
                    orderCommand.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await orderCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            finalOrder = new FinalOrder
                            {
                                designName = reader.GetString("DesignName"),
                                Price = reader.GetDouble("price"),
                                Quantity = reader.GetInt32("quantity"),
                                Description = reader.GetString("Description"),
                                size = reader.GetString("Size"),
                                flavor = reader.GetString("Flavor"),
                                type = reader.IsDBNull(reader.GetOrdinal("type")) ? null : reader.GetString("type"),
                                PickupDateTime = reader.GetDateTime("PickupDateTime"),
                                AddOns = new List<AddOnDetails2>(),
                                customAddons = new List<CustomAddons>()
                            };
                        }
                    }
                }

                if (finalOrder != null)
                {
                    // Get add-on IDs, quantity, and total
                    List<AddOnDetails2> addOnsDetails = new List<AddOnDetails2>();

                    using (var addOnsCommand = new MySqlCommand(addOnsSql, connection))
                    {
                        addOnsCommand.Parameters.AddWithValue("@orderId", orderId);

                        using (var reader = await addOnsCommand.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                int? addOnsId = reader.IsDBNull(reader.GetOrdinal("addOnsId")) ? (int?)null : reader.GetInt32("addOnsId");
                                int quantity = reader.GetInt32("quantity");
                                double total = reader.GetDouble("Total");

                                // Fetch add-on details (name and price) for each addOnsId
                                var addOnDetails = await GetAddOnDetailsById(addOnsId);
                                if (addOnDetails != null)
                                {
                                    addOnDetails.quantity = quantity;
                                    addOnDetails.total = total;
                                    addOnsDetails.Add(addOnDetails);
                                }
                            }
                        }
                    }

                    finalOrder.AddOns = addOnsDetails;

                    // Fetch custom add-ons
                    finalOrder.customAddons = await GetCustomAddonsByOrderId(orderId);
                }

                return finalOrder;
            }
        }

        // Method to fetch add-on details from the addons table by addOnsId
        private async Task<AddOnDetails2> GetAddOnDetailsById(int? addOnsId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT name, price FROM addons WHERE addOnsId = @addOnsId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@addOnsId", addOnsId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new AddOnDetails2
                            {
                                name = reader.GetString("name"),
                                pricePerUnit = reader.GetDouble("price")
                            };
                        }
                    }
                }
            }

            return null;
        }

        private async Task<List<CustomAddons>> GetCustomAddonsByOrderId(string orderId)
        {
            List<CustomAddons> customAddonsList = new List<CustomAddons>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
        SELECT name, price, quantity
        FROM orderaddons
        WHERE OrderId = UNHEX(@orderId) AND name LIKE 'custom%'";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            CustomAddons customAddon = new CustomAddons
                            {
                                name = reader.IsDBNull(reader.GetOrdinal("name")) ? null : reader.GetString("name"),
                                price = reader.IsDBNull(reader.GetOrdinal("price")) ? (double?)null : reader.GetDouble("price"),
                                quantity = reader.IsDBNull(reader.GetOrdinal("quantity")) ? (int?)null : reader.GetInt32("quantity")
                            };

                            customAddonsList.Add(customAddon);
                        }
                    }
                }
            }

            return customAddonsList;
        }




        [HttpGet("current-user/order-type/{type}")]
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

        [HttpGet("admin/by-employee-username")]
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



        [HttpGet("admin/by-customer-username/{customerUsername}")]
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


        [HttpGet("admin/by-type/{type}/{username}")]
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

        [HttpGet("admin/inactive")]
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

        [HttpGet("admin/active")]
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

        [HttpGet("customer/cart")]
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


        [HttpPatch("admin/update-price")] //change this 
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> UpdateOrderAddon([FromQuery] string orderId, [FromQuery] string name, [FromQuery] decimal price)
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
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

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

        [HttpPatch("admin/send-back-to-customer-no-change")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> UpdateOrderStatus([FromQuery] string orderId)
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
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

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

                return Ok("Order status updated to confirmation successfully");
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while updating the order status");
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


        [HttpPatch("customer/confirmation")] //debug this 
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
        public async Task<IActionResult> ConfirmOrCancelOrder([FromQuery] string orderId, [FromQuery] string action)
        {
            try
            {
                // Convert the GUID string to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

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
                            // Retrieve addOnsId and quantity from orderaddons table
                            string sqlGetOrderAddOns = @"SELECT addOnsId, quantity FROM orderaddons WHERE OrderId = UNHEX(@orderId)";

                            List<(int? AddOnsId, int Quantity)> orderAddOnsList = new List<(int?, int)>();

                            using (var getOrderAddOnsCommand = new MySqlCommand(sqlGetOrderAddOns, connection))
                            {
                                getOrderAddOnsCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                                using (var reader = await getOrderAddOnsCommand.ExecuteReaderAsync())
                                {
                                    while (await reader.ReadAsync())
                                    {
                                        // Retrieve addOnsId and check for null
                                        int? addOnsId = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                                        int quantity = reader.GetInt32(1);
                                        orderAddOnsList.Add((addOnsId, quantity));
                                    }
                                }
                            }

                            // Update AddOns quantities for each entry in orderaddons
                            /*foreach (var (AddOnsId, Quantity) in orderAddOnsList)
                            {
                                // Only update if AddOnsId is not null
                                if (AddOnsId.HasValue)
                                {
                                    string sqlUpdateAddOns = "UPDATE addons SET quantity = quantity - @Quantity WHERE addOnsId = @AddOnsId";
                                    using (var updateAddOnsCommand = new MySqlCommand(sqlUpdateAddOns, connection))
                                    {
                                        updateAddOnsCommand.Parameters.AddWithValue("@Quantity", Quantity);
                                        updateAddOnsCommand.Parameters.AddWithValue("@AddOnsId", AddOnsId.Value);
                                        await updateAddOnsCommand.ExecuteNonQueryAsync();
                                    }
                                }
                            }*/

                            // Set isActive to true
                            await UpdateOrderStatus(orderIdBinary, true);
                            await UpdateStatus(orderIdBinary, "confirmed");

                            // Update the last_updated_at column
                            await UpdateLastUpdatedAt(orderIdBinary);
                        }

                        else
                        {
                            return BadRequest($"Order with ID '{orderId}' is already confirmed.");
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
                            return BadRequest($"Order with ID '{orderId}' is already canceled.");
                        }
                    }
                    else
                    {
                        return BadRequest("Invalid action. Please choose 'confirm' or 'cancel'.");
                    }

                    return Ok($"Order with ID '{orderId}' has been successfully {action}ed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to {action} order with ID '{orderId}'.");
                return StatusCode(500, $"An error occurred while processing the request to {action} order with ID '{orderId}'.");
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


        [HttpPatch("artist/update-order-status")]
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
                    command.CommandText = @"SELECT o.DesignName, o.price, o.EmployeeId, o.CreatedAt, o.quantity, 
                                    u.Contact, u.Email 
                                    FROM orders o
                                    JOIN users u ON o.EmployeeId = u.UserId
                                    WHERE o.OrderId = UNHEX(@orderId)";
                    command.Parameters.AddWithValue("@orderId", orderIdBytes);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            var name = reader.GetString("DesignName");
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


        [HttpPatch("customer/update-cart")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> PatchOrderTypeAndPickupDate(
            [FromQuery] string orderId,
            [FromQuery] string type,
            [FromQuery] string pickupDate,
            [FromQuery] string pickupTime)
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

                // Parse the pickup date string to DateTime
                if (!DateTime.TryParseExact(pickupDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
                {
                    return BadRequest("Invalid pickup date format. Use 'yyyy-MM-dd'.");
                }

                // Parse the pickup time string to TimeSpan
                if (!DateTime.TryParseExact(pickupTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                {
                    return BadRequest("Invalid pickup time format. Use 'h:mm tt'.");
                }

                // Combine the pickup date and time into a single DateTime object
                DateTime pickupDateTime = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, parsedTime.Hour, parsedTime.Minute, 0);

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

                string sql = "UPDATE orders SET type = @type, PickupDateTime = @pickupDate, status = 'for update' WHERE OrderId = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@type", type);
                    command.Parameters.AddWithValue("@pickupDate", pickupDateTime);
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<int?> GetExistingTotal(string DesignName)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT Total FROM sales WHERE Name = @orderName";
                    command.Parameters.AddWithValue("@orderName", DesignName);

                    var result = await command.ExecuteScalarAsync();
                    return result != null ? Convert.ToInt32(result) : (int?)null;
                }
            }
        }

        private async Task UpdateTotalInSalesTable(string DesignName, int newTotal)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "UPDATE sales SET total = @newTotal WHERE DesignName = @orderName";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@newTotal", newTotal);
                    command.Parameters.AddWithValue("@orderName", DesignName);

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

        /* [HttpPatch("{orderId}/manage_add_ons")]
         [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
         public async Task<IActionResult> ManageAddOnsByOrderId(string orderId, [FromBody] ManageAddOnsRequest request)
         {
             try
             {
                 _logger.LogInformation($"Starting ManageAddOnsByOrderId for orderId: {orderId}");

                 string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                 using (var connection = new MySqlConnection(connectionstring))
                 {
                     await connection.OpenAsync();
                     using (var transaction = await connection.BeginTransactionAsync())
                     {
                         try
                         {
                             List<OrderAddOn> existingOrderAddOns = await GetOrderAddOns(connection, transaction, orderIdBinary);
                             List<PastryMaterialAddOn> pastryMaterialAddOns = await GetAllPastryMaterialAddOns(connection, transaction, orderIdBinary);

                             var addedAddOns = new HashSet<int>(existingOrderAddOns.Select(a => a.AddOnId));

                             foreach (var action in request.Actions)
                             {
                                 if (action.ActionType.ToLower() == "setquantity")
                                 {
                                     var materialAddOn = pastryMaterialAddOns.FirstOrDefault(pma => pma.AddOnId == action.AddOnId);
                                     if (materialAddOn != null)
                                     {
                                         await SetOrUpdateAddOn(connection, transaction, orderIdBinary, materialAddOn.AddOnId, action.Quantity);
                                         addedAddOns.Add(materialAddOn.AddOnId);
                                     }
                                     else
                                     {
                                         return BadRequest($"Add-on ID '{action.AddOnId}' not found in pastrymaterialaddons for order with ID '{orderId}'.");
                                     }
                                 }
                                 else if (action.ActionType.ToLower() == "remove")
                                 {
                                     await SetOrUpdateAddOn(connection, transaction, orderIdBinary, action.AddOnId, 0);
                                     addedAddOns.Add(action.AddOnId);
                                 }
                             }

                             foreach (var materialAddOn in pastryMaterialAddOns)
                             {
                                 if (!addedAddOns.Contains(materialAddOn.AddOnId))
                                 {
                                     await SetOrUpdateAddOn(connection, transaction, orderIdBinary, materialAddOn.AddOnId, materialAddOn.Quantity);
                                 }
                             }

                             double totalFromOrderAddons = await GetTotalFromOrderAddons(connection, transaction, orderIdBinary);
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

         private async Task<List<PastryMaterialAddOn>> GetAllPastryMaterialAddOns(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary)
         {
             var pastryMaterialAddOns = new List<PastryMaterialAddOn>();

             string sql = @"SELECT pma.AddOnsId, pma.quantity, a.name, a.price
                    FROM pastymaterialaddons pma
                    JOIN addons a ON pma.AddOnsId = a.AddOnsId
                    WHERE pma.pastry_material_id = (
                        SELECT pastry_material_id FROM orders WHERE OrderId = UNHEX(@orderId)
                    )";

             using (var command = new MySqlCommand(sql, connection, transaction))
             {
                 command.Parameters.AddWithValue("@orderId", orderIdBinary);

                 using (var reader = await command.ExecuteReaderAsync())
                 {
                     while (await reader.ReadAsync())
                     {
                         pastryMaterialAddOns.Add(new PastryMaterialAddOn
                         {
                             AddOnId = reader.GetInt32("AddOnsId"),
                             Quantity = reader.GetInt32("quantity"),
                             AddOnName = reader.GetString("name"),
                             Price = reader.GetDouble("price")
                         });
                     }
                 }
             }

             return pastryMaterialAddOns;
         }

         private async Task<List<OrderAddOn>> GetOrderAddOns(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary)
         {
             var orderAddOns = new List<OrderAddOn>();

             string sql = @"SELECT addOnsId, name, quantity, price 
                    FROM orderaddons 
                    WHERE OrderId = UNHEX(@orderId)";

             using (var command = new MySqlCommand(sql, connection, transaction))
             {
                 command.Parameters.AddWithValue("@orderId", orderIdBinary);

                 using (var reader = await command.ExecuteReaderAsync())
                 {
                     while (await reader.ReadAsync())
                     {
                         orderAddOns.Add(new OrderAddOn
                         {
                             AddOnId = reader.GetInt32("addOnsId"),
                             AddOnName = reader.GetString("name"),
                             Quantity = reader.GetInt32("quantity"),
                             Price = reader.GetDouble("price")
                         });
                     }
                 }
             }

             return orderAddOns;
         }

         private async Task SetOrUpdateAddOn(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary, int addOnId, int quantity)
         {
             string getPriceSql = @"SELECT price 
                            FROM addons 
                            WHERE AddOnsId = @addOnId";

             double price = 0.0;

             using (var getPriceCommand = new MySqlCommand(getPriceSql, connection, transaction))
             {
                 getPriceCommand.Parameters.AddWithValue("@addOnId", addOnId);

                 object priceResult = await getPriceCommand.ExecuteScalarAsync();
                 if (priceResult != null && priceResult != DBNull.Value)
                 {
                     price = Convert.ToDouble(priceResult);
                 }
                 else
                 {
                     throw new Exception($"Price not found for add-on ID '{addOnId}'.");
                 }
             }

             double total = quantity * price;

             string selectSql = @"SELECT COUNT(*) 
                          FROM orderaddons 
                          WHERE OrderId = UNHEX(@orderId) AND addOnsId = @addOnId";

             using (var selectCommand = new MySqlCommand(selectSql, connection, transaction))
             {
                 selectCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                 selectCommand.Parameters.AddWithValue("@addOnId", addOnId);

                 int count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                 if (count == 0)
                 {
                     string insertSql = @"INSERT INTO orderaddons (OrderId, addOnsId, name, quantity, price, total)
                                  VALUES (UNHEX(@orderId), @addOnId, 
                                          (SELECT name FROM addons WHERE AddOnsId = @addOnId), 
                                          @quantity, @price, @total)";

                     using (var insertCommand = new MySqlCommand(insertSql, connection, transaction))
                     {
                         insertCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                         insertCommand.Parameters.AddWithValue("@addOnId", addOnId);
                         insertCommand.Parameters.AddWithValue("@quantity", quantity);
                         insertCommand.Parameters.AddWithValue("@price", price);
                         insertCommand.Parameters.AddWithValue("@total", total);

                         _logger.LogInformation($"Inserting add-on ID '{addOnId}' with quantity '{quantity}', price '{price}', and total '{total}' into orderaddons");

                         await insertCommand.ExecuteNonQueryAsync();
                     }
                 }
                 else
                 {
                     string updateSql = @"UPDATE orderaddons 
                                  SET quantity = @quantity, total = @total
                                  WHERE OrderId = UNHEX(@orderId) AND addOnsId = @addOnId";

                     using (var updateCommand = new MySqlCommand(updateSql, connection, transaction))
                     {
                         updateCommand.Parameters.AddWithValue("@quantity", quantity);
                         updateCommand.Parameters.AddWithValue("@total", total);
                         updateCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                         updateCommand.Parameters.AddWithValue("@addOnId", addOnId);

                         _logger.LogInformation($"Updating quantity for add-on ID '{addOnId}' to '{quantity}', and total to '{total}' in orderaddons");

                         await updateCommand.ExecuteNonQueryAsync();
                     }
                 }
             }
         }

         private async Task<double> GetTotalFromOrderAddons(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary)
         {
             string getTotalSql = @"SELECT SUM(total) AS TotalSum
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

         */

        [HttpPatch("customer/manage-add-ons-by-material/{pastryMaterialId}")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> ManageAddOnsByPastryMaterialId(string pastryMaterialId, [FromQuery] string orderId, [FromQuery] int modifiedAddOnId, [FromBody] ManageAddOnAction action)
        {
            try
            {
                _logger.LogInformation($"Starting ManageAddOnsByPastryMaterialId for pastryMaterialId: {pastryMaterialId}, OrderId: {orderId}, and AddOnId: {modifiedAddOnId}");

                // Convert OrderId to binary format
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // Retrieve size from orders table
                            string size = await GetOrderSize(connection, transaction, orderIdBinary);

                            // Retrieve add-ons based on pastryMaterialId and size
                            List<PastryMaterialAddOn> allAddOns = new List<PastryMaterialAddOn>();

                            // Check if size matches any sub-variant
                            List<PastryMaterialAddOn> pastryMaterialSubVariantAddOns = await GetPastryMaterialSubVariantAddOns(connection, transaction, pastryMaterialId, size);
                            if (pastryMaterialSubVariantAddOns.Any())
                            {
                                allAddOns.AddRange(pastryMaterialSubVariantAddOns);
                            }

                            // Always retrieve add-ons from pastymaterialaddons regardless of size
                            List<PastryMaterialAddOn> pastryMaterialAddOns = await GetPastryMaterialAddOns(connection, transaction, pastryMaterialId);
                            allAddOns.AddRange(pastryMaterialAddOns);

                            // Fetch add-on details only once for efficiency
                            Dictionary<int, (string Name, double Price)> addOnDetailsDict = new Dictionary<int, (string Name, double Price)>();
                            foreach (var addOn in allAddOns)
                            {
                                var addOnDetails = await GetAddOnDetails(connection, transaction, addOn.AddOnId);
                                addOnDetailsDict[addOn.AddOnId] = addOnDetails;
                            }

                            // Process the action
                            foreach (var addOn in allAddOns)
                            {
                                if (addOn.AddOnId == modifiedAddOnId)
                                {
                                    if (action.ActionType.ToLower() == "setquantity")
                                    {
                                        // Fetch add-on details
                                        if (addOnDetailsDict.TryGetValue(addOn.AddOnId, out var addOnDetails))
                                        {
                                            // Calculate total price
                                            double total = action.Quantity * addOnDetails.Price;

                                            // Insert or update quantity for the specified add-on in orderaddons
                                            await SetOrUpdateAddOn(connection, transaction, orderIdBinary, addOn.AddOnId, action.Quantity, total);
                                        }
                                    }
                                    else if (action.ActionType.ToLower() == "remove")
                                    {
                                        // Set quantity to 0 and remove add-on from orderaddons
                                        await SetOrUpdateAddOn(connection, transaction, orderIdBinary, addOn.AddOnId, 0, 0);
                                    }
                                    else
                                    {
                                        return BadRequest($"Unsupported action type '{action.ActionType}'.");
                                    }
                                }
                                else
                                {
                                    // Insert add-on without modifying its quantity or total
                                    var addOnDetails = addOnDetailsDict[addOn.AddOnId];
                                    double total = addOn.Quantity * addOnDetails.Price;
                                    await SetOrUpdateAddOn(connection, transaction, orderIdBinary, addOn.AddOnId, addOn.Quantity, total);
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

                return Ok("Add-ons quantities successfully managed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error managing add-ons for pastry material with ID '{pastryMaterialId}'");
                return StatusCode(500, $"An error occurred while managing add-ons for pastry material with ID '{pastryMaterialId}'.");
            }
        }

        private async Task<string> GetOrderSize(MySqlConnection connection, MySqlTransaction transaction, string orderId)
        {
            string sql = @"SELECT Size
                   FROM orders
                   WHERE OrderId = UNHEX(@orderId)";

            using (var command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@orderId", orderId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return reader.GetString("size");
                    }
                    else
                    {
                        throw new Exception($"Order size not found for OrderId '{orderId}'.");
                    }
                }
            }
        }

        private async Task<List<PastryMaterialAddOn>> GetPastryMaterialAddOns(MySqlConnection connection, MySqlTransaction transaction, string pastryMaterialId)
        {
            List<PastryMaterialAddOn> pastryMaterialAddOns = new List<PastryMaterialAddOn>();

            string sql = @"SELECT add_ons_id AS AddOnId, amount AS DefaultQuantity
                   FROM pastymaterialaddons
                   WHERE pastry_material_id = @pastryMaterialId";

            using (var command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@pastryMaterialId", pastryMaterialId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        PastryMaterialAddOn addOn = new PastryMaterialAddOn
                        {
                            AddOnId = reader.GetInt32("AddOnId"),
                            Quantity = reader.GetInt32("DefaultQuantity")
                        };

                        pastryMaterialAddOns.Add(addOn);
                    }
                }
            }

            return pastryMaterialAddOns;
        }

        private async Task<List<PastryMaterialAddOn>> GetPastryMaterialSubVariantAddOns(MySqlConnection connection, MySqlTransaction transaction, string pastryMaterialId, string size)
        {
            List<PastryMaterialAddOn> pastryMaterialAddOns = new List<PastryMaterialAddOn>();

            string sql = @"SELECT pmsa.add_ons_id AS AddOnId, pmsa.amount AS DefaultQuantity
                   FROM pastrymaterialsubvariantaddons pmsa
                   JOIN pastrymaterialsubvariants pmsv ON pmsa.pastry_material_sub_variant_id = pmsv.pastry_material_sub_variant_id
                   WHERE pmsv.pastry_material_id = @pastryMaterialId AND pmsv.sub_variant_name = @size";

            using (var command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@pastryMaterialId", pastryMaterialId);
                command.Parameters.AddWithValue("@size", size);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        PastryMaterialAddOn addOn = new PastryMaterialAddOn
                        {
                            AddOnId = reader.GetInt32("AddOnId"),
                            Quantity = reader.GetInt32("DefaultQuantity")
                        };

                        pastryMaterialAddOns.Add(addOn);
                    }
                }
            }

            return pastryMaterialAddOns;
        }

        private async Task<(string Name, double Price)> GetAddOnDetails(MySqlConnection connection, MySqlTransaction transaction, int addOnId)
        {
            string sql = @"SELECT name, price
                   FROM addons
                   WHERE AddOnsId = @addOnId";

            using (var command = new MySqlCommand(sql, connection, transaction))
            {
                command.Parameters.AddWithValue("@addOnId", addOnId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        string name = reader.GetString("name");
                        double price = reader.GetDouble("price");
                        return (name, price);
                    }
                    else
                    {
                        throw new Exception($"Add-on details not found for ID '{addOnId}'.");
                    }
                }
            }
        }

        private async Task SetOrUpdateAddOn(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary, int addOnId, int quantity, double total)
        {
            if (quantity > 0)
            {
                // Check if the add-on already exists in orderaddons
                string selectSql = @"SELECT COUNT(*) 
                             FROM orderaddons 
                             WHERE OrderId = UNHEX(@orderId) AND AddOnsId = @addOnId";

                using (var selectCommand = new MySqlCommand(selectSql, connection, transaction))
                {
                    selectCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                    selectCommand.Parameters.AddWithValue("@addOnId", addOnId);

                    int count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                    if (count == 0)
                    {
                        // Insert new add-on into orderaddons
                        string insertSql = @"INSERT INTO orderaddons (OrderId, AddOnsId, quantity, total)
                                     VALUES (UNHEX(@orderId), @addOnId, @quantity, @total)";
                        using (var insertCommand = new MySqlCommand(insertSql, connection, transaction))
                        {
                            insertCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                            insertCommand.Parameters.AddWithValue("@addOnId", addOnId);
                            insertCommand.Parameters.AddWithValue("@quantity", quantity);
                            insertCommand.Parameters.AddWithValue("@total", total);

                            _logger.LogInformation($"Inserting add-on ID '{addOnId}' with quantity '{quantity}', and total '{total}' into orderaddons");

                            await insertCommand.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        // Update quantity and total for existing add-on in orderaddons
                        string updateSql = @"UPDATE orderaddons 
                                 SET quantity = @quantity, total = @total
                                 WHERE OrderId = UNHEX(@orderId) AND AddOnsId = @addOnId";

                        using (var updateCommand = new MySqlCommand(updateSql, connection, transaction))
                        {
                            updateCommand.Parameters.AddWithValue("@quantity", quantity);
                            updateCommand.Parameters.AddWithValue("@total", total);
                            updateCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                            updateCommand.Parameters.AddWithValue("@addOnId", addOnId);

                            _logger.LogInformation($"Updating quantity for add-on ID '{addOnId}' to '{quantity}', and total to '{total}' in orderaddons");

                            await updateCommand.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            else
            {
                // If quantity is 0, remove the add-on from orderaddons
                await RemoveAddOnFromOrderAddOns(connection, transaction, orderIdBinary, addOnId);
            }
        }

        private async Task RemoveAddOnFromOrderAddOns(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary, int addOnId)
        {
            string deleteSql = @"DELETE FROM orderaddons WHERE OrderId = UNHEX(@orderId) AND AddOnsId = @addOnId";
            using (var deleteCommand = new MySqlCommand(deleteSql, connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                deleteCommand.Parameters.AddWithValue("@addOnId", addOnId);

                _logger.LogInformation($"Removing add-on ID '{addOnId}' from orderaddons");

                await deleteCommand.ExecuteNonQueryAsync();
            }
        }




        [HttpPatch("customer/{orderId}/add-new-add-ons")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> AddNewAddOnToOrder(string orderId, [FromBody] AddNewAddOnRequest request)
        {
            try
            {
                _logger.LogInformation($"Starting AddNewAddOnToOrder for orderId: {orderId}");

                // Convert orderId to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Retrieve the add-on details from the AddOns table based on the name
                var addOnDSOS = await GetAddOnByNameFromDatabase(request.AddOnName);
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
                                         WHERE OrderId = UNHEX(@orderId) AND AddOnsId = @addOnsId";

                            using (var selectCommand = new MySqlCommand(selectSql, connection, transaction))
                            {
                                selectCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                                selectCommand.Parameters.AddWithValue("@addOnsId", addOnDSOS.AddOnId);

                                int count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                                if (count == 0)
                                {
                                    // Insert new add-on into orderaddons
                                    string insertSql = @"INSERT INTO orderaddons (OrderId, AddOnsId, quantity, total)
                                                 VALUES (UNHEX(@orderId), @addOnsId, @quantity, @total)";

                                    using (var insertCommand = new MySqlCommand(insertSql, connection, transaction))
                                    {
                                        insertCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                                        insertCommand.Parameters.AddWithValue("@addOnsId", addOnDSOS.AddOnId);
                                        insertCommand.Parameters.AddWithValue("@quantity", request.Quantity);
                                        insertCommand.Parameters.AddWithValue("@total", total);

                                        _logger.LogInformation($"Inserting add-on '{request.AddOnName}' with quantity '{request.Quantity}', price '{addOnDSOS.PricePerUnit}', and total '{total}' into orderaddons");

                                        await insertCommand.ExecuteNonQueryAsync();
                                    }
                                }
                                else
                                {
                                    // Update existing add-on in orderaddons
                                    string updateSql = @"UPDATE orderaddons 
                                                 SET quantity = @quantity, total = @total 
                                                 WHERE OrderId = UNHEX(@orderId) AND AddOnsId = @addOnsId";

                                    using (var updateCommand = new MySqlCommand(updateSql, connection, transaction))
                                    {
                                        updateCommand.Parameters.AddWithValue("@quantity", request.Quantity);
                                        updateCommand.Parameters.AddWithValue("@total", total);
                                        updateCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                                        updateCommand.Parameters.AddWithValue("@addOnsId", addOnDSOS.AddOnId);

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

        private async Task<AddOnDSOS> GetAddOnByNameFromDatabase(string addOnName)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT addOnsId, name, price FROM addons WHERE name = @name";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@name", addOnName);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new AddOnDSOS
                            {
                                AddOnId = reader.GetInt32("addOnsId"),
                                AddOnName = reader.GetString("name"),
                                PricePerUnit = reader.GetDouble("price")
                            };
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }
        }



        [HttpPatch("customer/{orderId}/update-order-details")]
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


        [HttpPatch("admin/assign-employee")]
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

        [HttpDelete("customer/remove-cart/{orderId}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
        public async Task<IActionResult> RemoveCart(string orderId)
        {
            try
            {
                // Get the current user's username
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("No valid customer username found.");
                }

                // Convert the hex orderId to binary format
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Check if the order belongs to the current user
                bool isOrderOwnedByUser = await IsOrderOwnedByUser(customerUsername, orderIdBinary);
                if (!isOrderOwnedByUser)
                {
                    return Unauthorized("You do not have permission to delete this order.");
                }

                // Delete the order from the database
                bool deleteSuccess = await DeleteOrderByOrderId(orderIdBinary);
                if (deleteSuccess)
                {
                    return Ok("Order removed successfully.");
                }
                else
                {
                    return NotFound("Order not found.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cart");
                return StatusCode(500, $"An error occurred while processing the request.");
            }
        }

        private async Task<bool> IsOrderOwnedByUser(string customerUsername, string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT COUNT(*) 
            FROM orders 
            WHERE OrderId = UNHEX(@orderId) 
            AND CustomerId = (SELECT UserId FROM users WHERE Username = @customerUsername)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    command.Parameters.AddWithValue("@customerUsername", customerUsername);

                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        private async Task<bool> DeleteOrderByOrderId(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "DELETE FROM orders WHERE OrderId = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
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

        private async Task InsertOrder(Order order, byte[] designId, string flavor, string size, string pastryId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO orders (
            OrderId, CustomerId, CustomerName, EmployeeId, CreatedAt, Status, DesignId, price, quantity, 
            last_updated_by, last_updated_at, type, isActive, PickupDateTime, Description, Flavor, Size, DesignName, PastryId) 
            VALUES (
            UNHEX(REPLACE(UUID(), '-', '')), NULL, @CustomerName, NULL, NOW(), @status, @designId, @order_name, @price, 
            @quantity, NULL, NULL, @type, @isActive, @pickupDateTime, @Description, @Flavor, @Size, @DesignName, @PastryId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@CustomerName", order.customerName);
                    command.Parameters.AddWithValue("@designId", designId);
                    command.Parameters.AddWithValue("@status", order.status);
                    command.Parameters.AddWithValue("@price", order.price);
                    command.Parameters.AddWithValue("@quantity", order.quantity);
                    command.Parameters.AddWithValue("@type", order.type);
                    command.Parameters.AddWithValue("@isActive", order.isActive);
                    command.Parameters.AddWithValue("@pickupDateTime", order.PickupDateTime);
                    command.Parameters.AddWithValue("@Description", order.Description);
                    command.Parameters.AddWithValue("@Flavor", flavor);
                    command.Parameters.AddWithValue("@Size", size);
                    command.Parameters.AddWithValue("@DesignName", order.designName);
                    command.Parameters.AddWithValue("@PastryId", pastryId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }


    }
}
