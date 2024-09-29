﻿using BillOfMaterialsAPI.Helpers;// Adjust the namespace according to your project structure
using BillOfMaterialsAPI.Models;
using CRUDFI.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Claims;



namespace BOM_API_v2.KaizenFiles.Controllers
{
    public static class DateTimeExtensions
    {
        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }
    }

    [Route("orders")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<OrdersController> _logger;

        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;


        public OrdersController(IConfiguration configuration, ILogger<OrdersController> logger, DatabaseContext context, KaizenTables kaizenTables)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;

            _context = context;
            _kaizenTables = kaizenTables;


        }

        [HttpPost("/culo-api/v1/current-user/buy-now")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> BuyNow([FromBody] BuyNow buyNowRequest)
        {
            try
            {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                // Retrieve customerId from username
                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
                if (customerId == null || customerId.Length == 0)
                {
                    return BadRequest("Customer not found.");
                }
                string customer = await GetUserIdByAllUsernameString(customerUsername);

                // Validate and parse pickup date and time
                if (!DateTime.TryParseExact(buyNowRequest.PickupDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate) ||
                    !DateTime.TryParseExact(buyNowRequest.PickupTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                {
                    return BadRequest("Invalid pickup date or time format. Use 'yyyy-MM-dd' for date and 'h:mm tt' for time.");
                }
                DateTime pickupDateTime = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, parsedTime.Hour, parsedTime.Minute, 0);

                // Validate the order type
                if (!buyNowRequest.Type.Equals("normal", StringComparison.OrdinalIgnoreCase) &&
                    !buyNowRequest.Type.Equals("rush", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Invalid type. Please choose 'normal' or 'rush'.");
                }

                // List to collect suborderId responses
                var responses = new List<SuborderResponse>();

                // Create and save orders for each item in the orderItem list
                foreach (var orderItem in buyNowRequest.orderItem)
                {


                    string designIdHex = BitConverter.ToString(orderItem.DesignId).Replace("-", "").ToLower();

                    string designName = await getDesignName(designIdHex);
                    if (designIdHex == null || designIdHex.Length == 0)
                    {
                        return BadRequest($"Design '{orderItem.DesignId}' not found.");
                    }

                    string shape = await GetDesignShapes(designIdHex);

                    string subersId = await GetPastryMaterialIdByDesignIds(designIdHex);
                    string pastryMaterialId = await GetPastryMaterialIdBySubersIdAndSize(subersId, orderItem.Size);
                    string subVariantId = await GetPastryMaterialSubVariantId(subersId, orderItem.Size);
                    string pastryId = !string.IsNullOrEmpty(subVariantId) ? subVariantId : pastryMaterialId;

                    double TotalPrice = await PriceCalculator.CalculatePastryMaterialPrice(pastryId, _context, _kaizenTables);

                    Debug.WriteLine("Total Price is: " + TotalPrice);

                    // Retrieve list of add_ons_id from pastymaterialaddons table
                    var mainVariantAddOnsId = await GetsMainVariantAddOns(pastryId, orderItem.Size);

                    // Initialize a list to store add-on IDs
                    var addOnIds = new List<string>();

                    // Check if the main variant add-ons list is empty
                    if (mainVariantAddOnsId == null || mainVariantAddOnsId.Count == 0)
                    {
                        // If no main variant add-ons found, call GetsSubVariantAddOns instead
                        var subVariantAddOnsId = await GetsSubVariantAddOns(pastryId);

                        // Handle sub-variant add-ons as needed
                        if (subVariantAddOnsId != null && subVariantAddOnsId.Count > 0)
                        {
                            addOnIds.AddRange(subVariantAddOnsId.Select(id => id.ToString()));
                        }

                    }
                    else
                    {
                        // Add main variant add-ons to the list
                        addOnIds.AddRange(mainVariantAddOnsId.Select(id => id.ToString()));
                    }

                    // Generate new orderId for each item
                    Guid orderId = Guid.NewGuid();
                    string orderIdBinary = ConvertGuidToBinary16(orderId.ToString()).ToLower();

                    // Insert the new order into the 'orders' table
                    await InsertOrderWithOrderId(orderIdBinary, customerUsername, customerId, pickupDateTime, buyNowRequest.Type, buyNowRequest.Payment);

                    // Generate suborderId
                    Guid suborderId = Guid.NewGuid();
                    string suborderIdBinary = ConvertGuidToBinary16(suborderId.ToString()).ToLower();

                    // Create the order object
                    var order = new Order
                    {
                        suborderId = suborderId,
                        price = TotalPrice,
                        quantity = orderItem.Quantity,
                        designName = designName,
                        size = orderItem.Size,
                        flavor = orderItem.Flavor,
                        isActive = false,
                        customerName = customerUsername,
                        color = orderItem.Color,
                        shape = shape,
                        Description = orderItem.Description,
                        status = "to pay"
                    };

                    // Insert the order with the determined pastryId
                    await InsertsOrder(order, orderIdBinary, designIdHex, orderItem.Flavor, orderItem.Size, pastryId, customerId, orderItem.Color, shape, orderItem.Description);

                    string userId = ConvertGuidToBinary16(customer.ToString()).ToLower();

                    string message = ((order.customerName ?? "Unknown") + " " + "''" + (order.designName ?? "Design") + "''" + " has been added to your to pay");

                    await NotifyAsync(userId, message);

                    // Add suborderId and add-ons to the response list
                    responses.Add(new SuborderResponse
                    {
                        suborderId = suborderIdBinary,
                        pastryId = pastryId,
                        addonId = addOnIds
                    });
                }

                return Ok(responses); // Return the list of suborderIds and add-ons
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the request.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        private async Task<List<int>> GetsMainVariantAddOns(string pastryMaterialId, string size)
        {
            var addOnsIds = new List<int>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
    SELECT pma.add_ons_id
    FROM pastrymaterialaddons pma
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
                            addOnsIds.Add(addOnsId);
                        }
                    }
                }
            }

            return addOnsIds;
        }

        private async Task<List<int>> GetsSubVariantAddOns(string subVariantId)
        {
            var addOnsIds = new List<int>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
    SELECT add_ons_id
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
                            addOnsIds.Add(addOnsId);
                        }
                    }
                }
            }

            return addOnsIds;
        }


        private async Task InsertsOrder(Order order, string orderId, string designId, string flavor, string size, string pastryId, byte[] customerId, string color, string shape, string Description)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();


                string sql = @"INSERT INTO suborders (
            suborder_id, order_id, customer_id, customer_name, employee_id, created_at, status, design_id, price, quantity, 
            last_updated_by, last_updated_at, is_active, description, flavor, size, color, shape, design_name, pastry_id) 
            VALUES (UNHEX(REPLACE(UUID(), '-', '')), UNHEX(@orderid), @customerId, @CustomerName, NULL, NOW(), @status, UNHEX(@designId), @price, 
            @quantity, NULL, NOW(), @isActive, @Description, @Flavor, @Size, @color, @shape, @DesignName, @PastryId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderid", orderId);
                    command.Parameters.AddWithValue("@customerId", customerId);
                    command.Parameters.AddWithValue("@CustomerName", order.customerName);
                    command.Parameters.AddWithValue("@designId", designId);
                    command.Parameters.AddWithValue("@status", order.status);
                    command.Parameters.AddWithValue("@price", order.price);
                    command.Parameters.AddWithValue("@quantity", order.quantity);
                    command.Parameters.AddWithValue("@isActive", order.isActive);
                    command.Parameters.AddWithValue("@color", order.color);
                    command.Parameters.AddWithValue("@shape", shape);
                    command.Parameters.AddWithValue("@Description", order.Description);
                    command.Parameters.AddWithValue("@Flavor", flavor);
                    command.Parameters.AddWithValue("@Size", size);
                    command.Parameters.AddWithValue("@DesignName", order.designName);
                    command.Parameters.AddWithValue("@PastryId", pastryId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }



        [HttpPost("/culo-api/v1/current-user/custom-orders")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> CreateCustomOrder([FromBody] PostCustomOrder customOrder)
        {
            try
            {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
                if (customerId == null || customerId.Length == 0)
                {
                    return BadRequest("Customer not found");
                }

                string customer = await GetUserIdByAllUsernameString(customerUsername);

                if (!DateTime.TryParseExact(customOrder.PickupDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate) ||
                    !DateTime.TryParseExact(customOrder.PickupTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                {
                    return BadRequest("Invalid pickup date or time format. Use 'yyyy-MM-dd' for date and 'h:mm tt' for time.");
                }

                // Combine the parsed date and time into a single DateTime object
                DateTime pickupDateTime = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, parsedTime.Hour, parsedTime.Minute, 0);

                // Generate a new GUID for the OrderId
                Guid orderId = Guid.NewGuid();
                string orderIdBinary = ConvertGuidToBinary16(orderId.ToString()).ToLower();

                // Create the custom order object
                var order = new Custom
                {
                    quantity = customOrder.quantity,
                    size = customOrder.size,
                    flavor = customOrder.flavor,
                    customerName = customerUsername, // Set customer name from authenticated user
                    color = customOrder.color,
                    shape = customOrder.shape,
                    tier = customOrder.tier,
                    Description = customOrder.Description,
                    picture = customOrder.picture,
                    description = customOrder.Description,
                    message = customOrder.message,
                    cover = customOrder.cover,
                    type = customOrder.type
                };

                await InsertToOrderWithOrderId(orderIdBinary, customerUsername, customerId, pickupDateTime, customOrder.type);
                // Insert custom order into the database
                await InsertCustomOrder(customOrder.quantity, orderIdBinary, customerId, customerUsername, customOrder.picture, customOrder.Description, customOrder.message, customOrder.size, customOrder.tier, customOrder.cover, customOrder.color, customOrder.shape, customOrder.flavor);

                string userId = ConvertGuidToBinary16(customer.ToString()).ToLower();
                string message = ((order.customerName ?? "Unknown") + " " + "your order is added for approval");
                await NotifyAsync(userId, message);

                return Ok(); // Return 200 OK if the order is successfully created
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while creating the custom order");
                return StatusCode(500, "An error occurred while processing the request"); // Return 500 Internal Server Error
            }
        }

        private async Task<string> GetUserIdByAllUsernameString(string username)
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


        private async Task InsertCustomOrder(int quantity, string orderId, byte[] customerId, string customerName, string pictureUrl, string description, string message, string size, string tier, string cover, string color, string shape, string flavor)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO customorders ( quantity, order_id ,custom_id, customer_id, customer_name, picture_url, description, message, size, 
        `, cover, color, shape, flavor, design_name, design_id, price, created_at, status)
    VALUES ( @quantity, UNHEX(@orderid), UNHEX(REPLACE(UUID(), '-', '')), @CustomerId, @CustomerName, @PictureUrl, @Description, @Message, @Size, 
        @Tier, @Cover, @Color, @Shape, @Flavor, NULL, UNHEX(REPLACE(UUID(), '-', '')), NULL, NOW(), 'to review')";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@quantity", quantity);
                    command.Parameters.AddWithValue("@orderid", orderId);
                    command.Parameters.AddWithValue("@CustomerId", customerId);
                    command.Parameters.AddWithValue("@CustomerName", customerName);
                    command.Parameters.AddWithValue("@PictureUrl", pictureUrl);
                    command.Parameters.AddWithValue("@Description", description);
                    command.Parameters.AddWithValue("@Message", message);
                    command.Parameters.AddWithValue("@Size", size);
                    command.Parameters.AddWithValue("@Tier", tier);
                    command.Parameters.AddWithValue("@Cover", cover);
                    command.Parameters.AddWithValue("@Color", color);
                    command.Parameters.AddWithValue("@Shape", shape);
                    command.Parameters.AddWithValue("@Flavor", flavor);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task InsertToOrderWithOrderId(string orderIdBinary, string customerName, byte[] customerId, DateTime pickupDateTime, string type)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO orders (order_id, customer_id, customer_name, pickup_date, type, status, created_at, last_updated_at) 
                       VALUES (UNHEX(@orderid), @customerId, @CustomerName, @pickupDateTime, @type, 'to review', NOW(), NOW())";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderid", orderIdBinary);
                    command.Parameters.AddWithValue("@customerId", customerId);
                    command.Parameters.AddWithValue("@pickupDateTime", pickupDateTime);
                    command.Parameters.AddWithValue("@CustomerName", customerName);
                    command.Parameters.AddWithValue("@type", type);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        [HttpPost("/culo-api/v1/current-user/cart")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> CreateOrder([FromBody] OrderDTO orderDto)
        {
            try
            {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
                if (customerId == null || customerId.Length == 0)
                {
                    return BadRequest("Customer not found");
                }

                string customer = await GetUserIdByAllUsernameString(customerUsername);

                string designIdHex = BitConverter.ToString(orderDto.DesignId).Replace("-", "").ToLower();

                string designName = await getDesignName(designIdHex);
                if (designIdHex == null || designIdHex.Length == 0)
                {
                    return BadRequest($"Design '{orderDto.DesignId}' not found.");
                }

                string shape = await GetDesignShapes(designIdHex);

                // Get the pastry material ID using just the design ID
                string subersId = await GetPastryMaterialIdByDesignIds(designIdHex);
                string pastryMaterialId = await GetPastryMaterialIdBySubersIdAndSize(subersId, orderDto.Size);
                string subVariantId = await GetPastryMaterialSubVariantId(subersId, orderDto.Size);
                string pastryId = !string.IsNullOrEmpty(subVariantId) ? subVariantId : pastryMaterialId;

                double TotalPrice = await PriceCalculator.CalculatePastryMaterialPrice(pastryId, _context, _kaizenTables);

                Debug.WriteLine("Total Price is: " + TotalPrice);

                // List to collect suborderId responses
                var responses = new List<SuborderResponse>();

                // Retrieve list of add_ons_id from pastymaterialaddons table
                var mainVariantAddOnsId = await GetsMainVariantAddOns(pastryId, orderDto.Size);

                // Check if the main variant add-ons list is empty
                // Initialize a list to store add-on IDs
                var addOnIds = new List<string>();

                // Check if the main variant add-ons list is empty
                if (mainVariantAddOnsId == null || mainVariantAddOnsId.Count == 0)
                {
                    // If no main variant add-ons found, call GetsSubVariantAddOns instead
                    var subVariantAddOnsId = await GetsSubVariantAddOns(pastryId);

                    // Handle sub-variant add-ons as needed
                    if (subVariantAddOnsId != null && subVariantAddOnsId.Count > 0)
                    {
                        addOnIds.AddRange(subVariantAddOnsId.Select(id => id.ToString()));
                    }
                }
                else
                {
                    // Add main variant add-ons to the list
                    addOnIds.AddRange(mainVariantAddOnsId.Select(id => id.ToString()));
                }

                var order = new Order
                {
                    orderId = Guid.NewGuid(),
                    suborderId = Guid.NewGuid(),
                    quantity = orderDto.Quantity,
                    price = TotalPrice, // Use the calculated price from the response
                    designName = designName,
                    size = orderDto.Size,
                    flavor = orderDto.Flavor,
                    isActive = false,
                    customerName = customerUsername, // Set customer name from authenticated user
                    color = orderDto.Color,
                    shape = shape,
                    Description = orderDto.Description,
                    status = "cart"
                };

                string suborderIdBinary = ConvertGuidToBinary16(order.suborderId.ToString()).ToLower();

                // Insert the order into the database using the determined pastryId
                await InsertOrder(order, designIdHex, orderDto.Flavor, orderDto.Size, pastryId, customerId, orderDto.Color, shape, orderDto.Description);

                string userId = ConvertGuidToBinary16(customer.ToString()).ToLower();

                string message = ((order.customerName ?? "Unknown") + " " + "''" + (order.designName ?? "Design") + "''" + " has been added to your cart");

                await NotifyAsync(userId, message);

                responses.Add(new SuborderResponse
                {
                    suborderId = suborderIdBinary,
                    pastryId = pastryId,
                    addonId = addOnIds
                });

                return Ok(responses); // Return the list of suborderIds and add-ons
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while creating the order");
                return StatusCode(500, "An error occurred while processing the request"); // Return 500 Internal Server Error
            }
        }

        private async Task<string> GetDesignShapes(string design)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string designQuery = "SELECT shape_name FROM designshapes WHERE design_id = UNHEX(@design_id)";
                using (var designcommand = new MySqlCommand(designQuery, connection))
                {
                    designcommand.Parameters.AddWithValue("@design_id", design);
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

        private async Task<double> CalculateTotalPrice(OrderDTO orderDto, string pastryId)
        {

            double SubTotalprice = await STotal2(pastryId);

            Debug.WriteLine(SubTotalprice);

            string mainId = await STotal1(pastryId, orderDto.Size);

            double TotalPrice;

            if (SubTotalprice == 0)
            {
                double MainTotalprice = await Total(pastryId);
                Debug.WriteLine(MainTotalprice);

                TotalPrice = MainTotalprice * orderDto.Quantity;
            }
            else
            {
                double MainTotalprice = await Total(mainId);
                Debug.WriteLine(MainTotalprice);

                TotalPrice = (SubTotalprice + MainTotalprice) * orderDto.Quantity;
            }

            return TotalPrice; // Return the calculated total price
        }


        // Method to get the price of items by item_id
        private async Task<List<double>> Price(int itemId)
        {
            var prices = new List<double>();

            // Query to retrieve price for the given itemId
            string query = "SELECT price FROM item WHERE id = @itemId";

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@itemId", itemId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            prices.Add(reader.GetDouble("price"));
                        }
                    }
                }
            }

            return prices;
        }

        // Method to calculate the total by multiplying amount by price for each item
        private async Task<double> Total(string pastryId)
        {
            double totalSum = 0.0;  // Initialize a variable to keep track of the total sum

            // Query to retrieve item_id and amount where pastry_material_id = @pastryId
            string query = "SELECT item_id, amount FROM ingredients WHERE pastry_material_id = @pastryId";

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@pastryId", pastryId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int itemId = reader.GetInt32("item_id");
                            double amount = reader.GetDouble("amount");

                            // Get the prices for the given itemId by calling Price()
                            var prices = await Price(itemId);

                            // Multiply amount by each price and add the result to the total sum
                            foreach (var price in prices)
                            {
                                totalSum += amount * price;
                            }
                        }
                    }
                }
            }

            return totalSum;  // Return the total sum
        }

        // Method to get the pastry_material_sub_variant_id by pastryId and size
        private async Task<string> STotal1(string pastryId, string size)
        {
            string psubId = string.Empty;

            // Query to retrieve the pastry_material_sub_variant_id based on pastryId and size
            string query = "SELECT pastry_material_id FROM pastrymaterialsubvariants WHERE pastry_material_sub_variant_id = @pastryId AND sub_variant_name = @size";

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@pastryId", pastryId);
                    command.Parameters.AddWithValue("@size", size);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Retrieve the sub-variant ID as a string
                            psubId = reader.GetString("pastry_material_id");
                        }
                    }
                }
            }

            return psubId;  // Return the sub-variant ID
        }

        // Method to get item_id and amount from pastrymaterialsubvariantingredients based on the psubId
        private async Task<double> STotal2(string psubId)
        {
            double totalSum = 0.0;

            // Query to retrieve item_id and amount for the given pastry_material_sub_variant_id
            string query = "SELECT item_id, amount FROM pastrymaterialsubvariantingredients WHERE pastry_material_sub_variant_id = @psubId";

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@psubId", psubId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int itemId = reader.GetInt32("item_id");
                            double amount = reader.GetDouble("amount");

                            // Get the price for the given itemId by calling STotal3()
                            var prices = await STotal3(itemId);

                            // Multiply amount by each price and add to the total sum
                            foreach (var price in prices)
                            {
                                totalSum += amount * price;
                            }
                        }
                    }
                }
            }

            return totalSum;  // Return the total sum
        }

        // Method to get the price for the given itemId
        private async Task<List<double>> STotal3(int itemId)
        {
            var prices = new List<double>();

            // Query to retrieve price for the given item_id
            string query = "SELECT price FROM item WHERE Id = @itemId";

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@itemId", itemId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            prices.Add(reader.GetDouble("price"));
                        }
                    }
                }
            }

            return prices;  // Return the list of prices
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

        private async Task InsertOrder(Order order, string designId, string flavor, string size, string pastryId, byte[] customerId, string color, string shape, string Description)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();


                string sql = @"INSERT INTO suborders (
            suborder_id, order_id, customer_id, customer_name, employee_id, created_at, status, design_id, price, quantity, 
            last_updated_by, last_updated_at, is_active, description, flavor, size, color, shape, design_name, pastry_id) 
            VALUES (
            UNHEX(REPLACE(UUID(), '-', '')), NULL, @customerId, @CustomerName, NULL, NOW(), @status, UNHEX(@designId), @price, 
            @quantity, NULL, NOW(), @isActive, @Description, @Flavor, @Size, @color, @shape, @DesignName, @PastryId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customerId", customerId);
                    command.Parameters.AddWithValue("@CustomerName", order.customerName);
                    command.Parameters.AddWithValue("@designId", designId);
                    command.Parameters.AddWithValue("@status", order.status);
                    command.Parameters.AddWithValue("@price", order.price);
                    command.Parameters.AddWithValue("@quantity", order.quantity);
                    command.Parameters.AddWithValue("@isActive", order.isActive);
                    command.Parameters.AddWithValue("@color", order.color);
                    command.Parameters.AddWithValue("@shape", shape);
                    command.Parameters.AddWithValue("@Description", order.Description);
                    command.Parameters.AddWithValue("@Flavor", flavor);
                    command.Parameters.AddWithValue("@Size", size);
                    command.Parameters.AddWithValue("@DesignName", order.designName);
                    command.Parameters.AddWithValue("@PastryId", pastryId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }



        [HttpPost("/culo-api/v1/current-user/cart/checkout")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> PatchOrderTypeAndPickupDate([FromBody] CheckOutRequest checkOutRequest)
        {
            try
            {
                // Retrieve customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized.");
                }

                // Retrieve customerId from username
                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
                if (customerId == null || customerId.Length == 0)
                {
                    return BadRequest("Customer not found.");
                }

                string customer = await GetUserIdByAllUsernameString(customerUsername);

                // Validate type
                if (!checkOutRequest.Type.Equals("normal", StringComparison.OrdinalIgnoreCase) &&
                    !checkOutRequest.Type.Equals("rush", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Invalid type. Please choose 'normal' or 'rush'.");
                }

                // Parse and validate the pickup date and time
                if (!DateTime.TryParseExact(checkOutRequest.PickupDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate) ||
                    !DateTime.TryParseExact(checkOutRequest.PickupTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                {
                    return BadRequest("Invalid pickup date or time format. Use 'yyyy-MM-dd' for date and 'h:mm tt' for time.");
                }

                // Combine the parsed date and time into a single DateTime object
                DateTime pickupDateTime = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, parsedTime.Hour, parsedTime.Minute, 0);

                // Generate a new GUID for the OrderId (this order will be shared across multiple suborders)
                Guid orderId = Guid.NewGuid();
                string orderIdBinary = ConvertGuidToBinary16(orderId.ToString()).ToLower();

                // Insert a new order
                await InsertOrderWithOrderId(orderIdBinary, customerUsername, customerId, pickupDateTime, checkOutRequest.Type, checkOutRequest.Payment);

                // Loop through each suborderid in the request and update them with the new orderId
                foreach (var suborderId in checkOutRequest.SuborderIds)
                {
                    string suborderIdBinary = ConvertGuidToBinary16(suborderId.ToString()).ToLower();

                    // Check if the suborder exists in the suborders table
                    if (!await DoesSuborderExist(suborderIdBinary))
                    {
                        return NotFound($"Suborder with ID '{suborderId}' not found.");
                    }

                    // Update the suborder with the new orderId
                    await UpdateSuborderWithOrderId(suborderIdBinary, orderIdBinary);
                }

                string userId = ConvertGuidToBinary16(customer.ToString()).ToLower();

                string message = ((customerUsername ?? "Unknown") + " your cart has been added to your to pay");

                await NotifyAsync(userId, message);

                return Ok($"Order for {checkOutRequest.SuborderIds.Count} suborder(s) has been successfully created with order ID '{orderIdBinary}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the request.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }


        private async Task<bool> DoesSuborderExist(string suborderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT COUNT(1) FROM suborders WHERE suborder_id = UNHEX(@suborderid)";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborderid", suborderId);
                    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
                }
            }
        }

        private async Task UpdateSuborderWithOrderId(string suborderId, string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Prepare the SQL query to update the suborder
                string sql = "UPDATE suborders SET order_id = UNHEX(@orderid), status = 'to pay' WHERE suborder_id = UNHEX(@suborderid)";
                using (var command = new MySqlCommand(sql, connection))
                {
                    // Define and add the necessary parameters
                    command.Parameters.AddWithValue("@orderid", orderIdBinary);  // Use the provided orderIdBinary
                    command.Parameters.AddWithValue("@suborderid", suborderId);  // Pass the suborderId as hexadecimal

                    // Execute the query
                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        private async Task InsertOrderWithOrderId(string orderIdBinary, string customerName, byte[] customerId, DateTime pickupDateTime, string type, string payment)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO orders (order_id, customer_id, customer_name, pickup_date, type, payment, status, created_at, last_updated_at) 
                       VALUES (UNHEX(@orderid), @customerId, @CustomerName, @pickupDateTime, @type, @payment, 'to pay', NOW(), NOW())";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderid", orderIdBinary);
                    command.Parameters.AddWithValue("@customerId", customerId);
                    command.Parameters.AddWithValue("@pickupDateTime", pickupDateTime);
                    command.Parameters.AddWithValue("@CustomerName", customerName);
                    command.Parameters.AddWithValue("@type", type);
                    command.Parameters.AddWithValue("@payment", payment);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        [HttpPost("/culo-api/v1/current-user/{orderId}/confirm")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> ConfirmOrder(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
            {
                return BadRequest("OrderId cannot be null or empty.");
            }

            // Convert orderId to binary format for querying
            string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

            // Check if the order exists
            var orderExists = await CheckOrderExistx(orderIdBinary);

            if (!orderExists)
            {
                return NotFound("No orders found for the specified orderId.");
            }

            // Perform the update for confirmation
            await UpdateOrderxxStatus(orderIdBinary, "confirm");

            byte[] suborderId = await UpdateOrderxxxxStatus(orderIdBinary);

            if (suborderId == null)
            {
                return NotFound("No suborder ID found for the given order ID.");
            }

            await UpdateOrderxxxStatus(suborderId, "confirm");

            // Retrieve all employee IDs with Type 3 or 4
            List<string> users = await GetEmployeeAllId();

            foreach (var user in users)
            {
                // Get the employee name by the userId
                string AdminName = await GetAdminNameById(user);

                // Convert the userId to the binary form expected in the database
                string userIdBinary = ConvertGuidToBinary16(user).ToLower();

                // Construct the notification message
                string message = ((AdminName ?? "Unknown") + " new order that needed approval has been added");

                // Send notification to the user
                await NotifyAsync(userIdBinary, message);
            }

            return Ok("Order confirmed successfully.");
        }

        private async Task<string> GetAdminNameById(string empId)
        {
            string adminName = string.Empty;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();  // Open the connection once

                // SQL query to fetch DisplayName for the specified empId
                string sql = "SELECT DisplayName FROM users WHERE UserId = UNHEX(@id) AND Type IN (3, 4)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Ensure empId is a valid binary UUID string
                    command.Parameters.AddWithValue("@id", empId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Retrieve and return the DisplayName if found
                            adminName = reader.GetString("DisplayName");
                        }
                    }
                }
            }

            return adminName;  // Return the DisplayName (or empty string if not found)
        }


        [HttpPost("/culo-api/v1/current-user/{orderId}/cancel")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> CancelOrder(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
            {
                return BadRequest("OrderId cannot be null or empty.");
            }

            // Convert orderId to binary format for querying
            string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

            // Check if the order exists
            var orderExists = await CheckOrderExistx(orderIdBinary);

            if (!orderExists)
            {
                return NotFound("No orders found for the specified orderId.");
            }

            // Perform the update for cancellation
            await UpdateOrderxxStatus(orderIdBinary, "cancel");

            byte[] suborderId = await UpdateOrderxxxxStatus(orderIdBinary);

            if (suborderId == null)
            {
                return NotFound("No suborder ID found for the given order ID.");
            }

            await UpdateOrderxxxStatus(suborderId, "cancel");

            return Ok("Order canceled successfully.");
        }


        private async Task UpdateOrderxxStatus(string orderIdBinary, string action)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Determine the value of is_active based on the action
                int isActive = action.Equals("confirm", StringComparison.OrdinalIgnoreCase) ? 1 :
                               action.Equals("cancel", StringComparison.OrdinalIgnoreCase) ? 0 :
                               throw new ArgumentException("Invalid action. Please choose 'confirm' or 'cancel'.");

                string sql = "UPDATE orders SET is_active = @isActive, status = 'for approval' WHERE order_id = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@isActive", isActive);
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateOrderxxxStatus(byte[] orderIdBinary, string action)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Determine the value of is_active based on the action
                int isActive = action.Equals("confirm", StringComparison.OrdinalIgnoreCase) ? 1 :
                               action.Equals("cancel", StringComparison.OrdinalIgnoreCase) ? 0 :
                               throw new ArgumentException("Invalid action. Please choose 'confirm' or 'cancel'.");

                string sql = "UPDATE suborders SET is_active = @isActive, status = 'for approval' WHERE suborder_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@isActive", isActive);
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        private async Task<byte[]> UpdateOrderxxxxStatus(string orderIdBinary)
        {
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
                        if (await reader.ReadAsync())
                        {
                            // Return the binary value of suborder_id directly
                            byte[] suborderIdBytes = (byte[])reader["suborder_id"];

                            // Debug.WriteLine to display the value of suborderIdBytes
                            Debug.WriteLine($"Suborder ID bytes for order ID '{orderIdBinary}': {BitConverter.ToString(suborderIdBytes)}");

                            return suborderIdBytes;
                        }
                        else
                        {
                            // Return null or handle cases where no rows are found
                            return null;
                        }
                    }
                }
            }
        }



        private async Task<bool> CheckOrderExistx(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
        SELECT COUNT(1) 
        FROM orders 
        WHERE order_id = UNHEX(@orderIdBinary)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderIdBinary", orderIdBinary);
                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }


        [HttpPost("suborders/{suborderId}/add-ons")] //done
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> AddNewAddOnToOrder(string suborderId, [FromBody] AddNewAddOnRequest request)
        {
            try
            {
                _logger.LogInformation($"Starting AddNewAddOnToOrder for orderId: {suborderId}");

                // Convert orderId to binary(16) format without '0x' prefix
                string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

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
                                         WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnsId";

                            using (var selectCommand = new MySqlCommand(selectSql, connection, transaction))
                            {
                                selectCommand.Parameters.AddWithValue("@orderId", suborderIdBinary);
                                selectCommand.Parameters.AddWithValue("@addOnsId", addOnDSOS.AddOnId);

                                int count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                                if (count == 0)
                                {
                                    // Insert new add-on into orderaddons
                                    string insertSql = @"INSERT INTO orderaddons (order_id, add_ons_id, quantity, total, name, price)
                                                 VALUES (UNHEX(@orderId), @addOnsId, @quantity, @total, @name, @price)";

                                    using (var insertCommand = new MySqlCommand(insertSql, connection, transaction))
                                    {
                                        insertCommand.Parameters.AddWithValue("@orderId", suborderIdBinary);
                                        insertCommand.Parameters.AddWithValue("@addOnsId", addOnDSOS.AddOnId);
                                        insertCommand.Parameters.AddWithValue("@name", addOnDSOS.AddOnName);
                                        insertCommand.Parameters.AddWithValue("@price", addOnDSOS.PricePerUnit);
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
                                                 WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnsId";

                                    using (var updateCommand = new MySqlCommand(updateSql, connection, transaction))
                                    {
                                        updateCommand.Parameters.AddWithValue("@quantity", request.Quantity);
                                        updateCommand.Parameters.AddWithValue("@total", total);
                                        updateCommand.Parameters.AddWithValue("@orderId", suborderIdBinary);
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
                _logger.LogError(ex, $"Error adding or updating add-on to order with ID '{suborderId}'");
                return StatusCode(500, $"An error occurred while adding or updating add-on to order with ID '{suborderId}'.");
            }
        }

        private async Task<AddOnDSOS> GetAddOnByNameFromDatabase(string addOnName)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT add_ons_id, name, price FROM addons WHERE name = @name";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@name", addOnName);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new AddOnDSOS
                            {
                                AddOnId = reader.GetInt32("add_ons_id"),
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


        [HttpPost("suborders/{suborderId}/assign")]//done 
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AssignEmployeeToOrder(string suborderId, [FromBody] AssignEmp assign)
        {
            try
            {
                // Convert the orderId from GUID string to binary(16) format without '0x' prefix
                string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();
                string empdBinary = ConvertGuidToBinary16(assign.employeeId).ToLower();

                // Check if the order with the given ID exists
                bool orderExists = await CheckOrderExists(suborderIdBinary);
                if (!orderExists)
                {
                    return NotFound("Order does not exist. Please try another ID.");
                }

                // Check if the employee with the given username exists
                string employeeName = await GetEmployeeNameById(empdBinary);

                if (employeeName == null || employeeName.Length == 0)
                {
                    return NotFound($"Employee with username '{assign}' not found. Please try another name.");
                }

                // Update the order with the employee ID and employee name
                await UpdateOrderEmployeeId(suborderIdBinary, empdBinary, employeeName);

                await UpdateOrderStatusToBaking(suborderIdBinary);

                string userId = empdBinary;

                string message = ((employeeName ?? "Unknown") + " new order has been assigned to you");

                await NotifyAsync(userId, message);

                return Ok($"Employee with username '{assign}' has been successfully assigned to order with ID '{suborderId}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to assign employee to order with ID '{suborderId}'.");
                return StatusCode(500, $"An error occurred while processing the request to assign employee to order with ID '{suborderId}'.");
            }
        }

        private async Task<string> GetEmployeeNameById(string empId)
        {
            string EmpId = string.Empty;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();  // Open the connection only once

                string sql = "SELECT DisplayName FROM users WHERE UserId = UNHEX(@id) AND Type = 2";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", empId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Retrieve the DisplayName as a string
                            EmpId = reader.GetString("DisplayName");
                        }
                    }
                }
            }

            return EmpId;  // Return the DisplayName
        }


        [HttpDelete("suborders/{suborderId}/{addonId}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> DeleteOrderAddon(string suborderId, int addonId)
        {
            if (string.IsNullOrEmpty(suborderId))
            {
                return BadRequest("SuborderId cannot be null or empty.");
            }

            // Convert suborderId to binary format for querying
            string orderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "DELETE FROM orderaddons WHERE order_id = UNHEX(@suborderId) AND add_ons_id = @addonId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborderId", orderIdBinary);
                    command.Parameters.AddWithValue("@addonId", addonId);

                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        return Ok($"Addon with ID '{addonId}' successfully deleted from order '{suborderId}'.");
                    }
                    else
                    {
                        return NotFound($"No addon found with ID '{addonId}' for order '{suborderId}'.");
                    }
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

        [HttpGet("debug")]
        public async Task<IActionResult> GetAllOrders()
        {
            try
            {
                List<Order> orders = new List<Order>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "SELECT suborder_id, order_id, customer_id, employee_id, created_at, pastry_id, status, HEX(design_id) as design_id, design_name, price, quantity, last_updated_by, last_updated_at, is_active, description, flavor, size, customer_name, employee_name, shape, color FROM suborders";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                                ? (Guid?)null
                                                : new Guid((byte[])reader["order_id"]);

                                Guid suborderId = new Guid((byte[])reader["suborder_id"]);
                                Guid customerId = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                  ? Guid.Empty
                                                  : new Guid((byte[])reader["customer_id"]);

                                Guid employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                                  ? Guid.Empty
                                                  : new Guid((byte[])reader["employee_id"]);
                                string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                                   ? null
                                                   : reader.GetString(reader.GetOrdinal("employee_name"));
                                string? LastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                                orders.Add(new Order
                                {
                                    orderId = orderId, // Handle null values for orderId
                                    suborderId = suborderId,
                                    customerId = customerId,
                                    employeeId = employeeId,
                                    pastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    status = reader.GetString(reader.GetOrdinal("status")),
                                    color = reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.GetString(reader.GetOrdinal("shape")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                    designName = reader.GetString(reader.GetOrdinal("design_name")),
                                    price = reader.GetDouble(reader.GetOrdinal("price")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = LastUpdatedBy,
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                    ? (DateTime?)null
                                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    Description = reader.GetString(reader.GetOrdinal("description")),
                                    flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                    size = reader.GetString(reader.GetOrdinal("size")),
                                    customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                    employeeName = employeeName
                                });
                            }
                        }
                    }
                }

                // If no orders are found, return an empty list
                if (orders.Count == 0)
                    return Ok(new List<Order>());

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("/culo-api/v1/current-user/custom-orders")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetAllCustomInitialOrdersByCustomerIds([FromQuery] string? search = null)
        {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(customerUsername))
            {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            string Id = await GetAdminIdByUsername(customerUsername);

            string customerId = Id.ToLower();

            if (customerId == null || customerId.Length == 0)
            {
                return BadRequest("Customer not found");
            }

            try
            {
                List<CustomPartial> orders;

                if (!string.IsNullOrEmpty(search))
                {
                    // Check if search matches a valid status
                    if (search.Equals("to review", StringComparison.OrdinalIgnoreCase))
                    {
                        // Fetch by status if valid search value is provided
                        orders = await FetchByStatusCustomInitialOrdersAsync(customerId,search);

                    }
                    else if (await IsEmployeeNameExistsAsync(search))
                    {
                        // Fetch by employee name if it exists in the database
                        orders = await FetchCustomOrdersByEmployeeInitialOrdersAsync(customerId,search);

                    }
                    else
                    {
                        // If search does not match any valid status or employee name
                        return NotFound($"{search} not found");
                    }
                }
                else
                {
                    // Fetch all if no search value is provided
                    orders = await FetchInitialCustomOrdersAsync(customerId);
                }

                // If no orders are found, return an empty list
                if (orders.Count == 0)
                    return Ok(new List<toPayInitial>());

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        private async Task<string> GetAdminIdByUsername(string username)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Username = @username AND Type IN (3, 4)";

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

        private async Task<List<CustomPartial>> FetchByStatusCustomInitialOrdersAsync(string id, string status)
        {
            List<CustomPartial> orders = new List<CustomPartial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
        SELECT 
            custom_id, order_id, customer_id, created_at, status, tier, shape, size, price, color, cover, picture_url, description, message, flavor, 
            design_id, design_name, quantity, customer_name
        FROM customorders 
        WHERE status = @status AND customer_id = @id";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add the status parameter to the SQL command
                    command.Parameters.AddWithValue("@status", status);
                    command.Parameters.AddWithValue("@id", id);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                ? (Guid?)null
                : new Guid((byte[])reader["order_id"]);

                            Guid suborderId = new Guid((byte[])reader["custom_id"]);
                            Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                    ? Guid.Empty
                                                    : new Guid((byte[])reader["customer_id"]);
                            Guid designId = new Guid((byte[])reader["design_id"]);
                            double? Price = reader.IsDBNull(reader.GetOrdinal("price")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("price"));

                            orders.Add(new CustomPartial
                            {
                                orderId = orderId, // Handle null values for orderId
                                customId = suborderId,
                                CustomerId = customerIdFromDb,
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                designId = designId,
                                Price = Price,
                                Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                color = reader.IsDBNull(reader.GetOrdinal("color")) ? string.Empty : reader.GetString(reader.GetOrdinal("color")),
                                designName = reader.IsDBNull(reader.GetOrdinal("design_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("design_name")),
                                shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? string.Empty : reader.GetString(reader.GetOrdinal("shape")),
                                tier = reader.IsDBNull(reader.GetOrdinal("tier")) ? string.Empty : reader.GetString(reader.GetOrdinal("tier")),
                                cover = reader.IsDBNull(reader.GetOrdinal("cover")) ? string.Empty : reader.GetString(reader.GetOrdinal("cover")),
                                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                                size = reader.IsDBNull(reader.GetOrdinal("size")) ? string.Empty : reader.GetString(reader.GetOrdinal("size")),
                                flavor = reader.IsDBNull(reader.GetOrdinal("flavor")) ? string.Empty : reader.GetString(reader.GetOrdinal("flavor")),
                                picture = reader.IsDBNull(reader.GetOrdinal("picture_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("picture_url")),
                                message = reader.IsDBNull(reader.GetOrdinal("message")) ? string.Empty : reader.GetString(reader.GetOrdinal("message"))
                            });

                        }
                    }
                }
            }

            return orders; // Return the list of toPayInitial records
        }

        private async Task<List<CustomPartial>> FetchCustomOrdersByEmployeeInitialOrdersAsync(string id, string name)
        {
            List<CustomPartial> orders = new List<CustomPartial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
     SELECT 
            custom_id, order_id, customer_id, created_at, status, tier, shape, size, price, color, cover, picture_url, description, message, flavor, 
            design_id, design_name, quantity, customer_name
        FROM customorders 
        WHERE employee_name = @name AND customer_id = @id";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add the status parameter to the SQL command
                    command.Parameters.AddWithValue("@name", name);
                    command.Parameters.AddWithValue("@id", id);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                ? (Guid?)null
                : new Guid((byte[])reader["order_id"]);

                            Guid suborderId = new Guid((byte[])reader["custom_id"]);
                            Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                    ? Guid.Empty
                                                    : new Guid((byte[])reader["customer_id"]);
                            Guid designId = new Guid((byte[])reader["design_id"]);

                            orders.Add(new CustomPartial
                            {
                                orderId = orderId, // Handle null values for orderId
                                customId = suborderId,
                                CustomerId = customerIdFromDb,
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                designId = designId,
                                Price = reader.GetDouble(reader.GetOrdinal("price")),
                                Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                designName = reader.IsDBNull(reader.GetOrdinal("design_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("design_name")),
                                color = reader.IsDBNull(reader.GetOrdinal("color")) ? string.Empty : reader.GetString(reader.GetOrdinal("color")),
                                shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? string.Empty : reader.GetString(reader.GetOrdinal("shape")),
                                tier = reader.IsDBNull(reader.GetOrdinal("tier")) ? string.Empty : reader.GetString(reader.GetOrdinal("tier")),
                                cover = reader.IsDBNull(reader.GetOrdinal("cover")) ? string.Empty : reader.GetString(reader.GetOrdinal("cover")),
                                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                                size = reader.IsDBNull(reader.GetOrdinal("size")) ? string.Empty : reader.GetString(reader.GetOrdinal("size")),
                                flavor = reader.IsDBNull(reader.GetOrdinal("flavor")) ? string.Empty : reader.GetString(reader.GetOrdinal("flavor")),
                                picture = reader.IsDBNull(reader.GetOrdinal("picture_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("picture_url")),
                                message = reader.IsDBNull(reader.GetOrdinal("message")) ? string.Empty : reader.GetString(reader.GetOrdinal("message"))
                            });
                        }
                    }
                }
            }

            return orders;
        }

        private async Task<List<CustomPartial>> FetchInitialCustomOrdersAsync(string id)
        {
            List<CustomPartial> orders = new List<CustomPartial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"SELECT custom_id, order_id, customer_id, created_at, status, tier, shape, size, price, color, cover, picture_url, description, message, flavor, 
            design_id, design_name, quantity, customer_name, employee_id, employee_name
        FROM customorders
WHERE customer_id = @id";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", id);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                ? (Guid?)null
                : new Guid((byte[])reader["order_id"]);

                            Guid suborderId = new Guid((byte[])reader["custom_id"]);
                            Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                    ? Guid.Empty
                                                    : new Guid((byte[])reader["customer_id"]);
                            Guid employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                                  ? Guid.Empty
                                                  : new Guid((byte[])reader["employee_id"]);
                            string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                               ? null
                                               : reader.GetString(reader.GetOrdinal("employee_name"));
                            Guid designId = new Guid((byte[])reader["design_id"]);
                            double? Price = reader.IsDBNull(reader.GetOrdinal("price")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("price"));

                            orders.Add(new CustomPartial
                            {
                                orderId = orderId, // Handle null values for orderId
                                customId = suborderId,
                                CustomerId = customerIdFromDb,
                                employeeId = employeeId,
                                employeeName = employeeName,
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                designId = designId,
                                Price = Price,
                                Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                designName = reader.IsDBNull(reader.GetOrdinal("design_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("design_name")),
                                CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                color = reader.IsDBNull(reader.GetOrdinal("color")) ? string.Empty : reader.GetString(reader.GetOrdinal("color")),
                                shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? string.Empty : reader.GetString(reader.GetOrdinal("shape")),
                                tier = reader.IsDBNull(reader.GetOrdinal("tier")) ? string.Empty : reader.GetString(reader.GetOrdinal("tier")),
                                cover = reader.IsDBNull(reader.GetOrdinal("cover")) ? string.Empty : reader.GetString(reader.GetOrdinal("cover")),
                                Description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                                size = reader.IsDBNull(reader.GetOrdinal("size")) ? string.Empty : reader.GetString(reader.GetOrdinal("size")),
                                flavor = reader.IsDBNull(reader.GetOrdinal("flavor")) ? string.Empty : reader.GetString(reader.GetOrdinal("flavor")),
                                picture = reader.IsDBNull(reader.GetOrdinal("picture_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("picture_url")),
                                message = reader.IsDBNull(reader.GetOrdinal("message")) ? string.Empty : reader.GetString(reader.GetOrdinal("message"))
                            });
                        }
                    }
                }
            }

            return orders;
        }

        [HttpGet("custom-orders/{customid}/full")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetAllCustomOrdersByCustomerId(string customid)
        {
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

            // Convert suborderId to binary format
            string customidBinary = ConvertGuidToBinary16((customid)).ToLower();

            // Check if the suborder exists in the suborders table
            if (!await DoesCustomOrderExist(customidBinary))
            {
                return NotFound($"Suborder with ID '{customidBinary}' not found.");
            }
            Debug.Write(customidBinary);

            try
            {
                List<CustomOrderFull> orders = new List<CustomOrderFull>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
                SELECT custom_id, order_id, customer_id, created_at, status, tier, shape, size, price, color, cover, picture_url, description, message, flavor, 
            design_id, design_name, quantity, customer_name, employee_id, employee_name
        FROM customorders 
            WHERE custom_id = UNHEX(@suborderIdBinary)";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@suborderIdBinary", customidBinary);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                    ? (Guid?)null
                    : new Guid((byte[])reader["order_id"]);

                                Guid suborderId = new Guid((byte[])reader["custom_id"]);
                                Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                        ? Guid.Empty
                                                        : new Guid((byte[])reader["customer_id"]);
                                Guid employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                                      ? Guid.Empty
                                                      : new Guid((byte[])reader["employee_id"]);
                                string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                                   ? null
                                                   : reader.GetString(reader.GetOrdinal("employee_name"));
                                double? Price = reader.IsDBNull(reader.GetOrdinal("price")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("price"));
                                Guid designId = new Guid((byte[])reader["design_id"]);
                                orders.Add(new CustomOrderFull
                                {
                                    orderId = orderId, // Handle null values for orderId
                                    customId = suborderId,
                                    CustomerId = customerIdFromDb,
                                    employeeId = employeeId,
                                    employeeName = employeeName,
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    designId = designId,
                                    Price = Price,
                                    Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    designName = reader.IsDBNull(reader.GetOrdinal("design_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("design_name")),
                                    CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                    color = reader.IsDBNull(reader.GetOrdinal("color")) ? string.Empty : reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? string.Empty : reader.GetString(reader.GetOrdinal("shape")),
                                    tier = reader.IsDBNull(reader.GetOrdinal("tier")) ? string.Empty : reader.GetString(reader.GetOrdinal("tier")),
                                    cover = reader.IsDBNull(reader.GetOrdinal("cover")) ? string.Empty : reader.GetString(reader.GetOrdinal("cover")),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                                    size = reader.IsDBNull(reader.GetOrdinal("size")) ? string.Empty : reader.GetString(reader.GetOrdinal("size")),
                                    flavor = reader.IsDBNull(reader.GetOrdinal("flavor")) ? string.Empty : reader.GetString(reader.GetOrdinal("flavor")),
                                    picture = reader.IsDBNull(reader.GetOrdinal("picture_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("picture_url")),
                                    message = reader.IsDBNull(reader.GetOrdinal("message")) ? string.Empty : reader.GetString(reader.GetOrdinal("message")),
                                });
                            }
                        }
                    }

                    if (orders.Count > 0)
                    {
                        // Scan the orders table using the retrieved orderId from the suborders table
                        foreach (var order in orders)
                        {
                            if (order.orderId.HasValue)
                            {
                                // Convert orderId to binary format
                                string orderIdBinary = ConvertGuidToBinary16(order.orderId.Value.ToString()).ToLower();
                                Debug.Write(orderIdBinary);

                                var orderDetails = await GetOrderDetailsByOrderId(connection, orderIdBinary);
                                if (orderDetails != null)
                                {
                                    // Populate additional fields from the orders table
                                    order.payment = orderDetails.payment;
                                    order.PickupDateTime = orderDetails.PickupDateTime;
                                }
                            }
                        }
                    }
                }


                // Return the orders list
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        private async Task<bool> DoesCustomOrderExist(string CustomorderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT COUNT(1) FROM customorders WHERE custom_id = UNHEX(@suborderid)";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborderid", CustomorderId);
                    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
                }
            }
        }

        [HttpGet("partial-details")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetAllInitialOrdersByCustomerIds([FromQuery] string? search = null)
        {
            try
            {
                List<AdminInitial> orders;

                if (!string.IsNullOrEmpty(search))
                {
                    // Check if search matches a valid status
                    if (search.Equals("assigning artist", StringComparison.OrdinalIgnoreCase) ||
                        search.Equals("done", StringComparison.OrdinalIgnoreCase) ||
                        search.Equals("for pick up", StringComparison.OrdinalIgnoreCase) ||
                        search.Equals("for approval", StringComparison.OrdinalIgnoreCase) ||
                        search.Equals("baking", StringComparison.OrdinalIgnoreCase))
                    {
                        // Fetch by status if valid search value is provided
                        orders = await FetchByStatusInitialOrdersAsync(search);


                    }
                    else if (await IsEmployeeNameExistsAsync(search))
                    {
                        // Fetch by employee name if it exists in the database
                        orders = await FetchByEmployeeInitialOrdersAsync(search);

                    }
                    else
                    {
                        // If search does not match any valid status or employee name
                        return NotFound($"{search} not found");
                    }
                }
                else
                {
                    // Fetch all if no search value is provided
                    orders = await FetchInitialOrdersAsync();

                }

                // If no orders are found, return an empty list
                if (orders.Count == 0)
                    return Ok(new List<toPayInitial>());

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        
        private async Task<List<AdminInitial>> FetchInitialOrdersAsync()
        {
            List<AdminInitial> orders = new List<AdminInitial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @" SELECT order_id, customer_id, type, created_at, status, payment, pickup_date, last_updated_by, last_updated_at, is_active, customer_name 
                        FROM orders WHERE status IN('baking', 'to review', 'for update', 'assigning artist', 'done')";

                using (var command = new MySqlCommand(sql, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                            ? (Guid?)null
                                            : new Guid((byte[])reader["order_id"]);

                            Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                    ? Guid.Empty
                                                    : new Guid((byte[])reader["customer_id"]);

                            string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                            // Initialize an AdminInitial object with order details
                            AdminInitial order = new AdminInitial
                            {
                                Id = orderId,
                                CustomerId = customerIdFromDb,
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                lastUpdatedBy = lastUpdatedBy ?? "",
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                ? (DateTime?)null
                                                : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                Payment = reader.IsDBNull(reader.GetOrdinal("payment")) ? (string) null :reader.GetString(reader.GetOrdinal("payment")),
                                Type = reader.GetString(reader.GetOrdinal("type")),
                                Pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                Status = reader.GetString(reader.GetOrdinal("status")),
                            };

                            // If the order ID is valid, fetch design details and total price
                            if (order.Id.HasValue)
                            {
                                // Convert order ID to string and pass to FetchDesignAndTotalPriceAsync
                                string orderIdString = BitConverter.ToString(order.Id.Value.ToByteArray()).Replace("-", "").ToLower();
                                List<AdminInitial> designDetails = await FetchDesignAndTotalAsync(orderIdString);
                                List<AdminInitial> customdetails = await FetchCustomOrderIdAsync(orderIdString);

                                if (customdetails.Any())
                                {
                                    order.customId = customdetails.First().customId;
                                }

                                // Append design details (like DesignName and Total) to the order
                                if (designDetails.Any())
                                {
                                    order.DesignId = designDetails.First().DesignId;
                                    order.DesignName = designDetails.First().DesignName;
                                }
                            }

                            orders.Add(order); // Add the order with design details to the list
                        }
                    }
                }
            }

            return orders;
        }

        private async Task<List<AdminInitial>> FetchCustomOrderIdAsync(string orderId)
        {
            List<AdminInitial> orders = new List<AdminInitial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
SELECT 
    custom_id FROM customorders WHERE order_id = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add the status parameter to the SQL command
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {

                            orders.Add(new AdminInitial
                            {
                                customId = reader.IsDBNull(reader.GetOrdinal("custom_id"))
                                            ? (Guid?)null
                                            : new Guid((byte[])reader["custom_id"])
                        });
                        }
                    }
                }
            }

            return orders;
        }


        private async Task<List<AdminInitial>> FetchDesignAndTotalAsync(string orderId)
        {
            List<AdminInitial> orders = new List<AdminInitial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
SELECT 
    HEX(design_id) as design_id, design_name 
FROM suborders WHERE order_id = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add the status parameter to the SQL command
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {

                            orders.Add(new AdminInitial
                            {
                                DesignId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                            });
                        }
                    }
                }
            }

            return orders;
        }

        private async Task<List<AdminInitial>> FetchByStatusInitialOrdersAsync(string status)
        {
            List<AdminInitial> orders = new List<AdminInitial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
        SELECT o.order_id, o.customer_id, o.type, o.created_at, o.status, o.payment, o.pickup_date, 
               o.last_updated_by, o.last_updated_at, o.is_active, o.customer_name 
        FROM orders o
        INNER JOIN suborders s ON o.order_id = s.order_id 
        WHERE s.status = @status AND o.status IN('baking', 'to review', 'for update', 'assigning artist', 'done')";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@status", status);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                            ? (Guid?)null
                                            : new Guid((byte[])reader["order_id"]);

                            Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                    ? Guid.Empty
                                                    : new Guid((byte[])reader["customer_id"]);

                            string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                            // Initialize an AdminInitial object with order details
                            AdminInitial order = new AdminInitial
                            {
                                Id = orderId,
                                CustomerId = customerIdFromDb,
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                lastUpdatedBy = lastUpdatedBy ?? "",
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                ? (DateTime?)null
                                                : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                Payment = reader.IsDBNull(reader.GetOrdinal("payment")) ? (string)null : reader.GetString(reader.GetOrdinal("payment")),
                                Type = reader.GetString(reader.GetOrdinal("type")),
                                Pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                Status = reader.GetString(reader.GetOrdinal("status")),
                            };

                            // If the order ID is valid, fetch design details and total price
                            if (order.Id.HasValue)
                            {
                                // Convert order ID to string and pass to FetchDesignAndTotalPriceAsync
                                string orderIdString = BitConverter.ToString(order.Id.Value.ToByteArray()).Replace("-", "").ToLower();
                                List<AdminInitial> designDetails = await FetchDesignAndTotalAsync(orderIdString);

                                // Append design details (like DesignName and Total) to the order
                                if (designDetails.Any())
                                {
                                    order.DesignId = designDetails.First().DesignId;
                                    order.DesignName = designDetails.First().DesignName;
                                }
                            }

                            orders.Add(order); // Add the order with design details to the list
                        }
                    }
                }
            }

            return orders;
        }


        private async Task<List<AdminInitial>> FetchByEmployeeInitialOrdersAsync(string name)
        {
            List<AdminInitial> orders = new List<AdminInitial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
        SELECT o.order_id, o.customer_id, o.type, o.created_at, o.status, o.payment, o.pickup_date, 
               o.last_updated_by, o.last_updated_at, o.is_active, o.customer_name 
        FROM orders o
        INNER JOIN suborders s ON o.order_id = s.order_id 
        WHERE s.employee_name = @name AND o.status IN('baking', 'to review', 'for update', 'assigning artist', 'done')";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@name", name);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                            ? (Guid?)null
                                            : new Guid((byte[])reader["order_id"]);

                            Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                    ? Guid.Empty
                                                    : new Guid((byte[])reader["customer_id"]);

                            string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                            // Initialize an AdminInitial object with order details
                            AdminInitial order = new AdminInitial
                            {
                                Id = orderId,
                                CustomerId = customerIdFromDb,
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                lastUpdatedBy = lastUpdatedBy ?? "",
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                ? (DateTime?)null
                                                : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                Payment = reader.IsDBNull(reader.GetOrdinal("payment")) ? (string)null : reader.GetString(reader.GetOrdinal("payment")),
                                Type = reader.GetString(reader.GetOrdinal("type")),
                                Pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                Status = reader.GetString(reader.GetOrdinal("status")),
                            };

                            // If the order ID is valid, fetch design details and total price
                            if (order.Id.HasValue)
                            {
                                // Convert order ID to string and pass to FetchDesignAndTotalPriceAsync
                                string orderIdString = BitConverter.ToString(order.Id.Value.ToByteArray()).Replace("-", "").ToLower();
                                List<AdminInitial> designDetails = await FetchDesignAndTotalAsync(orderIdString);

                                // Append design details (like DesignName and Total) to the order
                                if (designDetails.Any())
                                {
                                    order.DesignId = designDetails.First().DesignId;
                                    order.DesignName = designDetails.First().DesignName;
                                }
                            }

                            orders.Add(order); // Add the order with design details to the list
                        }
                    }
                }
            }

            return orders;
        }

        private async Task<bool> IsEmployeeNameExistsAsync(string name)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"SELECT COUNT(*) FROM suborders WHERE employee_name = @name";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@name", name);

                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }


        [HttpGet("{orderId}/full")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetFullOrderDetailsByAdmin(string orderId)
        {
            // Convert the orderId to binary
            string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

            try
            {
                // Check if the order is a custom order
                bool isCustomOrder = await IsCustomOrderAsync(orderIdBinary);

                if (!isCustomOrder)
                {
                    // Standard order flow
                    var orderDetails = await GetOrderDetailx(orderIdBinary);

                    if (orderDetails != null)
                    {
                        // Retrieve suborders
                        orderDetails.OrderItems = await GetSuborderDetails(orderIdBinary);

                        // Initialize total sum
                        double totalSum = 0;

                        foreach (var suborder in orderDetails.OrderItems)
                        {
                            // Retrieve add-ons for each suborder
                            suborder.OrderAddons = await GetOrderAddonsDetails(suborder.SuborderId);

                            // Calculate the total for this suborder
                            double addOnsTotal = suborder.OrderAddons.Sum(addon => addon.AddOnTotal);
                            suborder.SubOrderTotal = (suborder.Price * suborder.Quantity) + addOnsTotal;

                            // Add to the overall total
                            totalSum += suborder.SubOrderTotal;
                        }

                        // Set the total in CheckOutDetails
                        orderDetails.OrderTotal = totalSum;
                    }

                    return Ok(orderDetails);
                }
                else
                {
                    // Custom order flow
                    List<CustomOrderFull> orders = new List<CustomOrderFull>();

                    using (var connection = new MySqlConnection(connectionstring))
                    {
                        await connection.OpenAsync();

                        string sql = @"
                    SELECT custom_id, order_id, customer_id, created_at, status, tier, shape, size, price, color, cover, 
                        picture_url, description, message, flavor, design_id, design_name, quantity, customer_name, 
                        employee_id, employee_name
                    FROM customorders 
                    WHERE order_id = UNHEX(@suborderIdBinary)";

                        using (var command = new MySqlCommand(sql, connection))
                        {
                            command.Parameters.AddWithValue("@suborderIdBinary", orderIdBinary);

                            using (var reader = await command.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    Guid? orderIdFromDb = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                        ? (Guid?)null
                                        : new Guid((byte[])reader["order_id"]);

                                    Guid suborderId = new Guid((byte[])reader["custom_id"]);
                                    Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                        ? Guid.Empty
                                        : new Guid((byte[])reader["customer_id"]);

                                    Guid employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                        ? Guid.Empty
                                        : new Guid((byte[])reader["employee_id"]);

                                    string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                        ? null
                                        : reader.GetString(reader.GetOrdinal("employee_name"));

                                    double? price = reader.IsDBNull(reader.GetOrdinal("price"))
                                        ? (double?)null
                                        : reader.GetDouble(reader.GetOrdinal("price"));

                                    Guid designId = new Guid((byte[])reader["design_id"]);

                                    orders.Add(new CustomOrderFull
                                    {
                                        orderId = orderIdFromDb,
                                        customId = suborderId,
                                        CustomerId = customerIdFromDb,
                                        employeeId = employeeId,
                                        employeeName = employeeName,
                                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                        designId = designId,
                                        Price = price,
                                        Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                        designName = reader.IsDBNull(reader.GetOrdinal("design_name"))
                                            ? string.Empty
                                            : reader.GetString(reader.GetOrdinal("design_name")),
                                        CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                        color = reader.IsDBNull(reader.GetOrdinal("color")) ? string.Empty : reader.GetString(reader.GetOrdinal("color")),
                                        shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? string.Empty : reader.GetString(reader.GetOrdinal("shape")),
                                        tier = reader.IsDBNull(reader.GetOrdinal("tier")) ? string.Empty : reader.GetString(reader.GetOrdinal("tier")),
                                        cover = reader.IsDBNull(reader.GetOrdinal("cover")) ? string.Empty : reader.GetString(reader.GetOrdinal("cover")),
                                        Description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                                        size = reader.IsDBNull(reader.GetOrdinal("size")) ? string.Empty : reader.GetString(reader.GetOrdinal("size")),
                                        flavor = reader.IsDBNull(reader.GetOrdinal("flavor")) ? string.Empty : reader.GetString(reader.GetOrdinal("flavor")),
                                        picture = reader.IsDBNull(reader.GetOrdinal("picture_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("picture_url")),
                                        message = reader.IsDBNull(reader.GetOrdinal("message")) ? string.Empty : reader.GetString(reader.GetOrdinal("message")),
                                    });
                                }
                            }
                        }

                        if (orders.Count > 0)
                        {
                            // Process additional details for each custom order
                            foreach (var order in orders)
                            {
                                if (order.orderId.HasValue)
                                {
                                    // Convert orderId to binary format
                                    string orderIdBinaryNew = ConvertGuidToBinary16(order.orderId.Value.ToString()).ToLower();

                                    var orderDetails = await GetOrderDetailsByOrderId(connection, orderIdBinaryNew);
                                    if (orderDetails != null)
                                    {
                                        // Populate additional fields from the orders table
                                        order.payment = orderDetails.payment;
                                        order.PickupDateTime = orderDetails.PickupDateTime;
                                    }
                                }
                            }
                        }
                    }

                    return Ok(orders);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        private async Task<bool> IsCustomOrderAsync(string orderBinary)
        {
            bool isCustomOrder = false;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"SELECT COUNT(1) 
                       FROM customorders 
                       WHERE order_id = UNHEX(@orderBinary)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add the parameter for the query
                    command.Parameters.AddWithValue("@orderBinary", orderBinary);

                    // Execute the query and check if any row exists
                    var result = await command.ExecuteScalarAsync();

                    // If the result is greater than 0, the order exists in the suborders table
                    isCustomOrder = Convert.ToInt32(result) > 0;
                }
            }

            return isCustomOrder;
        }


        [HttpGet("/culo-api/v1/current-user/cart/")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetCartOrdersByCustomerId()
        {
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

            try
            {
                List<Cart> orders = new List<Cart>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
    SELECT 
        suborder_id, order_id, customer_id, employee_id, created_at, status, 
        HEX(design_id) as design_id, design_name, price, quantity, 
        last_updated_by, last_updated_at, is_active, description, 
        flavor, size, customer_name, employee_name, shape, color, pastry_id 
    FROM suborders 
    WHERE customer_id = @customerId AND status IN('cart')";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerId", customerId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                                ? (Guid?)null
                                                : new Guid((byte[])reader["order_id"]);

                                Guid suborderId = new Guid((byte[])reader["suborder_id"]);
                                Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                        ? Guid.Empty
                                                        : new Guid((byte[])reader["customer_id"]);

                                Guid employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                                  ? Guid.Empty
                                                  : new Guid((byte[])reader["employee_id"]);

                                string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                                   ? null
                                                   : reader.GetString(reader.GetOrdinal("employee_name"));

                                string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                                double ingredientPrice = reader.GetDouble(reader.GetOrdinal("price"));

                                // Calculate addonPrice using the private async method
                                string orderIdBinary = BitConverter.ToString((byte[])reader["order_id"]).Replace("-", "").ToLower();
                                double addonPrice = await GetAddonPriceAsync(orderIdBinary);

                                // Calculate final price
                                double finalPrice = ingredientPrice + addonPrice;

                                orders.Add(new Cart
                                {
                                    Id = orderId, // Handle null values for orderId
                                    suborderId = suborderId,
                                    CustomerId = customerIdFromDb,
                                    employeeId = employeeId,
                                    pastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    Status = reader.GetString(reader.GetOrdinal("status")),
                                    color = reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.GetString(reader.GetOrdinal("shape")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                    DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                                    Price = finalPrice, // Use finalPrice instead of raw price
                                    Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = lastUpdatedBy,
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                    ? (DateTime?)null
                                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    Description = reader.GetString(reader.GetOrdinal("description")),
                                    Flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                    Size = reader.GetString(reader.GetOrdinal("size")),
                                    CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                    employeeName = employeeName
                                });
                            }
                        }
                    }
                }

                // If no orders are found, return an empty list
                if (orders.Count == 0)
                    return Ok(new List<Order>());

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        // Private async method to calculate addon price for the order
        private async Task<double> GetAddonPriceAsync(string orderIdBinary)
        {
            double addonPrice = 0;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
    SELECT SUM(price * quantity) AS TotalAddonPrice
    FROM orderaddons
    WHERE order_id = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            if (!reader.IsDBNull(reader.GetOrdinal("TotalAddonPrice")))
                            {
                                addonPrice = reader.GetDouble(reader.GetOrdinal("TotalAddonPrice"));
                            }
                        }
                    }
                }
            }

            return addonPrice;
        }


        [HttpGet("/culo-api/v1/current-user/to-pay")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToPayInitialOrdersByCustomerIds()
        {
            try
            {
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

                // Fetch all orders (no search or filtering logic)
                List<AdminInitial> orders = await FetchInitialToPayOrdersAsync(customerId);

                // If no orders are found, return an empty list
                if (orders.Count == 0)
                    return Ok(new List<toPayInitial>());

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}. Stack Trace: {ex.StackTrace}");

            }
        }


        private async Task<List<AdminInitial>> FetchInitialToPayOrdersAsync(byte[] customerid)
        {
            List<AdminInitial> orders = new List<AdminInitial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @" SELECT order_id, customer_id, type, created_at, status, payment, pickup_date, last_updated_by, last_updated_at, is_active, customer_name 
                        FROM orders WHERE status IN ('to pay') AND customer_id = @customer_id";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customer_id", customerid);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                            ? (Guid?)null
                                            : new Guid((byte[])reader["order_id"]);

                            Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                    ? Guid.Empty
                                                    : new Guid((byte[])reader["customer_id"]);

                            string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));


                            // Initialize an AdminInitial object with order details
                            AdminInitial order = new AdminInitial
                            {
                                Id = orderId,
                                CustomerId = customerIdFromDb,
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                lastUpdatedBy = lastUpdatedBy ?? "",
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                ? (DateTime?)null
                                                : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                Payment = reader.GetString(reader.GetOrdinal("payment")),
                                Type = reader.GetString(reader.GetOrdinal("type")),
                                Pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                Status = reader.GetString(reader.GetOrdinal("status")),
                            };

                            // If the order ID is valid, fetch design details and total price
                            if (order.Id.HasValue)
                            {
                                // Convert order ID to string and pass to FetchDesignAndTotalPriceAsync
                                string orderIdString = BitConverter.ToString(order.Id.Value.ToByteArray()).Replace("-", "").ToLower();
                                List<AdminInitial> designDetails = await FetchDesignToPayAsync(orderIdString);

                                // Append design details (like DesignName and Total) to the order
                                if (designDetails.Any())
                                {
                                    order.DesignId = designDetails.First().DesignId;
                                    order.DesignName = designDetails.First().DesignName;
                                }
                            }

                            orders.Add(order); // Add the order with design details to the list
                        }
                    }
                }
            }

            return orders;
        }


        private async Task<List<AdminInitial>> FetchDesignToPayAsync(string orderId)
        {
            List<AdminInitial> orders = new List<AdminInitial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
SELECT 
    HEX(design_id) as design_id, design_name 
FROM suborders WHERE order_id = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add the status parameter to the SQL command
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {

                            orders.Add(new AdminInitial
                            {
                                DesignId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                            });
                        }
                    }
                }
            }

            return orders;
        }

        [HttpGet("/culo-api/v1/current-user/orders/{orderId}/full")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetFullOrderDetailsByCustomer(string orderId)
        {

            string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

            try
            {
                // Retrieve basic order details
                var orderDetails = await GetOrderDetailx(orderIdBinary);

                if (orderDetails != null)
                {
                    // Retrieve suborders
                    orderDetails.OrderItems = await GetSuborderDetails(orderIdBinary);

                    // Initialize total sum
                    double totalSum = 0;

                    foreach (var suborder in orderDetails.OrderItems)
                    {
                        // Retrieve add-ons for each suborder
                        suborder.OrderAddons = await GetOrderAddonsDetails(suborder.SuborderId);

                        // Calculate the total for this suborder
                        double addOnsTotal = suborder.OrderAddons.Sum(addon => addon.AddOnTotal);
                        suborder.SubOrderTotal = (suborder.Price * suborder.Quantity) + addOnsTotal;

                        // Add to the overall total
                        totalSum += suborder.SubOrderTotal;
                    }

                    // Set the total in CheckOutDetails
                    orderDetails.OrderTotal = totalSum;
                }

                return Ok(orderDetails);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("/culo-api/v1/current-user/to-process")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToProcessInitialOrdersByCustomerIds()
        {
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

            try
            {
                List<toPayInitial> orders = new List<toPayInitial>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
    SELECT 
        suborder_id, order_id, customer_id, created_at, status, 
        HEX(design_id) as design_id, design_name, price, quantity, 
        last_updated_by, last_updated_at, is_active, customer_name, pastry_id 
    FROM suborders 
    WHERE customer_id = @customerId AND status IN('assigning artist', 'baking', 'for approval')";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerId", customerId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                                ? (Guid?)null
                                                : new Guid((byte[])reader["order_id"]);

                                Guid suborderId = new Guid((byte[])reader["suborder_id"]);
                                Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                        ? Guid.Empty
                                                        : new Guid((byte[])reader["customer_id"]);

                                string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                                double ingredientPrice = reader.GetDouble(reader.GetOrdinal("price"));

                                // Calculate addonPrice using the private async method
                                string orderIdBinary = BitConverter.ToString((byte[])reader["order_id"]).Replace("-", "").ToLower();
                                double addonPrice = await GetAddonPriceAsync(orderIdBinary);
                                // Calculate final price
                                double finalPrice = ingredientPrice + addonPrice;

                                orders.Add(new toPayInitial
                                {
                                    Id = orderId, // Handle null values for orderId
                                    suborderId = suborderId,
                                    CustomerId = customerIdFromDb,
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    pastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                    DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                                    Price = finalPrice,
                                    Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = lastUpdatedBy ?? "",
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                    ? (DateTime?)null
                                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    CustomerName = reader.GetString(reader.GetOrdinal("customer_name"))
                                });
                            }
                        }
                    }
                }

                // If no orders are found, return an empty list
                if (orders.Count == 0)
                    return Ok(new List<toPayInitial>());

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("/culo-api/v1/current-user/to-process/{suborderid}/full")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToProcessOrdersByCustomerId(string suborderid)
        {
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

            // Convert suborderId to binary format
            string suborderIdBinary = ConvertGuidToBinary16((suborderid)).ToLower();

            // Check if the suborder exists in the suborders table
            if (!await DoesSuborderExist(suborderIdBinary))
            {
                return NotFound($"Suborder with ID '{suborderIdBinary}' not found.");
            }
            Debug.Write(suborderIdBinary);

            try
            {
                List<Full> orders = new List<Full>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
    SELECT 
                suborder_id, order_id, customer_id, employee_id, created_at, status, 
                HEX(design_id) as design_id, design_name, price, quantity, 
                last_updated_by, last_updated_at, is_active, description, 
                flavor, size, customer_name, employee_name, shape, color, pastry_id 
            FROM suborders 
            WHERE customer_id = @customerId AND status IN ('assigning artist','baking', 'for approval') AND suborder_id = UNHEX(@suborderIdBinary)";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerId", customerId);
                        command.Parameters.AddWithValue("@suborderIdBinary", suborderIdBinary);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                                ? (Guid?)null
                                                : new Guid((byte[])reader["order_id"]);

                                Guid suborderIdFromDb = new Guid((byte[])reader["suborder_id"]);
                                Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                        ? Guid.Empty
                                                        : new Guid((byte[])reader["customer_id"]);

                                Guid employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                                  ? Guid.Empty
                                                  : new Guid((byte[])reader["employee_id"]);

                                string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                                   ? null
                                                   : reader.GetString(reader.GetOrdinal("employee_name"));

                                string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                                orders.Add(new Full
                                {
                                    suborderId = suborderIdFromDb,
                                    orderId = orderId,
                                    CustomerId = customerIdFromDb,
                                    employeeId = employeeId,
                                    employeeName = employeeName ?? string.Empty,
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    Status = reader.GetString(reader.GetOrdinal("status")),
                                    pastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                    color = reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.GetString(reader.GetOrdinal("shape")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                    DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                                    Price = reader.GetDouble(reader.GetOrdinal("price")),
                                    Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = lastUpdatedBy ?? string.Empty,
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                    ? (DateTime?)null
                                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    Description = reader.GetString(reader.GetOrdinal("description")),
                                    Flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                    Size = reader.GetString(reader.GetOrdinal("size")),
                                    CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                });
                            }
                        }
                    }

                    if (orders.Count > 0)
                    {
                        // Scan the orders table using the retrieved orderId from the suborders table
                        foreach (var order in orders)
                        {
                            if (order.orderId.HasValue)
                            {
                                // Convert orderId to binary format
                                string orderIdBinary = ConvertGuidToBinary16(order.orderId.Value.ToString()).ToLower();
                                Debug.Write(orderIdBinary);

                                var orderDetails = await GetOrderDetailsByOrderId(connection, orderIdBinary);
                                if (orderDetails != null)
                                {
                                    // Populate additional fields from the orders table
                                    order.payment = orderDetails.payment;
                                    order.PickupDateTime = orderDetails.PickupDateTime;
                                }
                            }
                        }
                    }
                }


                // Return the orders list
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("/culo-api/v1/current-user/to-receive")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToReceiveInitialOrdersByCustomerId()
        {
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

            try
            {
                List<toPayInitial> orders = new List<toPayInitial>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
    SELECT 
        suborder_id, order_id, customer_id, created_at, status, 
        HEX(design_id) as design_id, design_name, price, quantity, 
        last_updated_by, last_updated_at, is_active, customer_name, pastry_id 
    FROM suborders 
    WHERE customer_id = @customerId AND status IN('for pick up')";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerId", customerId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                                ? (Guid?)null
                                                : new Guid((byte[])reader["order_id"]);

                                Guid suborderId = new Guid((byte[])reader["suborder_id"]);
                                Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                        ? Guid.Empty
                                                        : new Guid((byte[])reader["customer_id"]);

                                string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                                double ingredientPrice = reader.GetDouble(reader.GetOrdinal("price"));

                                // Calculate addonPrice using the private async method
                                string orderIdBinary = BitConverter.ToString((byte[])reader["order_id"]).Replace("-", "").ToLower();
                                double addonPrice = await GetAddonPriceAsync(orderIdBinary);
                                // Calculate final price
                                double finalPrice = ingredientPrice + addonPrice;

                                orders.Add(new toPayInitial
                                {
                                    Id = orderId, // Handle null values for orderId
                                    suborderId = suborderId,
                                    CustomerId = customerIdFromDb,
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    pastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                    DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                                    Price = finalPrice,
                                    Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = lastUpdatedBy ?? "",
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                    ? (DateTime?)null
                                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    CustomerName = reader.GetString(reader.GetOrdinal("customer_name"))
                                });
                            }
                        }
                    }
                }

                // If no orders are found, return an empty list
                if (orders.Count == 0)
                    return Ok(new List<toPayInitial>());

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("/culo-api/v1/current-user/to-receive/{suborderid}/full")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToReceiveOrdersByCustomerId(string suborderid)
        {
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

            // Convert suborderId to binary format
            string suborderIdBinary = ConvertGuidToBinary16((suborderid)).ToLower();

            // Check if the suborder exists in the suborders table
            if (!await DoesSuborderExist(suborderIdBinary))
            {
                return NotFound($"Suborder with ID '{suborderIdBinary}' not found.");
            }
            Debug.Write(suborderIdBinary);

            try
            {
                List<Full> orders = new List<Full>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
    SELECT 
                suborder_id, order_id, customer_id, employee_id, created_at, status, 
                HEX(design_id) as design_id, design_name, price, quantity, 
                last_updated_by, last_updated_at, is_active, description, 
                flavor, size, customer_name, employee_name, shape, color, pastry_id 
            FROM suborders 
            WHERE customer_id = @customerId AND status IN ('for pick up') AND suborder_id = UNHEX(@suborderIdBinary)";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerId", customerId);
                        command.Parameters.AddWithValue("@suborderIdBinary", suborderIdBinary);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                                ? (Guid?)null
                                                : new Guid((byte[])reader["order_id"]);

                                Guid suborderIdFromDb = new Guid((byte[])reader["suborder_id"]);
                                Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                        ? Guid.Empty
                                                        : new Guid((byte[])reader["customer_id"]);

                                Guid employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                                  ? Guid.Empty
                                                  : new Guid((byte[])reader["employee_id"]);

                                string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                                   ? null
                                                   : reader.GetString(reader.GetOrdinal("employee_name"));

                                string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                                double ingredientPrice = reader.GetDouble(reader.GetOrdinal("price"));

                                // Calculate addonPrice using the private async method
                                string orderIdBinary = BitConverter.ToString((byte[])reader["order_id"]).Replace("-", "").ToLower();
                                double addonPrice = await GetAddonPriceAsync(orderIdBinary);
                                // Calculate final price
                                double finalPrice = ingredientPrice + addonPrice;

                                orders.Add(new Full
                                {
                                    suborderId = suborderIdFromDb,
                                    orderId = orderId,
                                    CustomerId = customerIdFromDb,
                                    employeeId = employeeId,
                                    employeeName = employeeName ?? string.Empty,
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    Status = reader.GetString(reader.GetOrdinal("status")),
                                    pastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                    color = reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.GetString(reader.GetOrdinal("shape")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                    DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                                    Price = finalPrice,
                                    Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = lastUpdatedBy ?? string.Empty,
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                    ? (DateTime?)null
                                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    Description = reader.GetString(reader.GetOrdinal("description")),
                                    Flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                    Size = reader.GetString(reader.GetOrdinal("size")),
                                    CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                });
                            }
                        }
                    }

                    if (orders.Count > 0)
                    {
                        // Scan the orders table using the retrieved orderId from the suborders table
                        foreach (var order in orders)
                        {
                            if (order.orderId.HasValue)
                            {
                                // Convert orderId to binary format
                                string orderIdBinary = ConvertGuidToBinary16(order.orderId.Value.ToString()).ToLower();
                                Debug.Write(orderIdBinary);

                                var orderDetails = await GetOrderDetailsByOrderId(connection, orderIdBinary);
                                if (orderDetails != null)
                                {
                                    // Populate additional fields from the orders table
                                    order.payment = orderDetails.payment;
                                    order.PickupDateTime = orderDetails.PickupDateTime;
                                }
                            }
                        }
                    }
                }


                // Return the orders list
                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        private async Task<OrderDetails?> GetOrderDetailsByOrderId(MySqlConnection connection, string orderIdBinary)
        {
            string sql = @"
        SELECT order_id, status, payment, type, pickup_date 
        FROM orders 
        WHERE order_id = UNHEX(@orderIdBinary)";

            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@orderIdBinary", orderIdBinary);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new OrderDetails
                        {
                            orderId = new Guid((byte[])reader["order_id"]),
                            Status = reader.GetString(reader.GetOrdinal("status")),
                            payment = reader.IsDBNull(reader.GetOrdinal("payment")) ? (string) null :reader.GetString(reader.GetOrdinal("payment")),
                            type = reader.GetString(reader.GetOrdinal("type")),
                            PickupDateTime = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                            ? (DateTime?)null
                                            : reader.GetDateTime(reader.GetOrdinal("pickup_date"))
                        };
                    }
                }
            }

            return null;
        }

        [HttpGet("/culo-api/v1/current-user/artist/to-do")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrderxByCustomerId()
        {
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

            try
            {
                List<Cart> orders = new List<Cart>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
            SELECT 
                suborder_id, order_id, customer_id, employee_id, created_at, status, 
                HEX(design_id) as design_id, design_name, price, quantity, 
                last_updated_by, last_updated_at, is_active, description, 
                flavor, size, customer_name, employee_name, shape, color, pastry_id 
            FROM suborders 
            WHERE employee_id = @customerId AND status IN('confirmed')";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerId", customerId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                                ? (Guid?)null
                                                : new Guid((byte[])reader["order_id"]);

                                Guid suborderId = new Guid((byte[])reader["suborder_id"]);
                                Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                        ? Guid.Empty
                                                        : new Guid((byte[])reader["customer_id"]);

                                Guid employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                                  ? Guid.Empty
                                                  : new Guid((byte[])reader["employee_id"]);

                                string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                                   ? null
                                                   : reader.GetString(reader.GetOrdinal("employee_name"));

                                string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                                orders.Add(new Cart
                                {
                                    Id = orderId, // Handle null values for orderId
                                    suborderId = suborderId,
                                    CustomerId = customerIdFromDb,
                                    Status = reader.GetString(reader.GetOrdinal("status")),
                                    color = reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.GetString(reader.GetOrdinal("shape")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                    DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                                    Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    Description = reader.GetString(reader.GetOrdinal("description")),
                                    Flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                    Size = reader.GetString(reader.GetOrdinal("size")),
                                    CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                });
                            }
                        }
                    }
                }

                // If no orders are found, return an empty list
                if (orders.Count == 0)
                    return Ok(new List<Order>());

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("{orderId}/final-details")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetFinalOrderDetailsByOrderId(string orderId)
        {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(customerUsername))
            {
                return Unauthorized("User is not authorized");
            }

            byte[] customerId = await GetUserIdByAllUsername(customerUsername);
            if (customerId == null || customerId.Length == 0)
            {
                return BadRequest("Customer not found");
            }

            string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

            try
            {
                // Retrieve basic order details
                var orderDetails = await GetOrderDetailx(orderIdBinary);

                if (orderDetails != null)
                {
                    // Retrieve suborders
                    orderDetails.OrderItems = await GetSuborderDetails(orderIdBinary);

                    // Initialize total sum
                    double totalSum = 0;

                    foreach (var suborder in orderDetails.OrderItems)
                    {
                        // Retrieve add-ons for each suborder
                        suborder.OrderAddons = await GetOrderAddonsDetails(suborder.SuborderId);

                        // Calculate the total for this suborder
                        double addOnsTotal = suborder.OrderAddons.Sum(addon => addon.AddOnTotal);
                        suborder.SubOrderTotal = (suborder.Price * suborder.Quantity) + addOnsTotal;

                        // Add to the overall total
                        totalSum += suborder.SubOrderTotal;

                    }

                    // Set the total in CheckOutDetails
                    orderDetails.OrderTotal = totalSum;
                }

                return Ok(orderDetails);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        private async Task<CheckOutDetails> GetOrderDetailx(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string orderSql = @"
    SELECT order_id, status, payment, type, pickup_date 
    FROM orders 
    WHERE order_id = UNHEX(@orderIdBinary)";

                using (var command = new MySqlCommand(orderSql, connection))
                {
                    command.Parameters.AddWithValue("@orderIdBinary", orderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new CheckOutDetails
                            {
                                OrderId = new Guid((byte[])reader["order_id"]),
                                Status = reader.GetString(reader.GetOrdinal("status")),
                                PaymentMethod = reader.GetString(reader.GetOrdinal("payment")),
                                OrderType = reader.GetString(reader.GetOrdinal("type")),
                                PickupDateTime = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                    ? (DateTime?)null
                                    : reader.GetDateTime(reader.GetOrdinal("pickup_date"))
                            };
                        }
                    }
                }
            }
            return null;
        }

        private async Task<List<OrderItem>> GetSuborderDetails(string orderIdBinary)
        {
            var suborders = new List<OrderItem>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string suborderSql = @"
    SELECT 
        suborder_id, order_id, customer_id, employee_id, created_at, status, 
        HEX(design_id) as design_id, design_name, price, quantity, 
        last_updated_by, last_updated_at, is_active, description, 
        flavor, size, customer_name, employee_name, shape, color, pastry_id 
    FROM suborders 
    WHERE order_id = UNHEX(@orderIdBinary)";

                using (var command = new MySqlCommand(suborderSql, connection))
                {
                    command.Parameters.AddWithValue("@orderIdBinary", orderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var suborder = new OrderItem
                            {
                                SuborderId = new Guid((byte[])reader["suborder_id"]),
                                OrderId = new Guid((byte[])reader["order_id"]),
                                CustomerId = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                    ? Guid.Empty
                                    : new Guid((byte[])reader["customer_id"]),
                                EmployeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                    ? Guid.Empty
                                    : new Guid((byte[])reader["employee_id"]),
                                EmployeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                    ? string.Empty
                                    : reader.GetString(reader.GetOrdinal("employee_name")),
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                Status = reader.GetString(reader.GetOrdinal("status")),
                                PastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                Color = reader.GetString(reader.GetOrdinal("color")),
                                Shape = reader.GetString(reader.GetOrdinal("shape")),
                                DesignId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                                Price = reader.GetDouble(reader.GetOrdinal("price")),
                                Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                LastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                    ? string.Empty
                                    : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                LastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                    ? (DateTime?)null
                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                Description = reader.GetString(reader.GetOrdinal("description")),
                                Flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                Size = reader.GetString(reader.GetOrdinal("size")),
                                CustomerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                SubOrderTotal = reader.GetDouble(reader.GetOrdinal("price")) * reader.GetInt32(reader.GetOrdinal("quantity")) // Calculate Total
                            };

                            suborders.Add(suborder);
                        }
                    }
                }
            }

            return suborders;
        }

        private async Task<List<OrderAddon1>> GetOrderAddonsDetails(Guid suborderId)
        {
            var addons = new List<OrderAddon1>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string addOnsSql = @"
    SELECT add_ons_id, quantity, total, name, price
    FROM orderaddons
    WHERE order_id = UNHEX(@suborderIdBinary)";

                using (var command = new MySqlCommand(addOnsSql, connection))
                {
                    string subId = BitConverter.ToString(suborderId.ToByteArray()).Replace("-", "").ToLower();
                    command.Parameters.AddWithValue("@suborderIdBinary", subId);
                    Debug.Write("suborderId: " + subId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            addons.Add(new OrderAddon1
                            {
                                AddonId = reader.GetInt32(reader.GetOrdinal("add_ons_id")),
                                Quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                AddOnTotal = reader.GetDouble(reader.GetOrdinal("total")),
                                Name = reader.GetString(reader.GetOrdinal("name")),
                                Price = reader.GetDouble(reader.GetOrdinal("price"))
                            });
                        }
                    }
                }
            }

            return addons;
        }

        [HttpGet("employees-name")] //done
        public async Task<IActionResult> GetEmployeesOfType2()
        {
            try
            {
                List<employee> employees = new List<employee>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"SELECT Username AS Name, UserId FROM users WHERE Type = 2";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Guid userId = reader.IsDBNull(reader.GetOrdinal("UserId"))
                                                        ? Guid.Empty
                                                        : new Guid((byte[])reader["UserId"]);
                                employee employee = new employee
                                {
                                    name = reader.GetString("Name"),
                                    userId = userId

                                };

                                employees.Add(employee);
                            }
                        }
                    }
                }
                // If no orders are found, return an empty list
                if (employees.Count == 0)
                    return Ok(new List<employee>());

                return Ok(employees);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to retrieve employees: {ex.Message}");
            }
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

        [HttpGet("total-active-orders")] //done
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetTotalQuantities()
        {
            try
            {
                TotalOrders totalQuantities = new TotalOrders();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "SELECT SUM(quantity) AS TotalQuantity FROM suborders WHERE is_active = TRUE";

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

        [HttpGet("total-order-quantity/day")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetTotalQuantityForDay([FromQuery] int year, [FromQuery] int month, [FromQuery] int day)
        {
            try
            {
                // Create a DateTime object from the provided query parameters
                DateTime specificDay = new DateTime(year, month, day);

                // Call the method to get total quantity for the specific day
                int total = await GetTotalQuantityForSpecificDay(specificDay);

                // If total is 0, return an empty string or array
                if (total == 0)
                {
                    return Ok(new { Day = specificDay.ToString("dddd"), TotalOrders = 0 });
                }

                // Return the result as day name and total order quantity
                return Ok(new
                {
                    Day = specificDay.ToString("dddd"), // Get the full name of the day (e.g., Monday)
                    TotalOrders = total
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total quantity for the day.");
                return StatusCode(500, "Internal server error.");
            }
        }

        // Private method for fetching quantity on a specific day
        private async Task<int> GetTotalQuantityForSpecificDay(DateTime specificDay)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT SUM(quantity) 
            FROM suborders 
            WHERE DAY(created_at) = @day AND MONTH(created_at) = @month AND YEAR(created_at) = @year";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Use the DateTime properties for the day, month, and year
                    command.Parameters.AddWithValue("@day", specificDay.Day);
                    command.Parameters.AddWithValue("@month", specificDay.Month);
                    command.Parameters.AddWithValue("@year", specificDay.Year);

                    object result = await command.ExecuteScalarAsync();
                    return result != DBNull.Value ? Convert.ToInt32(result) : 0; // Return 0 if result is DBNull
                }
            }
        }

        [HttpGet("total-order-quantity/week")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetTotalQuantityForWeek([FromQuery] int year, [FromQuery] int month, [FromQuery] int day)
        {
            try
            {
                // Create a DateTime object from the provided query parameters for the start of the week
                DateTime startOfWeek = new DateTime(year, month, day).StartOfWeek(DayOfWeek.Monday);

                // Fetch total quantity for the specific week
                var weekQuantities = await GetTotalQuantityForSpecificWeek(startOfWeek);

                // If no data found, return an empty array
                if (weekQuantities.Count == 0)
                {
                    return Ok(new List<object>()); // Return an empty array
                }

                // Return result in the desired format
                return Ok(weekQuantities.Select(q => new
                {
                    Day = q.Key,
                    TotalOrders = q.Value
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total quantity for the week.");
                return StatusCode(500, "Internal server error.");
            }
        }

        // Private method for fetching quantity for a specific week
        private async Task<Dictionary<string, int>> GetTotalQuantityForSpecificWeek(DateTime startOfWeek)
        {
            var quantities = new Dictionary<string, int>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT DAYNAME(created_at) AS DayName, SUM(quantity) AS TotalQuantity
            FROM suborders 
            WHERE created_at >= @startOfWeek AND created_at < @endOfWeek
            GROUP BY DAYNAME(created_at)
            ORDER BY FIELD(DAYNAME(created_at), 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday')";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Set the start of the week and end of the week for the SQL query
                    command.Parameters.AddWithValue("@startOfWeek", startOfWeek);
                    command.Parameters.AddWithValue("@endOfWeek", startOfWeek.AddDays(7));

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Add day names and corresponding total quantities to the dictionary
                            quantities[reader.GetString("DayName")] = reader.GetInt32("TotalQuantity");
                        }
                    }
                }
            }

            return quantities;
        }


        [HttpGet("total-order-quantity/month")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetTotalQuantityForMonth([FromQuery] int year, [FromQuery] int month)
        {
            try
            {
                // Fetch total quantity for the specific month
                var dailyQuantities = await GetTotalQuantityForSpecificMonth(year, month);

                // If no data found, return an empty array
                if (dailyQuantities.Count == 0)
                {
                    return Ok(new List<object>()); // Return an empty array
                }

                // Return result in the desired format
                return Ok(dailyQuantities.Select(q => new
                {
                    Day = q.Key, // Day number
                    TotalOrders = q.Value // Total orders for that day
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total quantity for the month.");
                return StatusCode(500, "Internal server error.");
            }
        }

        // Private method to get total quantity for a specific month
        private async Task<Dictionary<int, int>> GetTotalQuantityForSpecificMonth(int year, int month)
        {
            var dailyQuantities = new Dictionary<int, int>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT DAY(created_at) AS Day, SUM(quantity) AS TotalQuantity
            FROM suborders 
            WHERE MONTH(created_at) = @month AND YEAR(created_at) = @year
            GROUP BY DAY(created_at)
            ORDER BY DAY(created_at)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Set the month and year parameters for the SQL query
                    command.Parameters.AddWithValue("@month", month);
                    command.Parameters.AddWithValue("@year", year);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Add day and total quantities to the dictionary
                            dailyQuantities[reader.GetInt32("Day")] = reader.GetInt32("TotalQuantity");
                        }
                    }
                }
            }

            return dailyQuantities;
        }

        [HttpGet("total-order-quantity/year")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetTotalQuantityForYear([FromQuery] int year)
        {
            try
            {
                // Fetch total quantity for the specific year
                var yearlyQuantities = await GetTotalQuantityForSpecificYear(year);

                // If no data found, return an empty array
                if (yearlyQuantities.Count == 0)
                {
                    return Ok(new List<object>()); // Return an empty array
                }

                // Return result in the desired format
                return Ok(yearlyQuantities.Select(q => new
                {
                    Month = q.Key, // Month name
                    TotalOrders = q.Value // Total orders for that month
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total quantity for the year.");
                return StatusCode(500, "Internal server error.");
            }
        }

        // Private method to get total quantity for a specific year
        private async Task<Dictionary<string, int>> GetTotalQuantityForSpecificYear(int year)
        {
            var quantities = new Dictionary<string, int>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT MONTHNAME(created_at) AS MonthName, SUM(quantity) AS TotalQuantity
            FROM suborders 
            WHERE YEAR(created_at) = @year
            GROUP BY MONTH(created_at)
            ORDER BY MONTH(created_at)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Set the year parameter for the SQL query
                    command.Parameters.AddWithValue("@year", year);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Add month name and total quantities to the dictionary
                            quantities[reader.GetString("MonthName")] = reader.GetInt32("TotalQuantity");
                        }
                    }
                }
            }

            return quantities;
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


        [HttpPatch("suborders/{suborderId}/update-status")] //done
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Artist)]
        public async Task<IActionResult> PatchOrderStatus(string suborderId, [FromQuery] string action)
        {
            try
            {
                // Convert the orderId from GUID string to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

                // Update the order status based on the action
                if (action.Equals("send", StringComparison.OrdinalIgnoreCase))
                {
                    await UpdateOrderStatus(orderIdBinary, true); // Set isActive to true
                    await UpdateStatus(orderIdBinary, "for pick up");
                    await UpdateLastUpdatedAt(orderIdBinary);
                }
                else if (action.Equals("done", StringComparison.OrdinalIgnoreCase))
                {
                    // Update the status in the database
                    await UpdateOrderStatus(orderIdBinary, false); // Set isActive to false
                    await UpdateStatus(orderIdBinary, "done");
                    await ProcessOrderCompletion(orderIdBinary);
                    // Update the last_updated_at column
                    await UpdateLastUpdatedAt(orderIdBinary);
                }
                else
                {
                    return BadRequest("Invalid action. Please choose 'send' or 'done'.");
                }

                // Call the method to get customer ID and name
                var (customerId, customerName) = await GetCustomerInfoBySubOrderId(orderIdBinary);

                if (customerId != null && customerId.Length > 0)
                {
                    // Convert the byte[] customerId to a hex string
                    string userId = BitConverter.ToString(customerId).Replace("-", "").ToLower();

                    // Check the value of 'action' and send the corresponding notification
                    if (action.Equals("send", StringComparison.OrdinalIgnoreCase))
                    {
                        // Construct the message for 'send' action
                        string message = ((customerName ?? "Unknown") + " your order is ready for pick up");

                        // Send the notification
                        await NotifyAsync(userId, message);
                    }
                    else if (action.Equals("done", StringComparison.OrdinalIgnoreCase))
                    {
                        // Construct the message for 'done' action
                        string message = ((customerName ?? "Unknown") + " order received");

                        // Send the notification
                        await NotifyAsync(userId, message);
                    }
                    else
                    {
                        // Handle other cases or log unexpected actions
                        Debug.Write("Invalid action or no notification sent for this action.");
                    }
                }
                else
                {
                    // Handle case where customer info is not found
                    Debug.Write("Customer not found for the given order.");
                }

                return Ok($"Order with ID '{suborderId}' has been successfully updated to '{action}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while processing the request to update order status for '{suborderId}'.");
                return StatusCode(500, $"An error occurred while processing the request to update order status for '{suborderId}'.");
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

        private async Task<forSales> GetOrderDetails(string orderIdBytes) //update this
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT o.design_name, o.price, o.employee_id, o.created_at, o.quantity, 
                                    u.Contact, u.Email 
                                    FROM suborders o
                                    JOIN users u ON o.employee_id = u.UserId
                                    WHERE o.suborder_id = UNHEX(@orderId)";
                    command.Parameters.AddWithValue("@orderId", orderIdBytes);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (reader.Read())
                        {
                            var name = reader.GetString("design_name");
                            var cost = reader.GetDouble("price");
                            var contact = reader.GetString("Contact").Trim(); // Adjust for CHAR(10)
                            var email = reader.GetString("Email");
                            var date = reader.GetDateTime("created_at");
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

                string sql = "UPDATE sales SET Total = @newTotal WHERE Name = @orderName";

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

                    // Update the status of the suborders
                    string subOrderSql = "UPDATE suborders SET status = @subOrderStatus WHERE suborder_id = UNHEX(@orderId)";
                    using (var subOrderCommand = new MySqlCommand(subOrderSql, connection))
                    {
                        subOrderCommand.Parameters.AddWithValue("@subOrderStatus", status);
                        subOrderCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                        await subOrderCommand.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while updating status for order with ID '{orderIdBinary}'.");
                throw;
            }
        }

        [HttpPut("/culo-api/v1/current-user/{suborderId}/manage-add-ons")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> ManageAddOnsByAddOnId(string suborderId, [FromBody] List<AddOn> manageAddOns)
        {
            // Convert suborderId to binary format
            string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

            // Loop through each AddOn in the manage list
            foreach (var manage in manageAddOns)
            {
                // Log the process for each add-on
                _logger.LogInformation($"Managing AddOnId: {manage.Id} for SubOrderId: {suborderId}");

                // Fetch the add-on price and name
                double addonPrice = await GetAddonPriceAsync(manage.Id);
                string name = await AddonName(manage.Id);

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();
                    try
                    {
                        // Calculate total price
                        double total = manage.quantity * addonPrice;

                        // Insert or update the order add-ons for the current add-on
                        await InsertOrUpdateOrderaddonWithSubOrderId(suborderIdBinary, manage.Id, addonPrice, manage.quantity, name, total);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Transaction failed for AddOnId: {manage.Id}, rolling back");
                    }
                }
            }

            return Ok("Add-ons quantities successfully managed.");
        }


        private async Task<double> GetAddonPriceAsync(int addOnId)
        {
            string sql = @"SELECT price FROM addons WHERE add_ons_id = @addOnId";
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@addOnId", addOnId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader.GetDouble("price");
                        }
                        else
                        {
                            throw new Exception($"Price not found for AddOnId '{addOnId}'.");
                        }
                    }
                }
            }
        }

        private async Task<string> AddonName(int addOnId)
        {
            string addonName = string.Empty;

            // Query to retrieve the pastry_material_sub_variant_id based on pastryId and size
            string query = "SELECT name FROM addons WHERE add_ons_id = @addonId";

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@addonId", addOnId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            addonName = reader.GetString("name");
                        }
                    }
                }
            }

            return addonName;  // Return the sub-variant ID
        }

        private async Task InsertOrUpdateOrderaddonWithSubOrderId(string orderIdBinary, int addOnsId, double price, int quantity, string name, double total)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Check if the record already exists
                string checkSql = @"SELECT COUNT(*) FROM orderaddons 
                            WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnId";

                using (var checkCommand = new MySqlCommand(checkSql, connection))
                {
                    checkCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                    checkCommand.Parameters.AddWithValue("@addOnId", addOnsId);

                    var count = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());

                    if (count > 0)
                    {
                        // Record exists, update it
                        string updateSql = @"UPDATE orderaddons 
                                     SET name = @name, price = @price, quantity = @quantity, total = @total 
                                     WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnId";

                        using (var updateCommand = new MySqlCommand(updateSql, connection))
                        {
                            updateCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                            updateCommand.Parameters.AddWithValue("@addOnId", addOnsId);
                            updateCommand.Parameters.AddWithValue("@quantity", quantity);
                            updateCommand.Parameters.AddWithValue("@price", price);
                            updateCommand.Parameters.AddWithValue("@name", name);
                            updateCommand.Parameters.AddWithValue("@total", total);

                            await updateCommand.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        // No existing record, insert new
                        string insertSql = @"INSERT INTO orderaddons (order_id, add_ons_id, quantity, total, name, price)
                                     VALUES (UNHEX(@orderId), @addOnId, @quantity, @total, @name, @price)";

                        using (var insertCommand = new MySqlCommand(insertSql, connection))
                        {
                            insertCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                            insertCommand.Parameters.AddWithValue("@addOnId", addOnsId);
                            insertCommand.Parameters.AddWithValue("@quantity", quantity);
                            insertCommand.Parameters.AddWithValue("@price", price);
                            insertCommand.Parameters.AddWithValue("@name", name);
                            insertCommand.Parameters.AddWithValue("@total", total);

                            await insertCommand.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }


        [HttpPatch("/culo-api/v1/current-user/manage-add-ons-by-material/{pastryMaterialId}/{suborderId}/{modifiedAddOnId}")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> ManageAddOnsByPastryMaterialId(string pastryMaterialId, string suborderId, int modifiedAddOnId, [FromBody] ManageAddOnAction action)
        {
            // Log the start of the process
            _logger.LogInformation($"Starting ManageAddOnsByPastryMaterialId for pastryMaterialId: {pastryMaterialId}, OrderId: {suborderId}, and AddOnId: {modifiedAddOnId}");

            // Convert OrderId to binary format
            string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // Retrieve size from orders table
                        string size = await GetOrderSize(connection, transaction, suborderIdBinary);

                        Debug.WriteLine(size);

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
                                if (action.ActionType.ToLower() == "quantity")
                                {
                                    // Fetch add-on details
                                    if (addOnDetailsDict.TryGetValue(addOn.AddOnId, out var addOnDetails))
                                    {
                                        // Calculate total price
                                        double total = action.Quantity * addOnDetails.Price;

                                        // Insert or update quantity for the specified add-on in orderaddons
                                        await SetOrUpdateAddOn(connection, transaction, suborderIdBinary, addOn.AddOnId, action.Quantity, total, addOnDetailsDict);
                                    }
                                }
                                else if (action.ActionType.ToLower() == "remove")
                                {
                                    // Set quantity to 0 and remove add-on from orderaddons
                                    await SetOrUpdateAddOn(connection, transaction, suborderIdBinary, addOn.AddOnId, 0, 0, addOnDetailsDict);
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
                                await SetOrUpdateAddOn(connection, transaction, suborderIdBinary, addOn.AddOnId, addOn.Quantity, total, addOnDetailsDict);
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


        private async Task<string> GetOrderSize(MySqlConnection connection, MySqlTransaction transaction, string orderId)
        {
            string sql = @"SELECT size
                   FROM suborders
                   WHERE suborder_id = UNHEX(@orderId)";

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
                   FROM pastrymaterialaddons
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
                   WHERE add_ons_id = @addOnId";

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

        private async Task SetOrUpdateAddOn(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary, int addOnId, int quantity, double total, Dictionary<int, (string Name, double Price)> addOnDetailsDict)
        {
            // Retrieve add-on details from the dictionary
            if (!addOnDetailsDict.TryGetValue(addOnId, out var addOnDetails))
            {
                throw new Exception($"Add-on details not found for AddOnId '{addOnId}'.");
            }

            if (quantity > 0)
            {
                // Check if the add-on already exists in orderaddons
                string selectSql = @"SELECT COUNT(*) 
            FROM orderaddons 
            WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnId";

                using (var selectCommand = new MySqlCommand(selectSql, connection, transaction))
                {
                    selectCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                    selectCommand.Parameters.AddWithValue("@addOnId", addOnId);

                    int count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                    if (count == 0)
                    {
                        // Insert new add-on into orderaddons
                        string insertSql = @"INSERT INTO orderaddons (order_id, add_ons_id, quantity, total, name, price)
                    VALUES (UNHEX(@orderId), @addOnId, @quantity, @total, @name, @price)";
                        using (var insertCommand = new MySqlCommand(insertSql, connection, transaction))
                        {
                            insertCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                            insertCommand.Parameters.AddWithValue("@addOnId", addOnId);
                            insertCommand.Parameters.AddWithValue("@quantity", quantity);
                            insertCommand.Parameters.AddWithValue("@total", total);
                            insertCommand.Parameters.AddWithValue("@name", addOnDetails.Name);
                            insertCommand.Parameters.AddWithValue("@price", addOnDetails.Price);
                            _logger.LogInformation($"Inserting add-on ID '{addOnId}' with quantity '{quantity}', total '{total}', name '{addOnDetails.Name}', and price '{addOnDetails.Price}' into orderaddons");
                            await insertCommand.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        // Update quantity and total for existing add-on in orderaddons
                        string updateSql = @"UPDATE orderaddons 
                    SET quantity = @quantity, total = @total
                    WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnId";

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
            string deleteSql = @"DELETE FROM orderaddons WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnId";
            using (var deleteCommand = new MySqlCommand(deleteSql, connection, transaction))
            {
                deleteCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                deleteCommand.Parameters.AddWithValue("@addOnId", addOnId);

                _logger.LogInformation($"Removing add-on ID '{addOnId}' from orderaddons");

                await deleteCommand.ExecuteNonQueryAsync();
            }
        }


        [HttpPatch("/culo-api/v1/current-user/manage-add-ons-by-material/suborders/{suborderId}")]//done 
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> UpdateOrderDetails(string suborderId, [FromBody] UpdateOrderDetailsRequest request)
        {
            try
            {
                _logger.LogInformation($"Starting UpdateOrderDetails for orderId: {suborderId}");

                // Convert orderId to binary(16) format without '0x' prefix
                string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // Update order details in the orders table
                            await UpdateOrderDetailsInDatabase(connection, transaction, suborderIdBinary, request);

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
                _logger.LogError(ex, $"Error updating order details for order with ID '{suborderId}'");
                return StatusCode(500, $"An error occurred while updating order details for order with ID '{suborderId}'.");
            }
        }

        private async Task UpdateOrderDetailsInDatabase(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary, UpdateOrderDetailsRequest request)
        {

            // Prepare SQL statement for updating orders table
            string updateSql = @"UPDATE suborders 
                         SET description = @description, 
                             quantity = @quantity,
                             size = @size,
                             flavor = @flavor,
                             color = @color, shape = @shape
                         WHERE suborder_id = UNHEX(@suborderId)";

            using (var command = new MySqlCommand(updateSql, connection, transaction))
            {
                command.Parameters.AddWithValue("@description", request.Description);
                command.Parameters.AddWithValue("@quantity", request.Quantity);
                command.Parameters.AddWithValue("@size", request.Size);
                command.Parameters.AddWithValue("@flavor", request.Flavor);
                command.Parameters.AddWithValue("@color", request.color);
                command.Parameters.AddWithValue("@shape", request.shape);
                command.Parameters.AddWithValue("@suborderId", orderIdBinary);


                await command.ExecuteNonQueryAsync();

                _logger.LogInformation($"Updated order details in orders table for order with ID '{orderIdBinary}'");
            }
        }

        [HttpPatch("custom-orders/{customId}/set-price")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> PatchCustomOrder(string customId, [FromBody] CustomOrderUpdateRequest customReq)
        {
            if (customReq == null || string.IsNullOrWhiteSpace(customId))
            {
                return BadRequest("Invalid request data.");
            }

            string suborderIdBinary = ConvertGuidToBinary16(customId).ToLower();

            string sql = @"
            UPDATE customorders 
            SET design_name = @designName, 
                price = @price 
            WHERE custom_id = UNHEX(@customId)";

            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    using (MySqlCommand cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@designName", customReq.DesignName);
                        cmd.Parameters.AddWithValue("@price", customReq.Price);
                        cmd.Parameters.AddWithValue("@customId", suborderIdBinary);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        var (customerId, customerName) = await GetCustomerInfoForCustomOrders(suborderIdBinary);

                        if (!string.IsNullOrEmpty(customerId))
                        {
                            // Convert the customerId to the binary format needed
                            string userId = ConvertGuidToBinary16(customerId).ToLower();

                            // Construct the message
                            string message = ((customerName ?? "Unknown") + " your order has been approved; view final details");

                            // Send the notification
                            await NotifyAsync(userId, message);
                        }
                        else
                        {
                            // Handle case where customer info is not found
                            Debug.Write("Customer not found for the given order.");
                        }

                        if (rowsAffected > 0)
                        {
                            return Ok("Custom order updated successfully.");
                        }
                        else
                        {
                            return NotFound("Custom order not found.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception here (not shown)
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }

        private async Task<(string customerId, string customerName)> GetCustomerInfoForCustomOrders(string order)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string designQuery = "SELECT customer_name, customer_id FROM customorders WHERE custom_id = UNHEX(@orderId)";
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



        [HttpDelete("/culo-api/v1/current-user/orders/{orderId}/remove")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
        public async Task<IActionResult> RemoveOrder(string orderId)
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
            WHERE order_id = UNHEX(@orderId) 
            AND customer_id = (SELECT UserId FROM users WHERE Username = @customerUsername)";

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

                string sql = "DELETE FROM orders WHERE order_id = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        [HttpDelete("/culo-api/v1/current-user/cart/{suborderId}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
        public async Task<IActionResult> RemoveCart(string suborderId)
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
                string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

                // Check if the order belongs to the current user
                bool isOrderOwnedByUser = await IsSuborderOwnedByUser(customerUsername, suborderIdBinary);
                if (!isOrderOwnedByUser)
                {
                    return Unauthorized("You do not have permission to delete this order.");
                }

                // Delete the order from the database
                bool deleteSuccess = await DeleteOrderBySuborderId(suborderIdBinary);
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

        private async Task<bool> IsSuborderOwnedByUser(string customerUsername, string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT COUNT(*) 
            FROM suborders 
            WHERE suborder_id = UNHEX(@orderId) 
            AND customer_id = (SELECT UserId FROM users WHERE Username = @customerUsername)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    command.Parameters.AddWithValue("@customerUsername", customerUsername);

                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        private async Task<bool> DeleteOrderBySuborderId(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "DELETE FROM suborders WHERE suborder_id = UNHEX(@orderId) AND status = 'cart'";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        [HttpDelete("/culo-api/v1/current-user/cart")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
        public async Task<IActionResult> RemoveAllCart()
        {
            try
            {
                // Get the current user's username
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("No valid customer username found.");
                }

                // Check if the suborders belong to the current user
                bool isOrderOwnedByUser = await SuborderOwnedByUser(customerUsername);
                if (!isOrderOwnedByUser)
                {
                    return Unauthorized("You do not have permission to delete this order.");
                }

                // Delete all suborders belonging to the current user
                bool deleteSuccess = await DeleteAllSubordersByCustomerUsername(customerUsername);
                if (deleteSuccess)
                {
                    return Ok("All suborders removed successfully.");
                }
                else
                {
                    return NotFound("No suborders found for this user.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cart");
                return StatusCode(500, $"An error occurred while processing the request.");
            }
        }

        private async Task<bool> SuborderOwnedByUser(string customerUsername)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
        SELECT COUNT(*) 
        FROM suborders 
        WHERE customer_id = (SELECT UserId FROM users WHERE Username = @customerUsername)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customerUsername", customerUsername);

                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        private async Task<bool> DeleteAllSubordersByCustomerUsername(string customerUsername)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
        DELETE FROM suborders 
        WHERE status = 'cart' AND customer_id = (SELECT UserId FROM users WHERE Username = @customerUsername)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customerUsername", customerUsername);

                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }


        private async Task<bool> CheckOrderExists(string SuborderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM suborders WHERE suborder_id = UNHEX(@orderId)";
                    command.Parameters.AddWithValue("@orderId", SuborderIdBinary);

                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result) > 0;
                }
            }
        }

        private async Task UpdateOrderEmployeeId(string orderIdBinary, string employeeId, string employeeUsername)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Update the status in both suborders and orders tables
                string sqlSuborders = "UPDATE suborders SET employee_id = UNHEX(@employeeId), employee_name = @employeeName, status = 'baking' WHERE suborder_id = UNHEX(@orderId)";

                using (var command = new MySqlCommand(sqlSuborders, connection))
                {
                    command.Parameters.AddWithValue("@employeeId", employeeId);
                    command.Parameters.AddWithValue("@employeeName", employeeUsername);
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateOrderStatusToBaking(string subOrderIdBinary)
        {
            string orderIdFi = null;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Step 1: Retrieve the order_id from the suborders table
                string sqlSubOrder = "SELECT order_id FROM suborders WHERE suborder_id = UNHEX(@subOrderId)";
                using (var command = new MySqlCommand(sqlSubOrder, connection))
                {
                    command.Parameters.AddWithValue("@subOrderId", subOrderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Convert the order_id directly as it is assumed to be a valid Guid in binary format
                            Guid orderId = new Guid((byte[])reader["order_id"]);

                            // Step 2: Convert the retrieved order_id to binary format
                            orderIdFi = ConvertGuidToBinary16(orderId.ToString()).ToLower();
                        }
                        else
                        {
                            throw new ArgumentException("No suborder found with the provided ID", nameof(subOrderIdBinary));
                        }
                    }
                }

                // Step 3: Update the status in the orders table
                if (!string.IsNullOrEmpty(orderIdFi))
                {
                    string sqlOrders = "UPDATE orders SET status = 'baking' WHERE order_id = UNHEX(@orderId)";
                    using (var command = new MySqlCommand(sqlOrders, connection))
                    {
                        command.Parameters.AddWithValue("@orderId", orderIdFi);
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        if (rowsAffected == 0)
                        {
                            throw new ArgumentException("Order not found or status not updated", nameof(orderIdFi));
                        }
                    }
                }
            }
        }


        private async Task UpdateOrderStatus(string orderIdBinary, bool isActive)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "UPDATE suborders SET is_active = @isActive WHERE suborder_id = UNHEX(@orderId)";

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

                string sql = "UPDATE suborders SET last_updated_at = NOW() WHERE suborder_id = UNHEX(@orderId)";

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

                string designQuery = "SELECT display_name FROM designs WHERE design_id = UNHEX(@displayName)";
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

        private async Task<(byte[] customerId, string customerName)> GetCustomerInfoBySubOrderId(string order)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string designQuery = "SELECT customer_name, customer_id FROM orders WHERE suborder_id = UNHEX(@orderId)";
                using (var designcommand = new MySqlCommand(designQuery, connection))
                {
                    designcommand.Parameters.AddWithValue("@orderId", order);
                    using (var reader = await designcommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Retrieve customer_id as byte[]
                            byte[] customerId = (byte[])reader["customer_id"];
                            string customerName = reader.GetString("customer_name");

                            return (customerId, customerName);  // Return customerId as byte[] and customerName as string
                        }
                        else
                        {
                            return (null, null); // No matching record found
                        }
                    }
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

        private async Task<List<string>> GetEmployeeAllId()
        {
            var empIds = new List<string>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();  // Open the connection only once

                string sql = "SELECT UserId FROM users WHERE Type IN (3,4)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())  // Loop through all the results
                        {
                            // Retrieve the UserId and add it to the list
                            empIds.Add(reader.GetString("UserId"));
                        }
                    }
                }
            }

            return empIds;  // Return the list of UserIds
        }


    }
}
