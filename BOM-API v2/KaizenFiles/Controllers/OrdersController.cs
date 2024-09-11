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
using System.Text.Json;
using static BOM_API_v2.KaizenFiles.Models.Adds;
using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Helpers;// Adjust the namespace according to your project structure



namespace BOM_API_v2.KaizenFiles.Controllers {
    [Route("orders")]
    [ApiController]
    [Authorize]
    public class OrdersController: ControllerBase {
        private readonly string connectionstring;
        private readonly ILogger<OrdersController> _logger;

        private readonly DatabaseContext _context;
        private readonly KaizenTables _kaizenTables;

        public OrdersController(IConfiguration configuration,ILogger<OrdersController> logger, DatabaseContext context, KaizenTables kaizenTables) {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;

            _context = context;
            _kaizenTables = kaizenTables;


        }

        [HttpPost("/culo-api/v1/current-user/buy-now")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> BuyNow([FromBody] BuyNow buyNowRequest) {
            try {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if(string.IsNullOrEmpty(customerUsername)) {
                    return Unauthorized("User is not authorized");
                }

                // Retrieve customerId from username
                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
                if(customerId == null || customerId.Length == 0) {
                    return BadRequest("Customer not found.");
                }

                // Validate and parse pickup date and time
                if(!DateTime.TryParseExact(buyNowRequest.PickupDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime parsedDate) ||
                    !DateTime.TryParseExact(buyNowRequest.PickupTime,"h:mm tt",CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime parsedTime)) {
                    return BadRequest("Invalid pickup date or time format. Use 'yyyy-MM-dd' for date and 'h:mm tt' for time.");
                }
                DateTime pickupDateTime = new DateTime(parsedDate.Year,parsedDate.Month,parsedDate.Day,parsedTime.Hour,parsedTime.Minute,0);

                // Validate the order type
                if(!buyNowRequest.Type.Equals("normal",StringComparison.OrdinalIgnoreCase) &&
                    !buyNowRequest.Type.Equals("rush",StringComparison.OrdinalIgnoreCase)) {
                    return BadRequest("Invalid type. Please choose 'normal' or 'rush'.");
                }

                // List to collect suborderId responses
                var responses = new List<SuborderResponse>();

                // Create and save orders for each item in the orderItem list
                foreach(var orderItem in buyNowRequest.orderItem) {


                    string designIdHex = BitConverter.ToString(orderItem.DesignId).Replace("-","").ToLower();

                    string designName = await getDesignName(designIdHex);
                    if(designIdHex == null || designIdHex.Length == 0) {
                        return BadRequest($"Design '{orderItem.DesignId}' not found.");
                    }

                    string shape = await GetDesignShapes(designIdHex);

                    string subersId = await GetPastryMaterialIdByDesignIds(designIdHex);
                    string pastryMaterialId = await GetPastryMaterialIdBySubersIdAndSize(subersId,orderItem.Size);
                    string subVariantId = await GetPastryMaterialSubVariantId(subersId,orderItem.Size);
                    string pastryId = !string.IsNullOrEmpty(subVariantId) ? subVariantId : pastryMaterialId;

                    double TotalPrice = await PriceCalculator.CalculatePastryMaterialPrice(pastryId, _context, _kaizenTables);

                    Debug.WriteLine("Total Price is: " + TotalPrice);

                    // Retrieve list of add_ons_id from pastymaterialaddons table
                    var mainVariantAddOnsId = await GetsMainVariantAddOns(pastryId,orderItem.Size);

                    // Initialize a list to store add-on IDs
                    var addOnIds = new List<string>();

                    // Check if the main variant add-ons list is empty
                    if(mainVariantAddOnsId == null || mainVariantAddOnsId.Count == 0) {
                        // If no main variant add-ons found, call GetsSubVariantAddOns instead
                        var subVariantAddOnsId = await GetsSubVariantAddOns(pastryId);

                        // Handle sub-variant add-ons as needed
                        if(subVariantAddOnsId != null && subVariantAddOnsId.Count > 0) {
                            addOnIds.AddRange(subVariantAddOnsId.Select(id => id.ToString()));
                        }
                        else {
                            return BadRequest($"No add-ons found with pastryId: {pastryMaterialId}");
                        }
                    }
                    else {
                        // Add main variant add-ons to the list
                        addOnIds.AddRange(mainVariantAddOnsId.Select(id => id.ToString()));
                    }

                    // Generate new orderId for each item
                    Guid orderId = Guid.NewGuid();
                    string orderIdBinary = ConvertGuidToBinary16(orderId.ToString()).ToLower();

                    // Insert the new order into the 'orders' table
                    await InsertOrderWithOrderId(orderIdBinary,customerUsername,customerId,pickupDateTime,buyNowRequest.Type,buyNowRequest.Payment);

                    // Generate suborderId
                    Guid suborderId = Guid.NewGuid();
                    string suborderIdBinary = ConvertGuidToBinary16(suborderId.ToString()).ToLower();

                    // Create the order object
                    var order = new Order {
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
                    await InsertsOrder(order,orderIdBinary,designIdHex,orderItem.Flavor,orderItem.Size,pastryId,customerId,orderItem.Color, shape, orderItem.Description);

                    // Add suborderId and add-ons to the response list
                    responses.Add(new SuborderResponse {
                        suborderId = suborderIdBinary,
                        pastryId = pastryId,
                        addonId = addOnIds
                    });
                }

                return Ok(responses); // Return the list of suborderIds and add-ons
            } catch(Exception ex) {
                _logger.LogError(ex,"An error occurred while processing the request.");
                return StatusCode(500,"An error occurred while processing the request.");
            }
        }

        private async Task<double> CalculatesTotalPrice(string size,int quantity,string pastryId) {

            double SubTotalprice = await STotal2(pastryId);

            Debug.WriteLine(SubTotalprice);

            string mainId = await STotal1(pastryId,size);

            double TotalPrice;

            if(SubTotalprice == 0) {
                double MainTotalprice = await Total(pastryId);
                Debug.WriteLine(MainTotalprice);

                TotalPrice = MainTotalprice * quantity;
            }
            else {
                double MainTotalprice = await Total(mainId);
                Debug.WriteLine(MainTotalprice);

                TotalPrice = (SubTotalprice + MainTotalprice) * quantity;
            }

            return TotalPrice; // Return the calculated total price
        }

        private async Task<List<int>> GetsMainVariantAddOns(string pastryMaterialId,string size) {
            var addOnsIds = new List<int>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
    SELECT pma.add_ons_id
    FROM pastrymaterialaddons pma
    JOIN pastrymaterials pm ON pm.pastry_material_id = pma.pastry_material_id
    WHERE pma.pastry_material_id = @pastryMaterialId
      AND pm.main_variant_name = @size";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@pastryMaterialId",pastryMaterialId);
                    command.Parameters.AddWithValue("@size",size);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            var addOnsId = reader.GetInt32("add_ons_id");
                            addOnsIds.Add(addOnsId);
                        }
                    }
                }
            }

            return addOnsIds;
        }

        private async Task<List<int>> GetsSubVariantAddOns(string subVariantId) {
            var addOnsIds = new List<int>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
    SELECT add_ons_id
    FROM pastrymaterialsubvariantaddons
    WHERE pastry_material_sub_variant_id = @subVariantId";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@subVariantId",subVariantId);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            var addOnsId = reader.GetInt32("add_ons_id");
                            addOnsIds.Add(addOnsId);
                        }
                    }
                }
            }

            return addOnsIds;
        }


        private async Task InsertsOrder(Order order,string orderId,string designId,string flavor,string size,string pastryId,byte[] customerId,string color,string shape,string Description) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();


                string sql = @"INSERT INTO suborders (
            suborder_id, order_id, customer_id, customer_name, employee_id, created_at, status, design_id, price, quantity, 
            last_updated_by, last_updated_at, is_active, description, flavor, size, color, shape, design_name, pastry_id) 
            VALUES (UNHEX(REPLACE(UUID(), '-', '')), UNHEX(@orderid), @customerId, @CustomerName, NULL, NOW(), @status, UNHEX(@designId), @price, 
            @quantity, NULL, NOW(), @isActive, @Description, @Flavor, @Size, @color, @shape, @DesignName, @PastryId)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderid",orderId);
                    command.Parameters.AddWithValue("@customerId",customerId);
                    command.Parameters.AddWithValue("@CustomerName",order.customerName);
                    command.Parameters.AddWithValue("@designId",designId);
                    command.Parameters.AddWithValue("@status",order.status);
                    command.Parameters.AddWithValue("@price",order.price);
                    command.Parameters.AddWithValue("@quantity",order.quantity);
                    command.Parameters.AddWithValue("@isActive",order.isActive);
                    command.Parameters.AddWithValue("@color",order.color);
                    command.Parameters.AddWithValue("@shape", shape);
                    command.Parameters.AddWithValue("@Description",order.Description);
                    command.Parameters.AddWithValue("@Flavor",flavor);
                    command.Parameters.AddWithValue("@Size",size);
                    command.Parameters.AddWithValue("@DesignName",order.designName);
                    command.Parameters.AddWithValue("@PastryId",pastryId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }



        [HttpPost("/culo-api/v1/current-user/custom-orders")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> CreateCustomOrder([FromBody] PostCustomOrder customOrder) {
            try {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if(string.IsNullOrEmpty(customerUsername)) {
                    return Unauthorized("User is not authorized");
                }

                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
                if(customerId == null || customerId.Length == 0) {
                    return BadRequest("Customer not found");
                }

                if(!DateTime.TryParseExact(customOrder.PickupDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime parsedDate) ||
                    !DateTime.TryParseExact(customOrder.PickupTime,"h:mm tt",CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime parsedTime)) {
                    return BadRequest("Invalid pickup date or time format. Use 'yyyy-MM-dd' for date and 'h:mm tt' for time.");
                }

                // Combine the parsed date and time into a single DateTime object
                DateTime pickupDateTime = new DateTime(parsedDate.Year,parsedDate.Month,parsedDate.Day,parsedTime.Hour,parsedTime.Minute,0);

                // Generate a new GUID for the OrderId
                Guid orderId = Guid.NewGuid();
                string orderIdBinary = ConvertGuidToBinary16(orderId.ToString()).ToLower();

                // Create the custom order object
                var order = new Custom {
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

                await InsertToOrderWithOrderId(orderIdBinary,customerUsername,customerId,pickupDateTime,customOrder.type);
                // Insert custom order into the database
                await InsertCustomOrder(customOrder.quantity,orderIdBinary,customerId,customerUsername,customOrder.picture,customOrder.Description,customOrder.message,customOrder.size,customOrder.tier,customOrder.cover,customOrder.color,customOrder.shape,customOrder.flavor);



                return Ok(); // Return 200 OK if the order is successfully created
            } catch(Exception ex) {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex,"An error occurred while creating the custom order");
                return StatusCode(500,"An error occurred while processing the request"); // Return 500 Internal Server Error
            }
        }


        private async Task InsertCustomOrder(int quantity,string orderId,byte[] customerId,string customerName,string pictureUrl,string description,string message,string size,string tier,string cover,string color,string shape,string flavor) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"INSERT INTO customorders ( quantity, order_id ,custom_id, customer_id, customer_name, picture_url, description, message, size, 
        tier, cover, color, shape, flavor, design_name, design_id, price, created_at, status)
    VALUES ( @quantity, UNHEX(@orderid), UNHEX(REPLACE(UUID(), '-', '')), @CustomerId, @CustomerName, @PictureUrl, @Description, @Message, @Size, 
        @Tier, @Cover, @Color, @Shape, @Flavor, NULL, UNHEX(REPLACE(UUID(), '-', '')), NULL, NOW(), 'to review')";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@quantity",quantity);
                    command.Parameters.AddWithValue("@orderid",orderId);
                    command.Parameters.AddWithValue("@CustomerId",customerId);
                    command.Parameters.AddWithValue("@CustomerName",customerName);
                    command.Parameters.AddWithValue("@PictureUrl",pictureUrl);
                    command.Parameters.AddWithValue("@Description",description);
                    command.Parameters.AddWithValue("@Message",message);
                    command.Parameters.AddWithValue("@Size",size);
                    command.Parameters.AddWithValue("@Tier",tier);
                    command.Parameters.AddWithValue("@Cover",cover);
                    command.Parameters.AddWithValue("@Color",color);
                    command.Parameters.AddWithValue("@Shape",shape);
                    command.Parameters.AddWithValue("@Flavor",flavor);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task InsertToOrderWithOrderId(string orderIdBinary,string customerName,byte[] customerId,DateTime pickupDateTime,string type) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"INSERT INTO orders (order_id, customer_id, customer_name, pickup_date, type, status, created_at, last_updated_at) 
                       VALUES (UNHEX(@orderid), @customerId, @CustomerName, @pickupDateTime, @type, 'to review', NOW(), NOW())";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderid",orderIdBinary);
                    command.Parameters.AddWithValue("@customerId",customerId);
                    command.Parameters.AddWithValue("@pickupDateTime",pickupDateTime);
                    command.Parameters.AddWithValue("@CustomerName",customerName);
                    command.Parameters.AddWithValue("@type",type);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        [HttpPost("/culo-api/v1/current-user/cart")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> CreateOrder([FromBody] OrderDTO orderDto) {
            try {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if(string.IsNullOrEmpty(customerUsername)) {
                    return Unauthorized("User is not authorized");
                }

                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
                if(customerId == null || customerId.Length == 0) {
                    return BadRequest("Customer not found");
                }

                string designIdHex = BitConverter.ToString(orderDto.DesignId).Replace("-","").ToLower();

                string designName = await getDesignName(designIdHex);
                if(designIdHex == null || designIdHex.Length == 0) {
                    return BadRequest($"Design '{orderDto.DesignId}' not found.");
                }

                string shape = await GetDesignShapes(designIdHex);

                // Get the pastry material ID using just the design ID
                string subersId = await GetPastryMaterialIdByDesignIds(designIdHex);
                string pastryMaterialId = await GetPastryMaterialIdBySubersIdAndSize(subersId,orderDto.Size);
                string subVariantId = await GetPastryMaterialSubVariantId(subersId,orderDto.Size);
                string pastryId = !string.IsNullOrEmpty(subVariantId) ? subVariantId : pastryMaterialId;

                double TotalPrice = await PriceCalculator.CalculatePastryMaterialPrice(pastryId, _context, _kaizenTables);

                Debug.WriteLine("Total Price is: " + TotalPrice);

                // List to collect suborderId responses
                var responses = new List<SuborderResponse>();

                // Retrieve list of add_ons_id from pastymaterialaddons table
                var mainVariantAddOnsId = await GetsMainVariantAddOns(pastryId,orderDto.Size);

                // Check if the main variant add-ons list is empty
                // Initialize a list to store add-on IDs
                var addOnIds = new List<string>();

                // Check if the main variant add-ons list is empty
                if(mainVariantAddOnsId == null || mainVariantAddOnsId.Count == 0) {
                    // If no main variant add-ons found, call GetsSubVariantAddOns instead
                    var subVariantAddOnsId = await GetsSubVariantAddOns(pastryId);

                    // Handle sub-variant add-ons as needed
                    if(subVariantAddOnsId != null && subVariantAddOnsId.Count > 0) {
                        addOnIds.AddRange(subVariantAddOnsId.Select(id => id.ToString()));
                    }
                    else {
                        return BadRequest($"No add-ons found with pastryId: {pastryMaterialId}");
                    }
                }
                else {
                    // Add main variant add-ons to the list
                    addOnIds.AddRange(mainVariantAddOnsId.Select(id => id.ToString()));
                }

                var order = new Order {
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
                await InsertOrder(order,designIdHex,orderDto.Flavor,orderDto.Size,pastryId,customerId,orderDto.Color, shape, orderDto.Description);

                responses.Add(new SuborderResponse {
                    suborderId = suborderIdBinary,
                    pastryId = pastryId,
                    addonId = addOnIds
                });

                return Ok(responses); // Return the list of suborderIds and add-ons
            } catch(Exception ex) {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex,"An error occurred while creating the order");
                return StatusCode(500,"An error occurred while processing the request"); // Return 500 Internal Server Error
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

        private async Task<double> CalculateTotalPrice(OrderDTO orderDto,string pastryId) {

            double SubTotalprice = await STotal2(pastryId);

            Debug.WriteLine(SubTotalprice);

            string mainId = await STotal1(pastryId,orderDto.Size);

            double TotalPrice;

            if(SubTotalprice == 0) {
                double MainTotalprice = await Total(pastryId);
                Debug.WriteLine(MainTotalprice);

                TotalPrice = MainTotalprice * orderDto.Quantity;
            }
            else {
                double MainTotalprice = await Total(mainId);
                Debug.WriteLine(MainTotalprice);

                TotalPrice = (SubTotalprice + MainTotalprice) * orderDto.Quantity;
            }

            return TotalPrice; // Return the calculated total price
        }


        // Method to get the price of items by item_id
        private async Task<List<double>> Price(int itemId) {
            var prices = new List<double>();

            // Query to retrieve price for the given itemId
            string query = "SELECT price FROM item WHERE id = @itemId";

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();
                using(var command = new MySqlCommand(query,connection)) {
                    command.Parameters.AddWithValue("@itemId",itemId);
                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            prices.Add(reader.GetDouble("price"));
                        }
                    }
                }
            }

            return prices;
        }

        // Method to calculate the total by multiplying amount by price for each item
        private async Task<double> Total(string pastryId) {
            double totalSum = 0.0;  // Initialize a variable to keep track of the total sum

            // Query to retrieve item_id and amount where pastry_material_id = @pastryId
            string query = "SELECT item_id, amount FROM ingredients WHERE pastry_material_id = @pastryId";

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();
                using(var command = new MySqlCommand(query,connection)) {
                    command.Parameters.AddWithValue("@pastryId",pastryId);
                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            int itemId = reader.GetInt32("item_id");
                            double amount = reader.GetDouble("amount");

                            // Get the prices for the given itemId by calling Price()
                            var prices = await Price(itemId);

                            // Multiply amount by each price and add the result to the total sum
                            foreach(var price in prices) {
                                totalSum += amount * price;
                            }
                        }
                    }
                }
            }

            return totalSum;  // Return the total sum
        }

        // Method to get the pastry_material_sub_variant_id by pastryId and size
        private async Task<string> STotal1(string pastryId,string size) {
            string psubId = string.Empty;

            // Query to retrieve the pastry_material_sub_variant_id based on pastryId and size
            string query = "SELECT pastry_material_id FROM pastrymaterialsubvariants WHERE pastry_material_sub_variant_id = @pastryId AND sub_variant_name = @size";

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();
                using(var command = new MySqlCommand(query,connection)) {
                    command.Parameters.AddWithValue("@pastryId",pastryId);
                    command.Parameters.AddWithValue("@size",size);
                    using(var reader = await command.ExecuteReaderAsync()) {
                        if(await reader.ReadAsync()) {
                            // Retrieve the sub-variant ID as a string
                            psubId = reader.GetString("pastry_material_id");
                        }
                    }
                }
            }

            return psubId;  // Return the sub-variant ID
        }

        // Method to get item_id and amount from pastrymaterialsubvariantingredients based on the psubId
        private async Task<double> STotal2(string psubId) {
            double totalSum = 0.0;

            // Query to retrieve item_id and amount for the given pastry_material_sub_variant_id
            string query = "SELECT item_id, amount FROM pastrymaterialsubvariantingredients WHERE pastry_material_sub_variant_id = @psubId";

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();
                using(var command = new MySqlCommand(query,connection)) {
                    command.Parameters.AddWithValue("@psubId",psubId);
                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            int itemId = reader.GetInt32("item_id");
                            double amount = reader.GetDouble("amount");

                            // Get the price for the given itemId by calling STotal3()
                            var prices = await STotal3(itemId);

                            // Multiply amount by each price and add to the total sum
                            foreach(var price in prices) {
                                totalSum += amount * price;
                            }
                        }
                    }
                }
            }

            return totalSum;  // Return the total sum
        }

        // Method to get the price for the given itemId
        private async Task<List<double>> STotal3(int itemId) {
            var prices = new List<double>();

            // Query to retrieve price for the given item_id
            string query = "SELECT price FROM item WHERE Id = @itemId";

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();
                using(var command = new MySqlCommand(query,connection)) {
                    command.Parameters.AddWithValue("@itemId",itemId);
                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            prices.Add(reader.GetDouble("price"));
                        }
                    }
                }
            }

            return prices;  // Return the list of prices
        }

        private async Task<string> GetPastryMaterialIdByDesignIds(string designIdHex) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT pastry_material_id FROM pastrymaterials WHERE design_id = UNHEX(@designId)";
                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@designId",designIdHex);

                    var result = await command.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value ? result.ToString() : null;
                }
            }
        }

        private async Task<string> GetPastryMaterialIdBySubersIdAndSize(string subersId,string size) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT pastry_material_id FROM pastrymaterials WHERE pastry_material_id = @subersId AND main_variant_name = @size";
                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@subersId",subersId);
                    command.Parameters.AddWithValue("@size",size);

                    var result = await command.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value ? result.ToString() : null;
                }
            }
        }

        private async Task InsertOrder(Order order,string designId,string flavor,string size,string pastryId,byte[] customerId,string color,string shape,string Description) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();


                string sql = @"INSERT INTO suborders (
            suborder_id, order_id, customer_id, customer_name, employee_id, created_at, status, design_id, price, quantity, 
            last_updated_by, last_updated_at, is_active, description, flavor, size, color, shape, design_name, pastry_id) 
            VALUES (
            UNHEX(REPLACE(UUID(), '-', '')), NULL, @customerId, @CustomerName, NULL, NOW(), @status, UNHEX(@designId), @price, 
            @quantity, NULL, NOW(), @isActive, @Description, @Flavor, @Size, @color, @shape, @DesignName, @PastryId)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@customerId",customerId);
                    command.Parameters.AddWithValue("@CustomerName",order.customerName);
                    command.Parameters.AddWithValue("@designId",designId);
                    command.Parameters.AddWithValue("@status",order.status);
                    command.Parameters.AddWithValue("@price",order.price);
                    command.Parameters.AddWithValue("@quantity",order.quantity);
                    command.Parameters.AddWithValue("@isActive",order.isActive);
                    command.Parameters.AddWithValue("@color",order.color);
                    command.Parameters.AddWithValue("@shape",shape);
                    command.Parameters.AddWithValue("@Description",order.Description);
                    command.Parameters.AddWithValue("@Flavor",flavor);
                    command.Parameters.AddWithValue("@Size",size);
                    command.Parameters.AddWithValue("@DesignName",order.designName);
                    command.Parameters.AddWithValue("@PastryId",pastryId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }



        [HttpPost("/culo-api/v1/current-user/cart/checkout")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> PatchOrderTypeAndPickupDate([FromBody] CheckOutRequest checkOutRequest) {
            try {
                // Retrieve customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;
                if(string.IsNullOrEmpty(customerUsername)) {
                    return Unauthorized("User is not authorized.");
                }

                // Retrieve customerId from username
                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
                if(customerId == null || customerId.Length == 0) {
                    return BadRequest("Customer not found.");
                }

                // Validate type
                if(!checkOutRequest.Type.Equals("normal",StringComparison.OrdinalIgnoreCase) &&
                    !checkOutRequest.Type.Equals("rush",StringComparison.OrdinalIgnoreCase)) {
                    return BadRequest("Invalid type. Please choose 'normal' or 'rush'.");
                }

                // Parse and validate the pickup date and time
                if(!DateTime.TryParseExact(checkOutRequest.PickupDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime parsedDate) ||
                    !DateTime.TryParseExact(checkOutRequest.PickupTime,"h:mm tt",CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime parsedTime)) {
                    return BadRequest("Invalid pickup date or time format. Use 'yyyy-MM-dd' for date and 'h:mm tt' for time.");
                }

                // Combine the parsed date and time into a single DateTime object
                DateTime pickupDateTime = new DateTime(parsedDate.Year,parsedDate.Month,parsedDate.Day,parsedTime.Hour,parsedTime.Minute,0);

                // Generate a new GUID for the OrderId (this order will be shared across multiple suborders)
                Guid orderId = Guid.NewGuid();
                string orderIdBinary = ConvertGuidToBinary16(orderId.ToString()).ToLower();

                // Insert a new order
                await InsertOrderWithOrderId(orderIdBinary,customerUsername,customerId,pickupDateTime,checkOutRequest.Type,checkOutRequest.Payment);

                // Loop through each suborderid in the request and update them with the new orderId
                foreach(var suborderId in checkOutRequest.SuborderIds) {
                    string suborderIdBinary = ConvertGuidToBinary16(suborderId.ToString()).ToLower();

                    // Check if the suborder exists in the suborders table
                    if(!await DoesSuborderExist(suborderIdBinary)) {
                        return NotFound($"Suborder with ID '{suborderId}' not found.");
                    }

                    // Update the suborder with the new orderId
                    await UpdateSuborderWithOrderId(suborderIdBinary,orderIdBinary);
                }

                return Ok($"Order for {checkOutRequest.SuborderIds.Count} suborder(s) has been successfully created with order ID '{orderIdBinary}'.");
            } catch(Exception ex) {
                _logger.LogError(ex,"An error occurred while processing the request.");
                return StatusCode(500,"An error occurred while processing the request.");
            }
        }


        private async Task<bool> DoesSuborderExist(string suborderId) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT COUNT(1) FROM suborders WHERE suborder_id = UNHEX(@suborderid)";
                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@suborderid",suborderId);
                    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
                }
            }
        }

        private async Task UpdateSuborderWithOrderId(string suborderId,string orderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                // Prepare the SQL query to update the suborder
                string sql = "UPDATE suborders SET order_id = UNHEX(@orderid), status = 'to pay' WHERE suborder_id = UNHEX(@suborderid)";
                using(var command = new MySqlCommand(sql,connection)) {
                    // Define and add the necessary parameters
                    command.Parameters.AddWithValue("@orderid",orderIdBinary);  // Use the provided orderIdBinary
                    command.Parameters.AddWithValue("@suborderid",suborderId);  // Pass the suborderId as hexadecimal

                    // Execute the query
                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        private async Task InsertOrderWithOrderId(string orderIdBinary,string customerName,byte[] customerId,DateTime pickupDateTime,string type,string payment) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"INSERT INTO orders (order_id, customer_id, customer_name, pickup_date, type, payment, status, created_at, last_updated_at) 
                       VALUES (UNHEX(@orderid), @customerId, @CustomerName, @pickupDateTime, @type, @payment, 'to pay', NOW(), NOW())";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderid",orderIdBinary);
                    command.Parameters.AddWithValue("@customerId",customerId);
                    command.Parameters.AddWithValue("@pickupDateTime",pickupDateTime);
                    command.Parameters.AddWithValue("@CustomerName",customerName);
                    command.Parameters.AddWithValue("@type",type);
                    command.Parameters.AddWithValue("@payment",payment);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        [HttpPost("/culo-api/v1/current-user/confirm-cancel-order/{orderId}")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> ConfirmOrCancelOrder(string orderId,[FromQuery] string action) {
            // Validate action parameter
            if(action != "confirm" && action != "cancel") {
                return BadRequest("Invalid action parameter. It must be 'confirm' or 'cancel'.");
            }

            if(string.IsNullOrEmpty(orderId)) {
                return BadRequest("OrderId cannot be null or empty.");
            }

            // Convert orderId to binary format for querying
            string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

            // Check if the order exists
            var orderExists = await CheckOrderExistx(orderIdBinary);

            if(!orderExists) {
                return NotFound("No orders found for the specified orderId.");
            }

            // Perform the update based on the action
            await UpdateOrderxxStatus(orderIdBinary,action);

            byte[] suborderid = await UpdateOrderxxxxStatus(orderIdBinary);

            Debug.Write(suborderid);
            if(suborderid == null) {
                return NotFound("No suborder ID found for the given order ID.");
            }

            await UpdateOrderxxxStatus(suborderid,action);


            // Return a success message based on the action
            if(action == "confirm") {
                return Ok("Order confirmed successfully.");
            }
            else if(action == "cancel") {
                return Ok("Order canceled successfully.");
            }
            else {
                return StatusCode(500,"An error occurred while updating the order status.");
            }
        }

        private async Task UpdateOrderxxStatus(string orderIdBinary,string action) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                // Determine the value of is_active based on the action
                int isActive = action.Equals("confirm",StringComparison.OrdinalIgnoreCase) ? 1 :
                               action.Equals("cancel",StringComparison.OrdinalIgnoreCase) ? 0 :
                               throw new ArgumentException("Invalid action. Please choose 'confirm' or 'cancel'.");

                string sql = "UPDATE orders SET is_active = @isActive, status = 'for approval' WHERE order_id = UNHEX(@orderId)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@isActive",isActive);
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateOrderxxxStatus(byte[] orderIdBinary,string action)//decide whether to use or nahh
        {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                // Determine the value of is_active based on the action
                int isActive = action.Equals("confirm",StringComparison.OrdinalIgnoreCase) ? 1 :
                               action.Equals("cancel",StringComparison.OrdinalIgnoreCase) ? 0 :
                               throw new ArgumentException("Invalid action. Please choose 'confirm' or 'cancel'.");

                string sql = "UPDATE suborders SET is_active = @isActive, status = 'for approval' WHERE suborder_id = @orderId";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@isActive",isActive);
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        private async Task<byte[]> UpdateOrderxxxxStatus(string orderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                // Specify the columns you want to select
                string sql = "SELECT suborder_id FROM suborders WHERE order_id = UNHEX(@orderId)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);

                    // Use ExecuteReaderAsync to execute the SELECT query
                    using(var reader = await command.ExecuteReaderAsync()) {
                        if(await reader.ReadAsync()) {
                            // Return the binary value of suborder_id directly
                            byte[] suborderIdBytes = (byte[])reader["suborder_id"];

                            // Debug.WriteLine to display the value of suborderIdBytes
                            Debug.WriteLine($"Suborder ID bytes for order ID '{orderIdBinary}': {BitConverter.ToString(suborderIdBytes)}");

                            return suborderIdBytes;
                        }
                        else {
                            // Return null or handle cases where no rows are found
                            return null;
                        }
                    }
                }
            }
        }



        private async Task<bool> CheckOrderExistx(string orderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
        SELECT COUNT(1) 
        FROM orders 
        WHERE order_id = UNHEX(@orderIdBinary)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderIdBinary",orderIdBinary);
                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        /*private async Task<bool> UpdateOrderStatus(string orderIdBinary, string action)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
UPDATE orders 
SET is_active = @isActive, status = @status 
WHERE order_id = UNHEX(@orderIdBinary);

UPDATE suborders 
SET is_active = @isActive, status = @status 
WHERE order_id = UNHEX(@orderIdBinary);";


                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderIdBinary", orderIdBinary);

                    if (action == "confirm")
                    {
                        command.Parameters.AddWithValue("@isActive", true);
                        command.Parameters.AddWithValue("@status", "confirmed");
                    }
                    else if (action == "cancel")
                    {
                        command.Parameters.AddWithValue("@isActive", false);
                        command.Parameters.AddWithValue("@status", "cancelled");
                    }

                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }*/

        [HttpPost("suborders/{suborderId}/add-ons")] //done
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> AddNewAddOnToOrder(string suborderId,[FromBody] AddNewAddOnRequest request) {
            try {
                _logger.LogInformation($"Starting AddNewAddOnToOrder for orderId: {suborderId}");

                // Convert orderId to binary(16) format without '0x' prefix
                string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

                // Retrieve the add-on details from the AddOns table based on the name
                var addOnDSOS = await GetAddOnByNameFromDatabase(request.AddOnName);
                if(addOnDSOS == null) {
                    return BadRequest($"Add-on '{request.AddOnName}' not found in the AddOns table.");
                }

                // Calculate total price
                double total = request.Quantity * addOnDSOS.PricePerUnit;

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    using(var transaction = await connection.BeginTransactionAsync()) {
                        try {
                            // Check if the add-on already exists in orderaddons
                            string selectSql = @"SELECT COUNT(*) 
                                         FROM orderaddons 
                                         WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnsId";

                            using(var selectCommand = new MySqlCommand(selectSql,connection,transaction)) {
                                selectCommand.Parameters.AddWithValue("@orderId",suborderIdBinary);
                                selectCommand.Parameters.AddWithValue("@addOnsId",addOnDSOS.AddOnId);

                                int count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                                if(count == 0) {
                                    // Insert new add-on into orderaddons
                                    string insertSql = @"INSERT INTO orderaddons (order_id, add_ons_id, quantity, total, name, price)
                                                 VALUES (UNHEX(@orderId), @addOnsId, @quantity, @total, @name, @price)";

                                    using(var insertCommand = new MySqlCommand(insertSql,connection,transaction)) {
                                        insertCommand.Parameters.AddWithValue("@orderId",suborderIdBinary);
                                        insertCommand.Parameters.AddWithValue("@addOnsId",addOnDSOS.AddOnId);
                                        insertCommand.Parameters.AddWithValue("@name",addOnDSOS.AddOnName);
                                        insertCommand.Parameters.AddWithValue("@price",addOnDSOS.PricePerUnit);
                                        insertCommand.Parameters.AddWithValue("@quantity",request.Quantity);
                                        insertCommand.Parameters.AddWithValue("@total",total);

                                        _logger.LogInformation($"Inserting add-on '{request.AddOnName}' with quantity '{request.Quantity}', price '{addOnDSOS.PricePerUnit}', and total '{total}' into orderaddons");

                                        await insertCommand.ExecuteNonQueryAsync();
                                    }
                                }
                                else {
                                    // Update existing add-on in orderaddons
                                    string updateSql = @"UPDATE orderaddons 
                                                 SET quantity = @quantity, total = @total 
                                                 WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnsId";

                                    using(var updateCommand = new MySqlCommand(updateSql,connection,transaction)) {
                                        updateCommand.Parameters.AddWithValue("@quantity",request.Quantity);
                                        updateCommand.Parameters.AddWithValue("@total",total);
                                        updateCommand.Parameters.AddWithValue("@orderId",suborderIdBinary);
                                        updateCommand.Parameters.AddWithValue("@addOnsId",addOnDSOS.AddOnId);

                                        _logger.LogInformation($"Updating add-on '{request.AddOnName}' to quantity '{request.Quantity}' and total '{total}' in orderaddons");

                                        await updateCommand.ExecuteNonQueryAsync();
                                    }
                                }
                            }

                            await transaction.CommitAsync();
                        } catch(Exception ex) {
                            _logger.LogError(ex,"Transaction failed, rolling back");
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }

                return Ok("Add-on successfully added or updated in the order.");
            } catch(Exception ex) {
                _logger.LogError(ex,$"Error adding or updating add-on to order with ID '{suborderId}'");
                return StatusCode(500,$"An error occurred while adding or updating add-on to order with ID '{suborderId}'.");
            }
        }

        private async Task<AddOnDSOS> GetAddOnByNameFromDatabase(string addOnName) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT add_ons_id, name, price FROM addons WHERE name = @name";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@name",addOnName);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        if(await reader.ReadAsync()) {
                            return new AddOnDSOS {
                                AddOnId = reader.GetInt32("add_ons_id"),
                                AddOnName = reader.GetString("name"),
                                PricePerUnit = reader.GetDouble("price")
                            };
                        }
                        else {
                            return null;
                        }
                    }
                }
            }
        }


        [HttpPost("suborders/{suborderId}/assign")]//done 
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AssignEmployeeToOrder(string suborderId,[FromBody] AssignEmp assign) {
            try {
                // Convert the orderId from GUID string to binary(16) format without '0x' prefix
                string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

                // Check if the order with the given ID exists
                bool orderExists = await CheckOrderExists(suborderIdBinary);
                if(!orderExists) {
                    return NotFound("Order does not exist. Please try another ID.");
                }

                // Check if the employee with the given username exists
                byte[] employeeId = await GetEmployeeIdByUsername(assign.name);
                if(employeeId == null || employeeId.Length == 0) {
                    return NotFound($"Employee with username '{assign}' not found. Please try another name.");
                }

                // Update the order with the employee ID and employee name
                await UpdateOrderEmployeeId(suborderIdBinary,employeeId,assign.name);

                await UpdateOrderStatusToBaking(suborderIdBinary);

                return Ok($"Employee with username '{assign}' has been successfully assigned to order with ID '{suborderId}'.");
            } catch(Exception ex) {
                _logger.LogError(ex,$"An error occurred while processing the request to assign employee to order with ID '{suborderId}'.");
                return StatusCode(500,$"An error occurred while processing the request to assign employee to order with ID '{suborderId}'.");
            }
        }

        [HttpDelete("suborders/{suborderId}/{addonId}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> DeleteOrderAddon(string suborderId,int addonId) {
            if(string.IsNullOrEmpty(suborderId)) {
                return BadRequest("SuborderId cannot be null or empty.");
            }

            // Convert suborderId to binary format for querying
            string orderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "DELETE FROM orderaddons WHERE order_id = UNHEX(@suborderId) AND add_ons_id = @addonId";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@suborderId",orderIdBinary);
                    command.Parameters.AddWithValue("@addonId",addonId);

                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    if(rowsAffected > 0) {
                        return Ok($"Addon with ID '{addonId}' successfully deleted from order '{suborderId}'.");
                    }
                    else {
                        return NotFound($"No addon found with ID '{addonId}' for order '{suborderId}'.");
                    }
                }
            }
        }


        /*[HttpPost("customer/add-to-suborder")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> CreateSubOrder([FromBody] SubOrderDTO subOrderDto, [FromQuery] string orderId, [FromQuery] string designName, [FromQuery] string description, [FromQuery] string flavor, [FromQuery] string size)
        {
            try
            {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                // Convert the orderId to binary
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Get the PickupDateTime and Type from the orders table
                DateTime? pickupDateTime = await GetOrderPickupDateTime(orderIdBinary);
                string orderType = await GetOrderType(orderIdBinary);

                if (pickupDateTime == null || orderType == null)
                {
                    return BadRequest("Order not found or invalid data.");
                }

                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
                if (customerId == null || customerId.Length == 0)
                {
                    return BadRequest("Customer not found");
                }

                byte[] designId = await GetDesignIdByDesignName(designName);
                if (designId == null || designId.Length == 0)
                {
                    return BadRequest("Design not found.");
                }

                string designIdHex = BitConverter.ToString(designId).Replace("-", "").ToLower();

                // Get the pastry material ID using just the design ID
                string subersId = await GetPastryMaterialIdByDesignIds(designIdHex);

                // Get the pastry material ID using the design ID and size
                string pastryMaterialId = await GetPastryMaterialIdBySubersIdAndSize(subersId, size);

                // Get the pastry material sub-variant ID using the pastry material ID and size
                string subVariantId = await GetPastryMaterialSubVariantId(pastryMaterialId, size);

                // Determine the appropriate pastryId to use
                string pastryId = !string.IsNullOrEmpty(subVariantId) ? subVariantId : pastryMaterialId;


                // Generate a new suborder object
                var subOrder = new SubOrder
                {
                    SubOrderId = Guid.NewGuid(),
                    OrderId = orderIdBinary,
                    CustomerName = customerUsername,
                    CreatedAt = DateTime.Now,
                    Status = "pending",
                    DesignName = designName,
                    Price = subOrderDto.Price,
                    Quantity = subOrderDto.Quantity,
                    Size = size,
                    Flavor = flavor,
                    Description = description,
                    Type = orderType, // Set Type from orders table
                    IsActive = false, // Set isActive to false for all suborders created
                    PickupDateTime = pickupDateTime.Value // Set PickupDateTime from orders table
                };

                // Insert the suborder into the database
                await InsertSubOrder(subOrder, pastryId, designId, customerId);

                return Ok(); // Return 200 OK if the suborder is successfully created
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while creating the suborder");
                return StatusCode(500, "An error occurred while processing the request"); // Return 500 Internal Server Error
            }
        }



        private async Task<DateTime?> GetOrderPickupDateTime(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT PickupDateTime FROM orders WHERE OrderId = UNHEX(@orderId)";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader.GetDateTime("PickupDateTime");
                        }
                        return null;
                    }
                }
            }
        }

        private async Task<string> GetOrderType(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT Type FROM orders WHERE OrderId = UNHEX(@orderId)";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader.GetString("Type");
                        }
                        return null;
                    }
                }
            }
        }


        private async Task InsertSubOrder(SubOrder subOrder, string pastryId, byte[] designId, byte[] customerId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string subOrderIdHex = "0x" + Guid.NewGuid().ToString("N");

                string sql = @"INSERT INTO suborders (
            suborder_id, OrderId, PastryId, CustomerId, CustomerName, EmployeeId, CreatedAt, Status, 
            DesignId, price, quantity, Size, Flavor, Description, last_updated_by, last_updated_at, type, PickupDateTime, isActive) 
            VALUES (
            @SubOrderId, UNHEX(@OrderId), @PastryId, @customerId, @CustomerName, NULL, @CreatedAt, 
            @Status, @DesingId, @Price, @Quantity, @Size, @Flavor, @Description, NULL, NULL, @Type, @PickupDateTime, @IsActive)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@SubOrderId", subOrderIdHex);
                    command.Parameters.AddWithValue("@OrderId", subOrder.OrderId);
                    command.Parameters.AddWithValue("@PastryId", pastryId);
                    command.Parameters.AddWithValue("@DesingId", designId);
                    command.Parameters.AddWithValue("@customerId", customerId);
                    command.Parameters.AddWithValue("@CustomerName", subOrder.CustomerName);
                    command.Parameters.AddWithValue("@CreatedAt", subOrder.CreatedAt);
                    command.Parameters.AddWithValue("@Status", subOrder.Status);
                    command.Parameters.AddWithValue("@Price", subOrder.Price);
                    command.Parameters.AddWithValue("@Quantity", subOrder.Quantity);
                    command.Parameters.AddWithValue("@Size", subOrder.Size);
                    command.Parameters.AddWithValue("@Flavor", subOrder.Flavor);
                    command.Parameters.AddWithValue("@Description", subOrder.Description);
                    command.Parameters.AddWithValue("@Type", subOrder.Type);
                    command.Parameters.AddWithValue("@PickupDateTime", subOrder.PickupDateTime);
                    command.Parameters.AddWithValue("@IsActive", subOrder.IsActive);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        */

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


        private async Task<byte[]> GetUserIdByAllUsername(string username) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Username = @username AND Type IN (1,2, 3, 4)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@username",username);
                    var result = await command.ExecuteScalarAsync();

                    if(result != null && result != DBNull.Value) {
                        // Return the binary value directly
                        byte[] userIdBytes = (byte[])result;

                        // Debug.WriteLine to display the value of userIdBytes
                        Debug.WriteLine($"UserId bytes for username '{username}': {BitConverter.ToString(userIdBytes)}");

                        return userIdBytes;
                    }
                    else {
                        return null; // User not found or type not matching
                    }
                }
            }
        }

        [HttpGet]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetAllOrders() {
            try {
                List<Order> orders = new List<Order>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = "SELECT suborder_id, order_id, customer_id, employee_id, created_at, pastry_id, status, HEX(design_id) as design_id, design_name, price, quantity, last_updated_by, last_updated_at, is_active, description, flavor, size, customer_name, employee_name, shape, color FROM suborders";

                    using(var command = new MySqlCommand(sql,connection)) {
                        using(var reader = await command.ExecuteReaderAsync()) {
                            while(await reader.ReadAsync()) {
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

                                orders.Add(new Order {
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
                if(orders.Count == 0)
                    return Ok(new List<Order>());

                return Ok(orders);
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("custom-orders")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetAllCustomInitialOrdersByCustomerIds([FromQuery] string? search = null) {
            try {
                List<CustomPartial> orders;

                if(!string.IsNullOrEmpty(search)) {
                    // Check if search matches a valid status
                    if(search.Equals("to review",StringComparison.OrdinalIgnoreCase)) {
                        // Fetch by status if valid search value is provided
                        orders = await FetchByStatusCustomInitialOrdersAsync(search);

                    }
                    else if(await IsEmployeeNameExistsAsync(search)) {
                        // Fetch by employee name if it exists in the database
                        orders = await FetchCustomOrdersByEmployeeInitialOrdersAsync(search);

                    }
                    else {
                        // If search does not match any valid status or employee name
                        return NotFound($"{search} not found");
                    }
                }
                else {
                    // Fetch all if no search value is provided
                    orders = await FetchInitialCustomOrdersAsync();
                }

                // If no orders are found, return an empty list
                if(orders.Count == 0)
                    return Ok(new List<toPayInitial>());

                return Ok(orders);
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }

        private async Task<List<CustomPartial>> FetchByStatusCustomInitialOrdersAsync(string status) {
            List<CustomPartial> orders = new List<CustomPartial>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
        SELECT 
            custom_id, order_id, customer_id, created_at, status, tier, shape, size, price, color, cover, picture_url, description, message, flavor, 
            design_id, design_name, quantity, customer_name
        FROM customorders 
        WHERE status = @status";

                using(var command = new MySqlCommand(sql,connection)) {
                    // Add the status parameter to the SQL command
                    command.Parameters.AddWithValue("@status",status);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                ? (Guid?)null
                : new Guid((byte[])reader["order_id"]);

                            Guid suborderId = new Guid((byte[])reader["custom_id"]);
                            Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                    ? Guid.Empty
                                                    : new Guid((byte[])reader["customer_id"]);
                            Guid designId = new Guid((byte[])reader["design_id"]);
                            double? Price = reader.IsDBNull(reader.GetOrdinal("price")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("price"));

                            orders.Add(new CustomPartial {
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

        private async Task<List<CustomPartial>> FetchCustomOrdersByEmployeeInitialOrdersAsync(string name) {
            List<CustomPartial> orders = new List<CustomPartial>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
     SELECT 
            custom_id, order_id, customer_id, created_at, status, tier, shape, size, price, color, cover, picture_url, description, message, flavor, 
            design_id, design_name, quantity, customer_name
        FROM customorders 
        WHERE employee_name = @name AND status != 'to review'";

                using(var command = new MySqlCommand(sql,connection)) {
                    // Add the status parameter to the SQL command
                    command.Parameters.AddWithValue("@name",name);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                ? (Guid?)null
                : new Guid((byte[])reader["order_id"]);

                            Guid suborderId = new Guid((byte[])reader["custom_id"]);
                            Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                    ? Guid.Empty
                                                    : new Guid((byte[])reader["customer_id"]);
                            Guid designId = new Guid((byte[])reader["design_id"]);

                            orders.Add(new CustomPartial {
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

        private async Task<List<CustomPartial>> FetchInitialCustomOrdersAsync() {
            List<CustomPartial> orders = new List<CustomPartial>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"SELECT custom_id, order_id, customer_id, created_at, status, tier, shape, size, price, color, cover, picture_url, description, message, flavor, 
            design_id, design_name, quantity, customer_name, employee_id, employee_name
        FROM customorders
WHERE status != 'to review'";

                using(var command = new MySqlCommand(sql,connection)) {
                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
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

                            orders.Add(new CustomPartial {
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
        public async Task<IActionResult> GetAllCustomOrdersByCustomerId(string customid) {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if(string.IsNullOrEmpty(customerUsername)) {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            byte[] customerId = await GetUserIdByAllUsername(customerUsername);
            if(customerId == null || customerId.Length == 0) {
                return BadRequest("Customer not found");
            }

            // Convert suborderId to binary format
            string customidBinary = ConvertGuidToBinary16((customid)).ToLower();

            // Check if the suborder exists in the suborders table
            if(!await DoesCustomOrderExist(customidBinary)) {
                return NotFound($"Suborder with ID '{customidBinary}' not found.");
            }
            Debug.Write(customidBinary);

            try {
                List<CustomOrderFull> orders = new List<CustomOrderFull>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = @"
                SELECT custom_id, order_id, customer_id, created_at, status, tier, shape, size, price, color, cover, picture_url, description, message, flavor, 
            design_id, design_name, quantity, customer_name, employee_id, employee_name
        FROM customorders 
            WHERE custom_id = UNHEX(@suborderIdBinary)";

                    using(var command = new MySqlCommand(sql,connection)) {
                        command.Parameters.AddWithValue("@suborderIdBinary",customidBinary);

                        using(var reader = await command.ExecuteReaderAsync()) {
                            while(await reader.ReadAsync()) {
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
                                orders.Add(new CustomOrderFull {
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

                    if(orders.Count > 0) {
                        // Scan the orders table using the retrieved orderId from the suborders table
                        foreach(var order in orders) {
                            if(order.orderId.HasValue) {
                                // Convert orderId to binary format
                                string orderIdBinary = ConvertGuidToBinary16(order.orderId.Value.ToString()).ToLower();
                                Debug.Write(orderIdBinary);

                                var orderDetails = await GetOrderDetailsByOrderId(connection,orderIdBinary);
                                if(orderDetails != null) {
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
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }

        private async Task<bool> DoesCustomOrderExist(string CustomorderId) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT COUNT(1) FROM customorders WHERE custom_id = UNHEX(@suborderid)";
                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@suborderid",CustomorderId);
                    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
                }
            }
        }

        [HttpGet("partial-details")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetAllInitialOrdersByCustomerIds([FromQuery] string? search = null) {
            try {
                List<AdminInitial> orders;

                if(!string.IsNullOrEmpty(search)) {
                    // Check if search matches a valid status
                    if(search.Equals("assigning artist",StringComparison.OrdinalIgnoreCase) ||
                        search.Equals("done",StringComparison.OrdinalIgnoreCase) ||
                        search.Equals("for pick up",StringComparison.OrdinalIgnoreCase) ||
                        search.Equals("for approval",StringComparison.OrdinalIgnoreCase) ||
                        search.Equals("baking",StringComparison.OrdinalIgnoreCase)) {
                        // Fetch by status if valid search value is provided
                        orders = await FetchByStatusInitialOrdersAsync(search);


                    }
                    else if(await IsEmployeeNameExistsAsync(search)) {
                        // Fetch by employee name if it exists in the database
                        orders = await FetchByEmployeeInitialOrdersAsync(search);

                    }
                    else {
                        // If search does not match any valid status or employee name
                        return NotFound($"{search} not found");
                    }
                }
                else {
                    // Fetch all if no search value is provided
                    orders = await FetchInitialOrdersAsync();

                }

                // If no orders are found, return an empty list
                if(orders.Count == 0)
                    return Ok(new List<toPayInitial>());

                return Ok(orders);
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }


        private async Task<List<AdminInitial>> FetchInitialOrdersAsync() {
            List<AdminInitial> orders = new List<AdminInitial>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @" SELECT order_id, customer_id, type, created_at, status, payment, pickup_date, last_updated_by, last_updated_at, is_active, customer_name 
                        FROM orders WHERE payment IS NOT NULL";

                using(var command = new MySqlCommand(sql,connection)) {
                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
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
                            AdminInitial order = new AdminInitial {
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
                                Type = reader.GetString(reader.GetOrdinal("payment")),
                                Pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                Status = reader.GetString(reader.GetOrdinal("status")),
                            };

                            // If the order ID is valid, fetch design details and total price
                            if(order.Id.HasValue) {
                                // Convert order ID to string and pass to FetchDesignAndTotalPriceAsync
                                string orderIdString = BitConverter.ToString(order.Id.Value.ToByteArray()).Replace("-","").ToLower();
                                List<AdminInitial> designDetails = await FetchDesignAndTotalAsync(orderIdString);

                                // Append design details (like DesignName and Total) to the order
                                if(designDetails.Any()) {
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


        private async Task<List<AdminInitial>> FetchDesignAndTotalAsync(string orderId) {
            List<AdminInitial> orders = new List<AdminInitial>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
SELECT 
    HEX(design_id) as design_id, design_name 
FROM suborders WHERE order_id = UNHEX(@orderId)";

                using(var command = new MySqlCommand(sql,connection)) {
                    // Add the status parameter to the SQL command
                    command.Parameters.AddWithValue("@orderId",orderId);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {

                            orders.Add(new AdminInitial {
                                DesignId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                            });
                        }
                    }
                }
            }

            return orders;
        }

        private async Task<List<AdminInitial>> FetchByStatusInitialOrdersAsync(string status) {
            List<AdminInitial> orders = new List<AdminInitial>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
        SELECT o.order_id, o.customer_id, o.type, o.created_at, o.status, o.payment, o.pickup_date, 
               o.last_updated_by, o.last_updated_at, o.is_active, o.customer_name 
        FROM orders o
        INNER JOIN suborders s ON o.order_id = s.order_id 
        WHERE s.status = @status AND o.payment IS NOT NULL";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@status",status);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
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
                            AdminInitial order = new AdminInitial {
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
                                Type = reader.GetString(reader.GetOrdinal("payment")),
                                Pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                Status = reader.GetString(reader.GetOrdinal("status")),
                            };

                            // If the order ID is valid, fetch design details and total price
                            if(order.Id.HasValue) {
                                // Convert order ID to string and pass to FetchDesignAndTotalPriceAsync
                                string orderIdString = BitConverter.ToString(order.Id.Value.ToByteArray()).Replace("-","").ToLower();
                                List<AdminInitial> designDetails = await FetchDesignAndTotalAsync(orderIdString);

                                // Append design details (like DesignName and Total) to the order
                                if(designDetails.Any()) {
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


        private async Task<List<AdminInitial>> FetchByEmployeeInitialOrdersAsync(string name) {
            List<AdminInitial> orders = new List<AdminInitial>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
        SELECT o.order_id, o.customer_id, o.type, o.created_at, o.status, o.payment, o.pickup_date, 
               o.last_updated_by, o.last_updated_at, o.is_active, o.customer_name 
        FROM orders o
        INNER JOIN suborders s ON o.order_id = s.order_id 
        WHERE s.employee_name = @name AND o.payment IS NOT NULL";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@name",name);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
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
                            AdminInitial order = new AdminInitial {
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
                                Type = reader.GetString(reader.GetOrdinal("payment")),
                                Pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                Status = reader.GetString(reader.GetOrdinal("status")),
                            };

                            // If the order ID is valid, fetch design details and total price
                            if(order.Id.HasValue) {
                                // Convert order ID to string and pass to FetchDesignAndTotalPriceAsync
                                string orderIdString = BitConverter.ToString(order.Id.Value.ToByteArray()).Replace("-","").ToLower();
                                List<AdminInitial> designDetails = await FetchDesignAndTotalAsync(orderIdString);

                                // Append design details (like DesignName and Total) to the order
                                if(designDetails.Any()) {
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

        private async Task<bool> IsEmployeeNameExistsAsync(string name) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"SELECT COUNT(*) FROM suborders WHERE employee_name = @name";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@name",name);

                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }


        private async Task<List<toPayInitial>> FetchByTypeInitialOrdersAsync(string type) //decide whether to use this or nahh
        {
            // First, get the list of order_ids based on the type
            var orderIdBytesList = await getType(type);

            if(orderIdBytesList == null || !orderIdBytesList.Any()) {
                return new List<toPayInitial>(); // No orders found for the given type
            }

            var orders = new List<toPayInitial>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                // Create a SQL query that uses IN to filter by the list of order_ids
                string sql = @"
        SELECT 
            suborder_id, order_id, customer_id, created_at, status, 
            HEX(design_id) as design_id, design_name, price, quantity, 
            last_updated_by, last_updated_at, is_active, customer_name, pastry_id 
        FROM suborders 
        WHERE order_id IN (" + string.Join(", ",orderIdBytesList.Select((_,i) => $"@orderId{i}")) + ")";

                using(var command = new MySqlCommand(sql,connection)) {
                    // Add parameters for each order_id
                    for(int i = 0; i < orderIdBytesList.Count; i++) {
                        command.Parameters.AddWithValue($"@orderId{i}",orderIdBytesList[i]);
                    }

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
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

                            orders.Add(new toPayInitial {
                                Id = orderId, // Handle null values for orderId
                                suborderId = suborderId,
                                CustomerId = customerIdFromDb,
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                pastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                                Price = reader.GetDouble(reader.GetOrdinal("price")),
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

            return orders; // Return the list of toPayInitial records
        }

        private async Task<List<byte[]>> getType(string type) {
            var orderIdList = new List<byte[]>(); // Initialize the list to hold the results

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                // Specify the columns you want to select
                string sql = "SELECT order_id FROM orders WHERE type = @type";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@type",type);

                    // Use ExecuteReaderAsync to execute the SELECT query
                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) // Iterate through all rows
                        {
                            // Add each binary value of order_id to the list
                            byte[] orderIdBytes = (byte[])reader["order_id"];
                            orderIdList.Add(orderIdBytes);
                        }
                    }
                }
            }

            return orderIdList; // Return the list of order_id values
        }

        [HttpGet("{orderId}/full")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetFullOrderDetailsByAdmin(string orderId) {

            string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

            try {
                // Retrieve basic order details
                var orderDetails = await GetOrderDetailx(orderIdBinary);

                if(orderDetails != null) {
                    // Retrieve suborders
                    orderDetails.OrderItems = await GetSuborderDetails(orderIdBinary);

                    // Initialize total sum
                    double totalSum = 0;

                    foreach(var suborder in orderDetails.OrderItems) {
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
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }


        [HttpGet("/culo-api/v1/current-user/cart/")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetCartOrdersByCustomerId() {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if(string.IsNullOrEmpty(customerUsername)) {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            byte[] customerId = await GetUserIdByAllUsername(customerUsername);
            if(customerId == null || customerId.Length == 0) {
                return BadRequest("Customer not found");
            }

            try {
                List<Cart> orders = new List<Cart>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = @"
            SELECT 
                suborder_id, order_id, customer_id, employee_id, created_at, status, 
                HEX(design_id) as design_id, design_name, price, quantity, 
                last_updated_by, last_updated_at, is_active, description, 
                flavor, size, customer_name, employee_name, shape, color, pastry_id 
            FROM suborders 
            WHERE customer_id = @customerId AND status IN('cart')";

                    using(var command = new MySqlCommand(sql,connection)) {
                        command.Parameters.AddWithValue("@customerId",customerId);

                        using(var reader = await command.ExecuteReaderAsync()) {
                            while(await reader.ReadAsync()) {
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

                                orders.Add(new Cart {
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
                                    Price = reader.GetDouble(reader.GetOrdinal("price")),
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
                if(orders.Count == 0)
                    return Ok(new List<Order>());

                return Ok(orders);
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("/culo-api/v1/current-user/to-pay")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToPayInitialOrdersByCustomerIds() {
            try {
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if(string.IsNullOrEmpty(customerUsername)) {
                    return Unauthorized("User is not authorized");
                }

                // Get the customer's ID using the extracted username
                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
                if(customerId == null || customerId.Length == 0) {
                    return BadRequest("Customer not found");
                }

                // Fetch all orders (no search or filtering logic)
                List<AdminInitial> orders = await FetchInitialToPayOrdersAsync(customerId);

                // If no orders are found, return an empty list
                if(orders.Count == 0)
                    return Ok(new List<toPayInitial>());

                return Ok(orders);
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}. Stack Trace: {ex.StackTrace}");

            }
        }


        private async Task<List<AdminInitial>> FetchInitialToPayOrdersAsync(byte[] customerid) {
            List<AdminInitial> orders = new List<AdminInitial>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @" SELECT order_id, customer_id, type, created_at, status, payment, pickup_date, last_updated_by, last_updated_at, is_active, customer_name 
                        FROM orders WHERE status IN ('to pay') AND customer_id = @customer_id";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@customer_id",customerid);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
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
                            AdminInitial order = new AdminInitial {
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
                            if(order.Id.HasValue) {
                                // Convert order ID to string and pass to FetchDesignAndTotalPriceAsync
                                string orderIdString = BitConverter.ToString(order.Id.Value.ToByteArray()).Replace("-","").ToLower();
                                List<AdminInitial> designDetails = await FetchDesignToPayAsync(orderIdString);

                                // Append design details (like DesignName and Total) to the order
                                if(designDetails.Any()) {
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


        private async Task<List<AdminInitial>> FetchDesignToPayAsync(string orderId) {
            List<AdminInitial> orders = new List<AdminInitial>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
SELECT 
    HEX(design_id) as design_id, design_name 
FROM suborders WHERE order_id = UNHEX(@orderId)";

                using(var command = new MySqlCommand(sql,connection)) {
                    // Add the status parameter to the SQL command
                    command.Parameters.AddWithValue("@orderId",orderId);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {

                            orders.Add(new AdminInitial {
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
        public async Task<IActionResult> GetFullOrderDetailsByCustomer(string orderId) {

            string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

            try {
                // Retrieve basic order details
                var orderDetails = await GetOrderDetailx(orderIdBinary);

                if(orderDetails != null) {
                    // Retrieve suborders
                    orderDetails.OrderItems = await GetSuborderDetails(orderIdBinary);

                    // Initialize total sum
                    double totalSum = 0;

                    foreach(var suborder in orderDetails.OrderItems) {
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
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }

        /*[HttpGet("/culo-api/v1/current-user/process/partial-details")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetInProcessInitialOrdersByCustomerIds()
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
                List<AdminInitial> orders = await FetchInitialInProcessOrdersAsync(customerId);

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


        private async Task<List<AdminInitial>> FetchInitialInProcessOrdersAsync(byte[] customerid)
        {
            List<AdminInitial> orders = new List<AdminInitial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL query to retrieve orders with suborder statuses in 'assigning artist', 'baking', 'for approval'
                string sql = @"
        SELECT o.order_id, o.customer_id, o.type, o.created_at, o.status, o.payment, o.pickup_date, 
               o.last_updated_by, o.last_updated_at, o.is_active, o.customer_name 
        FROM orders o
        INNER JOIN suborders s ON o.order_id = s.order_id 
        WHERE o.customer_id = @customer_id 
        AND s.status IN ('assigning artist', 'baking', 'for approval')";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customer_id", customerid);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Read order details from the result set
                            Guid? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                            ? (Guid?)null
                                            : new Guid((byte[])reader["order_id"]);

                            Guid customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                    ? Guid.Empty
                                                    : new Guid((byte[])reader["customer_id"]);

                            string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                            string? payment = reader.IsDBNull(reader.GetOrdinal("payment"))
                                              ? null
                                              : reader.GetString(reader.GetOrdinal("payment"));

                            string? type = reader.IsDBNull(reader.GetOrdinal("type"))
                                           ? null
                                           : reader.GetString(reader.GetOrdinal("type"));

                            string? status = reader.IsDBNull(reader.GetOrdinal("status"))
                                             ? null
                                             : reader.GetString(reader.GetOrdinal("status"));

                            string? customerName = reader.IsDBNull(reader.GetOrdinal("customer_name"))
                                                   ? null
                                                   : reader.GetString(reader.GetOrdinal("customer_name"));

                            DateTime? pickupDate = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                                   ? (DateTime?)null
                                                   : reader.GetDateTime(reader.GetOrdinal("pickup_date"));

                            DateTime? lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                      ? (DateTime?)null
                                                      : reader.GetDateTime(reader.GetOrdinal("last_updated_at"));

                            // Initialize an AdminInitial object with order details
                            AdminInitial order = new AdminInitial
                            {
                                Id = orderId,
                                CustomerId = customerIdFromDb,
                                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                lastUpdatedBy = lastUpdatedBy ?? "",
                                lastUpdatedAt = lastUpdatedAt,
                                isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                CustomerName = customerName,
                                Payment = payment,
                                Type = type,
                                Pickup = pickupDate,
                                Status = status
                            };

                            // Fetch design details if order ID is valid
                            if (order.Id.HasValue)
                            {
                                string orderIdString = BitConverter.ToString(order.Id.Value.ToByteArray()).Replace("-", "").ToLower();
                                List<AdminInitial> designDetails = await FetchDesignInProcessAsync(orderIdString);

                                // Append design details to the order
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



        private async Task<List<AdminInitial>> FetchDesignInProcessAsync(string orderId)
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

        [HttpGet("/culo-api/v1/current-user/to-receive/partial-details")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToReceiveInitialOrdersByCustomerIds()
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
                List<AdminInitial> orders = await FetchInitialToReceiveOrdersAsync(customerId);

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


        private async Task<List<AdminInitial>> FetchInitialToReceiveOrdersAsync(byte[] customerid)
        {
            List<AdminInitial> orders = new List<AdminInitial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @" SELECT order_id, customer_id, type, created_at, status, payment, pickup_date, last_updated_by, last_updated_at, is_active, customer_name 
                        FROM orders WHERE status IN ('for pick up') AND customer_id = @customer_id";

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
                                Type = reader.GetString(reader.GetOrdinal("payment")),
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
        
        /*[HttpGet("/culo-api/v1/current-user/to-pay/partial-details")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToPayInitialOrdersByCustomerIds()
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
    WHERE customer_id = @customerId AND status IN('to pay')";

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

                                orders.Add(new toPayInitial
                                {
                                    Id = orderId, // Handle null values for orderId
                                    suborderId = suborderId,
                                    CustomerId = customerIdFromDb,
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    pastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                    DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                                    Price = reader.GetDouble(reader.GetOrdinal("price")),
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

        [HttpGet("/culo-api/v1/current-user/to-pay/full-details/{suborderid}")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToPayOrdersByCustomerId(string suborderid)
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
            WHERE customer_id = @customerId AND status IN ('to pay') AND suborder_id = UNHEX(@suborderIdBinary)";

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
        */

        [HttpGet("/culo-api/v1/current-user/to-process")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToProcessInitialOrdersByCustomerIds() {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if(string.IsNullOrEmpty(customerUsername)) {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            byte[] customerId = await GetUserIdByAllUsername(customerUsername);
            if(customerId == null || customerId.Length == 0) {
                return BadRequest("Customer not found");
            }

            try {
                List<toPayInitial> orders = new List<toPayInitial>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = @"
    SELECT 
        suborder_id, order_id, customer_id, created_at, status, 
        HEX(design_id) as design_id, design_name, price, quantity, 
        last_updated_by, last_updated_at, is_active, customer_name, pastry_id 
    FROM suborders 
    WHERE customer_id = @customerId AND status IN('assigning artist', 'baking', 'for approval')";

                    using(var command = new MySqlCommand(sql,connection)) {
                        command.Parameters.AddWithValue("@customerId",customerId);

                        using(var reader = await command.ExecuteReaderAsync()) {
                            while(await reader.ReadAsync()) {
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

                                orders.Add(new toPayInitial {
                                    Id = orderId, // Handle null values for orderId
                                    suborderId = suborderId,
                                    CustomerId = customerIdFromDb,
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    pastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                    DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                                    Price = reader.GetDouble(reader.GetOrdinal("price")),
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
                if(orders.Count == 0)
                    return Ok(new List<toPayInitial>());

                return Ok(orders);
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("/culo-api/v1/current-user/to-process/{suborderid}/full")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToProcessOrdersByCustomerId(string suborderid) {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if(string.IsNullOrEmpty(customerUsername)) {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            byte[] customerId = await GetUserIdByAllUsername(customerUsername);
            if(customerId == null || customerId.Length == 0) {
                return BadRequest("Customer not found");
            }

            // Convert suborderId to binary format
            string suborderIdBinary = ConvertGuidToBinary16((suborderid)).ToLower();

            // Check if the suborder exists in the suborders table
            if(!await DoesSuborderExist(suborderIdBinary)) {
                return NotFound($"Suborder with ID '{suborderIdBinary}' not found.");
            }
            Debug.Write(suborderIdBinary);

            try {
                List<Full> orders = new List<Full>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = @"
    SELECT 
                suborder_id, order_id, customer_id, employee_id, created_at, status, 
                HEX(design_id) as design_id, design_name, price, quantity, 
                last_updated_by, last_updated_at, is_active, description, 
                flavor, size, customer_name, employee_name, shape, color, pastry_id 
            FROM suborders 
            WHERE customer_id = @customerId AND status IN ('assigning artist','baking', 'for approval') AND suborder_id = UNHEX(@suborderIdBinary)";

                    using(var command = new MySqlCommand(sql,connection)) {
                        command.Parameters.AddWithValue("@customerId",customerId);
                        command.Parameters.AddWithValue("@suborderIdBinary",suborderIdBinary);

                        using(var reader = await command.ExecuteReaderAsync()) {
                            while(await reader.ReadAsync()) {
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

                                orders.Add(new Full {
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

                    if(orders.Count > 0) {
                        // Scan the orders table using the retrieved orderId from the suborders table
                        foreach(var order in orders) {
                            if(order.orderId.HasValue) {
                                // Convert orderId to binary format
                                string orderIdBinary = ConvertGuidToBinary16(order.orderId.Value.ToString()).ToLower();
                                Debug.Write(orderIdBinary);

                                var orderDetails = await GetOrderDetailsByOrderId(connection,orderIdBinary);
                                if(orderDetails != null) {
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
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("/culo-api/v1/current-user/to-receive")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToReceiveInitialOrdersByCustomerId() {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if(string.IsNullOrEmpty(customerUsername)) {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            byte[] customerId = await GetUserIdByAllUsername(customerUsername);
            if(customerId == null || customerId.Length == 0) {
                return BadRequest("Customer not found");
            }

            try {
                List<toPayInitial> orders = new List<toPayInitial>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = @"
    SELECT 
        suborder_id, order_id, customer_id, created_at, status, 
        HEX(design_id) as design_id, design_name, price, quantity, 
        last_updated_by, last_updated_at, is_active, customer_name, pastry_id 
    FROM suborders 
    WHERE customer_id = @customerId AND status IN('for pick up')";

                    using(var command = new MySqlCommand(sql,connection)) {
                        command.Parameters.AddWithValue("@customerId",customerId);

                        using(var reader = await command.ExecuteReaderAsync()) {
                            while(await reader.ReadAsync()) {
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

                                orders.Add(new toPayInitial {
                                    Id = orderId, // Handle null values for orderId
                                    suborderId = suborderId,
                                    CustomerId = customerIdFromDb,
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    pastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                    DesignName = reader.GetString(reader.GetOrdinal("design_name")),
                                    Price = reader.GetDouble(reader.GetOrdinal("price")),
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
                if(orders.Count == 0)
                    return Ok(new List<toPayInitial>());

                return Ok(orders);
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("/culo-api/v1/current-user/to-receive/{suborderid}/full")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToReceiveOrdersByCustomerId(string suborderid) {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if(string.IsNullOrEmpty(customerUsername)) {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            byte[] customerId = await GetUserIdByAllUsername(customerUsername);
            if(customerId == null || customerId.Length == 0) {
                return BadRequest("Customer not found");
            }

            // Convert suborderId to binary format
            string suborderIdBinary = ConvertGuidToBinary16((suborderid)).ToLower();

            // Check if the suborder exists in the suborders table
            if(!await DoesSuborderExist(suborderIdBinary)) {
                return NotFound($"Suborder with ID '{suborderIdBinary}' not found.");
            }
            Debug.Write(suborderIdBinary);

            try {
                List<Full> orders = new List<Full>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = @"
    SELECT 
                suborder_id, order_id, customer_id, employee_id, created_at, status, 
                HEX(design_id) as design_id, design_name, price, quantity, 
                last_updated_by, last_updated_at, is_active, description, 
                flavor, size, customer_name, employee_name, shape, color, pastry_id 
            FROM suborders 
            WHERE customer_id = @customerId AND status IN ('for pick up') AND suborder_id = UNHEX(@suborderIdBinary)";

                    using(var command = new MySqlCommand(sql,connection)) {
                        command.Parameters.AddWithValue("@customerId",customerId);
                        command.Parameters.AddWithValue("@suborderIdBinary",suborderIdBinary);

                        using(var reader = await command.ExecuteReaderAsync()) {
                            while(await reader.ReadAsync()) {
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

                                orders.Add(new Full {
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

                    if(orders.Count > 0) {
                        // Scan the orders table using the retrieved orderId from the suborders table
                        foreach(var order in orders) {
                            if(order.orderId.HasValue) {
                                // Convert orderId to binary format
                                string orderIdBinary = ConvertGuidToBinary16(order.orderId.Value.ToString()).ToLower();
                                Debug.Write(orderIdBinary);

                                var orderDetails = await GetOrderDetailsByOrderId(connection,orderIdBinary);
                                if(orderDetails != null) {
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
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }

        private async Task<OrderDetails?> GetOrderDetailsByOrderId(MySqlConnection connection,string orderIdBinary) {
            string sql = @"
        SELECT order_id, status, payment, type, pickup_date 
        FROM orders 
        WHERE order_id = UNHEX(@orderIdBinary)";

            using(var command = new MySqlCommand(sql,connection)) {
                command.Parameters.AddWithValue("@orderIdBinary",orderIdBinary);

                using(var reader = await command.ExecuteReaderAsync()) {
                    if(await reader.ReadAsync()) {
                        return new OrderDetails {
                            orderId = new Guid((byte[])reader["order_id"]),
                            Status = reader.GetString(reader.GetOrdinal("status")),
                            payment = reader.GetString(reader.GetOrdinal("payment")),
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

        [HttpGet("current-user/artist/to-do")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrderxByCustomerId() {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if(string.IsNullOrEmpty(customerUsername)) {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            byte[] customerId = await GetUserIdByAllUsername(customerUsername);
            if(customerId == null || customerId.Length == 0) {
                return BadRequest("Customer not found");
            }

            try {
                List<Cart> orders = new List<Cart>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = @"
            SELECT 
                suborder_id, order_id, customer_id, employee_id, created_at, status, 
                HEX(design_id) as design_id, design_name, price, quantity, 
                last_updated_by, last_updated_at, is_active, description, 
                flavor, size, customer_name, employee_name, shape, color, pastry_id 
            FROM suborders 
            WHERE employee_id = @customerId AND status IN('confirmed')";

                    using(var command = new MySqlCommand(sql,connection)) {
                        command.Parameters.AddWithValue("@customerId",customerId);

                        using(var reader = await command.ExecuteReaderAsync()) {
                            while(await reader.ReadAsync()) {
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

                                orders.Add(new Cart {
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
                                    Price = reader.GetDouble(reader.GetOrdinal("price")),
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
                if(orders.Count == 0)
                    return Ok(new List<Order>());

                return Ok(orders);
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("{orderId}/final-details")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetFinalOrderDetailsByOrderId(string orderId) {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if(string.IsNullOrEmpty(customerUsername)) {
                return Unauthorized("User is not authorized");
            }

            byte[] customerId = await GetUserIdByAllUsername(customerUsername);
            if(customerId == null || customerId.Length == 0) {
                return BadRequest("Customer not found");
            }

            string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

            try {
                // Retrieve basic order details
                var orderDetails = await GetOrderDetailx(orderIdBinary);

                if(orderDetails != null) {
                    // Retrieve suborders
                    orderDetails.OrderItems = await GetSuborderDetails(orderIdBinary);

                    // Initialize total sum
                    double totalSum = 0;

                    foreach(var suborder in orderDetails.OrderItems) {
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
            } catch(Exception ex) {
                return StatusCode(500,$"An error occurred: {ex.Message}");
            }
        }


        private async Task<CheckOutDetails> GetOrderDetailx(string orderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string orderSql = @"
    SELECT order_id, status, payment, type, pickup_date 
    FROM orders 
    WHERE order_id = UNHEX(@orderIdBinary)";

                using(var command = new MySqlCommand(orderSql,connection)) {
                    command.Parameters.AddWithValue("@orderIdBinary",orderIdBinary);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        if(await reader.ReadAsync()) {
                            return new CheckOutDetails {
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

        private async Task<List<OrderItem>> GetSuborderDetails(string orderIdBinary) {
            var suborders = new List<OrderItem>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string suborderSql = @"
    SELECT 
        suborder_id, order_id, customer_id, employee_id, created_at, status, 
        HEX(design_id) as design_id, design_name, price, quantity, 
        last_updated_by, last_updated_at, is_active, description, 
        flavor, size, customer_name, employee_name, shape, color, pastry_id 
    FROM suborders 
    WHERE order_id = UNHEX(@orderIdBinary)";

                using(var command = new MySqlCommand(suborderSql,connection)) {
                    command.Parameters.AddWithValue("@orderIdBinary",orderIdBinary);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            var suborder = new OrderItem {
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

        private async Task<List<OrderAddon1>> GetOrderAddonsDetails(Guid suborderId) {
            var addons = new List<OrderAddon1>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string addOnsSql = @"
    SELECT add_ons_id, quantity, total, name, price
    FROM orderaddons
    WHERE order_id = UNHEX(@suborderIdBinary)";

                using(var command = new MySqlCommand(addOnsSql,connection)) {
                    string subId = BitConverter.ToString(suborderId.ToByteArray()).Replace("-","").ToLower();
                    command.Parameters.AddWithValue("@suborderIdBinary",subId);
                    Debug.Write("suborderId: " + subId);
                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            addons.Add(new OrderAddon1 {
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





        /*[HttpGet("current-user/for-confirmation-orders")]
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
        */

        [HttpGet("employees-name")] //done
        public async Task<IActionResult> GetEmployeesOfType2() {
            try {
                List<employee> employees = new List<employee>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = @"SELECT Username AS Name, UserId FROM users WHERE Type = 2";

                    using(var command = new MySqlCommand(sql,connection)) {
                        using(var reader = await command.ExecuteReaderAsync()) {
                            while(await reader.ReadAsync()) {
                                Guid userId = reader.IsDBNull(reader.GetOrdinal("UserId"))
                                                        ? Guid.Empty
                                                        : new Guid((byte[])reader["UserId"]);
                                employee employee = new employee {
                                    name = reader.GetString("Name"),
                                    userId = userId

                                };

                                employees.Add(employee);
                            }
                        }
                    }
                }
                // If no orders are found, return an empty list
                if(employees.Count == 0)
                    return Ok(new List<employee>());

                return Ok(employees);
            } catch(Exception ex) {
                return StatusCode(500,$"Failed to retrieve employees: {ex.Message}");
            }
        }

        [HttpGet("{suborderId}/add-ons")] //done (might remove this)
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetAddOnsByOrderId(string suborderId) {
            try {
                // Convert orderId to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

                // Fetch DesignId and Size for the given orderId
                var (designIdHex, size) = await GetDesignIdAndSizeByOrderId(orderIdBinary);

                if(string.IsNullOrEmpty(designIdHex)) {
                    return NotFound($"No DesignId found for order with ID '{suborderId}'.");
                }

                // Fetch pastry_material_id using DesignId
                string pastryMaterialId = await GetPastryMaterialIdByDesignId(designIdHex);

                if(pastryMaterialId == null) {
                    return NotFound($"No pastry material found for designId '{designIdHex}'.");
                }

                // Retrieve list of add_ons_id and amount from pastymaterialaddons table
                var mainVariantAddOns = await GetMainVariantAddOns(pastryMaterialId,size);

                // Retrieve pastry_material_sub_variant_id from pastrymaterialsubvariants table
                string subVariantId = await GetPastryMaterialSubVariantId(pastryMaterialId,size);

                // Retrieve list of add_ons_id and amount from pastrymaterialsubvariantaddons table
                var subVariantAddOns = subVariantId != null ? await GetSubVariantAddOns(subVariantId) : new List<(int, int)>();

                var allAddOns = new Dictionary<int,int>();
                foreach(var (addOnsId, amount) in mainVariantAddOns) {
                    if(allAddOns.ContainsKey(addOnsId)) {
                        allAddOns[addOnsId] += amount;
                    }
                    else {
                        allAddOns[addOnsId] = amount;
                    }
                }

                foreach(var (addOnsId, amount) in subVariantAddOns) {
                    if(allAddOns.ContainsKey(addOnsId)) {
                        allAddOns[addOnsId] += amount;
                    }
                    else {
                        allAddOns[addOnsId] = amount;
                    }
                }

                var addOns = new List<AddOnDPOS>();

                foreach(var addOnsId in allAddOns.Keys) {
                    var details = await GetAddOnsDetailsByAddOnsId(addOnsId);
                    foreach(var detail in details) {
                        detail.Quantity = allAddOns[addOnsId]; // Set quantity from the combined total
                        detail.AddOnId = addOnsId; // Set the AddOnId
                        addOns.Add(detail);
                    }
                }

                if(addOns.Count == 0) {
                    return NotFound($"No add-ons found for pastry material ID '{pastryMaterialId}' with Size '{size}'.");
                }

                // Prepare the response object
                var response = new orderAddons {
                    pastryId = pastryMaterialId,
                    addOnDPOs = addOns
                };

                return Ok(response);
            } catch(Exception ex) {
                _logger.LogError(ex,$"Error retrieving add-ons for order with ID '{suborderId}'");
                return StatusCode(500,$"An error occurred while retrieving add-ons for order with ID '{suborderId}'.");
            }
        }



        private async Task<(string designIdHex, string size)> GetDesignIdAndSizeByOrderId(string orderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT design_id, size FROM suborders WHERE suborder_id = UNHEX(@orderId)";
                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        if(await reader.ReadAsync()) {
                            byte[] designIdBinary = (byte[])reader["design_id"];
                            string designIdHex = BitConverter.ToString(designIdBinary).Replace("-","").ToLower();
                            string size = reader.GetString("size");

                            return (designIdHex, size);
                        }
                        else {
                            return (null, null); // Order not found or does not have a design associated
                        }
                    }
                }
            }
        }

        private async Task<string> GetPastryMaterialIdByDesignId(string designIdHex) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT pastry_material_id FROM pastrymaterials WHERE design_id = UNHEX(@designId)";
                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@designId",designIdHex);

                    var result = await command.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value ? result.ToString() : null;
                }
            }
        }

        private async Task<List<(int addOnsId, int quantity)>> GetMainVariantAddOns(string pastryMaterialId,string size) {
            var addOns = new List<(int, int)>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
            SELECT pma.add_ons_id, pma.amount
            FROM pastymaterialaddons pma
            JOIN pastrymaterials pm ON pm.pastry_material_id = pma.pastry_material_id
            WHERE pma.pastry_material_id = @pastryMaterialId
              AND pm.main_variant_name = @size";
                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@pastryMaterialId",pastryMaterialId);
                    command.Parameters.AddWithValue("@size",size);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            var addOnsId = reader.GetInt32("add_ons_id");
                            var quantity = reader.GetInt32("amount");
                            addOns.Add((addOnsId, quantity));
                        }
                    }
                }
            }

            return addOns;
        }

        private async Task<string> GetPastryMaterialSubVariantId(string pastryMaterialId,string size) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
            SELECT pastry_material_sub_variant_id
            FROM pastrymaterialsubvariants
            WHERE pastry_material_id = @pastryMaterialId
              AND sub_variant_name = @size";
                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@pastryMaterialId",pastryMaterialId);
                    command.Parameters.AddWithValue("@size",size);

                    var subVariantId = await command.ExecuteScalarAsync();
                    return subVariantId?.ToString();
                }
            }
        }

        private async Task<List<(int addOnsId, int quantity)>> GetSubVariantAddOns(string subVariantId) {
            var addOns = new List<(int, int)>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
            SELECT add_ons_id, amount
            FROM pastrymaterialsubvariantaddons
            WHERE pastry_material_sub_variant_id = @subVariantId";
                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@subVariantId",subVariantId);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            var addOnsId = reader.GetInt32("add_ons_id");
                            var quantity = reader.GetInt32("amount");
                            addOns.Add((addOnsId, quantity));
                        }
                    }
                }
            }

            return addOns;
        }

        private async Task<List<AddOnDPOS>> GetAddOnsDetailsByAddOnsId(int addOnsId) {
            var addOns = new List<AddOnDPOS>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT name, price FROM addons WHERE add_ons_id = @addOnsId";
                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@addOnsId",addOnsId);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            var addOn = new AddOnDPOS {
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



        [HttpGet("total-orders")] //done
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetTotalQuantities() {
            try {
                TotalOrders totalQuantities = new TotalOrders();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = "SELECT SUM(quantity) AS TotalQuantity FROM suborders WHERE is_active = TRUE";

                    using(var command = new MySqlCommand(sql,connection)) {
                        var result = await command.ExecuteScalarAsync();
                        if(result != null && result != DBNull.Value) {
                            totalQuantities.Total = Convert.ToInt32(result);
                        }
                    }
                }

                return Ok(totalQuantities);
            } catch(Exception ex) {
                _logger.LogError(ex,"An error occurred while summing the quantities.");
                return StatusCode(StatusCodes.Status500InternalServerError,"An error occurred while summing the quantities.");
            }
        }

        /*[HttpGet("customer/final-order-details/{orderId}")] //debug this soon
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrderByOrderId(string orderId)
        {
            try
            {
                // Convert the hex string to a binary(16) formatted string
                string binary16OrderId = ConvertGuidToBinary16(orderId).ToLower();

                // Fetch the specific order and its suborders/addons from the database
                FinalOrder finalOrder = await GetFinalOrderByIdFromDatabase(binary16OrderId);

                if (finalOrder == null)
                {
                    return NotFound($"Order with orderId {orderId} not found.");
                }

                // Retrieve DesignId and Size from suborders
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

                // Calculate the total price of all suborders
                double totalSuborderPrice = finalOrder.summary.Sum(suborder => suborder.Price * suborder.Quantity);

                // Calculate the total from orderaddons
                double totalFromOrderAddons = await GetTotalFromOrderAddons(binary16OrderId);

                // Set the finalOrder allTotal as the sum of all suborders' prices and add-ons
                finalOrder.allTotal = totalSuborderPrice + totalFromOrderAddons;

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

                // Query to get order and suborder details
                string orderSql = @"
        SELECT o.OrderId, o.PickupDateTime, o.CustomerName, o.status, o.payment, 
               o.last_updated_by, o.last_updated_at, o.type, o.isActive,
               s.suborder_id, s.PastryId, s.CustomerId, s.EmployeeId, s.EmployeeName,
               s.DesignId, s.DesignName, s.price, s.quantity, s.Size, s.Flavor,
               s.color, s.shape, s.tier, s.Description, s.CreatedAt, s.Status AS SubOrderStatus
        FROM orders o
        JOIN suborders s ON o.OrderId = s.OrderId
        WHERE o.OrderId = UNHEX(@orderId)";

                // Query to get add-ons details from orderaddons
                string addOnsSql = @"
        SELECT addOnsId, quantity, Total
        FROM orderaddons
        WHERE OrderId = UNHEX(@orderId)";

                FinalOrder finalOrder = null;
                List<OrderSummary> orderSummaries = new List<OrderSummary>();

                // Get order and suborder details
                using (var orderCommand = new MySqlCommand(orderSql, connection))
                {
                    orderCommand.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await orderCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (finalOrder == null)
                            {
                                // Initialize FinalOrder object
                                finalOrder = new FinalOrder
                                {
                                    OrderId = orderId,
                                    PickupDateTime = reader.GetDateTime("PickupDateTime"),
                                    CustomerName = reader.GetString("CustomerName"),
                                    status = reader.GetString("status"),
                                    payment = reader.GetString("payment"),
                                    lastUpdatedBy = reader.GetString("last_updated_by"),
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at")) ? (DateTime?)null : reader.GetDateTime("last_updated_at"),
                                    Type = reader.GetString("type"),
                                    IsActive = reader.GetBoolean("isActive"),
                                    summary = new List<OrderSummary>(),
                                    AddOns = new List<AddOnDetails2>(),
                                    customAddons = new List<CustomAddons>()
                                };
                            }

                            // Add each suborder to the summary list
                            orderSummaries.Add(new OrderSummary
                            {
                                suborderId = reader.GetGuid("suborder_id"),
                                PastryMaterialId = reader.GetString("PastryId"),
                                CustomerId = reader.GetGuid("CustomerId"),
                                employeeId = reader.IsDBNull(reader.GetOrdinal("EmployeeId")) ? (Guid?)null : reader.GetGuid("EmployeeId"),
                                employeeName = reader.IsDBNull(reader.GetOrdinal("EmployeeName")) ? string.Empty : reader.GetString("EmployeeName"),
                                designId = reader.IsDBNull(reader.GetOrdinal("DesignId")) ? null : (byte[]?)reader["DesignId"],
                                DesignName = reader.GetString("DesignName"),
                                Price = reader.GetDouble("price"),
                                Quantity = reader.GetInt32("quantity"),
                                Size = reader.GetString("Size"),
                                Flavor = reader.GetString("Flavor"),
                                color = reader.GetString("color"),
                                shape = reader.GetString("shape"),
                                tier = reader.GetString("tier"),
                                Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? string.Empty : reader.GetString("Description"),
                                CreatedAt = reader.GetDateTime("CreatedAt"),
                                Status = reader.GetString("SubOrderStatus")
                            });
                        }
                    }
                }

                // Assign order summaries to the finalOrder
                if (finalOrder != null)
                {
                    finalOrder.summary = orderSummaries;

                    // Get add-ons details
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
        */



        [HttpGet("/culo-api/v1/current-user/type")] //update this 
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
        public async Task<IActionResult> GetOrdersByType([FromQuery] string type) {
            try {
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if(string.IsNullOrEmpty(customerUsername)) {
                    return Unauthorized("User is not authorized");
                }

                byte[] customerId = await GetUserIdByAllUsername(customerUsername);
                if(customerId == null || customerId.Length == 0) {
                    return BadRequest("Customer not found");
                }

                List<Order> orders = new List<Order>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    connection.Open();

                    string sql = "SELECT * FROM suborders s JOIN orders o ON s.order_id = o.order_id WHERE o.type = @type";



                    using(var command = new MySqlCommand(sql,connection)) {
                        command.Parameters.AddWithValue("@type",type);

                        using(var reader = command.ExecuteReader()) {
                            while(reader.Read()) {
                                Guid employeeId = Guid.Empty; // Initialize to empty Guid

                                // Check for DBNull value before casting to Guid
                                if(reader["employee_id"] != DBNull.Value) {
                                    employeeId = (Guid)reader["employee_id"];
                                }

                                orders.Add(new Order {
                                    suborderId = new Guid((byte[])reader["suborder_id"]),
                                    orderId = new Guid((byte[])reader["order_id"]),
                                    customerId = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                    ? Guid.Empty
                                    : new Guid((byte[])reader["customer_id"]),
                                    employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                    ? Guid.Empty
                                    : new Guid((byte[])reader["employee_id"]),
                                    employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                    ? string.Empty
                                    : reader.GetString(reader.GetOrdinal("employee_name")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    status = reader.GetString(reader.GetOrdinal("status")),
                                    pastryId = reader.GetString(reader.GetOrdinal("pastry_id")),
                                    color = reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.GetString(reader.GetOrdinal("shape")),
                                    tier = reader.GetString(reader.GetOrdinal("tier")),
                                    designId = FromHexString(reader.GetString(reader.GetOrdinal("design_id"))),
                                    designName = reader.GetString(reader.GetOrdinal("design_name")),
                                    price = reader.GetDouble(reader.GetOrdinal("price")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                    ? string.Empty
                                    : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                    ? (DateTime?)null
                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    Description = reader.GetString(reader.GetOrdinal("description")),
                                    flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                    size = reader.GetString(reader.GetOrdinal("size")),
                                    customerName = reader.GetString(reader.GetOrdinal("customer_name")),

                                });
                            }
                        }
                    }
                }

                if(orders.Count == 0) {
                    return NotFound("No orders found for the specified type.");
                }

                return Ok(orders);
            } catch(Exception ex) {
                return StatusCode(StatusCodes.Status500InternalServerError,$"An error occurred while fetching orders by type: {ex.Message}");
            }
        }

        private async Task<string> GetLastupdater(string username) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT Username FROM users WHERE Username = @username AND Type IN(3,4)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@username",username);
                    var result = await command.ExecuteScalarAsync();

                    if(result != null && result != DBNull.Value) {
                        // Return the binary value directly
                        string user = (string)result;

                        // Debug.WriteLine to display the value of userIdBytes
                        Debug.WriteLine($"username: '{username}'");

                        return user;
                    }
                    else {
                        return null; // Employee not found or not of type 2 or 3
                    }
                }
            }
        }

        private async Task<byte[]> GetUserIdByUsername(string username) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Username = @username AND Type IN (2, 3)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@username",username);
                    var result = await command.ExecuteScalarAsync();

                    if(result != null && result != DBNull.Value) {
                        // Return the binary value directly
                        byte[] userIdBytes = (byte[])result;

                        // Debug.WriteLine to display the value of userIdBytes
                        Debug.WriteLine($"UserId bytes for username '{username}': {BitConverter.ToString(userIdBytes)}");

                        return userIdBytes;
                    }
                    else {
                        return null; // Employee not found or not of type 2 or 3
                    }
                }
            }
        }




        private async Task<List<Order>> GetOrdersByEmployeeId(byte[] employeeIdBytes) {
            List<Order> orders = new List<Order>();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT * FROM orders WHERE EmployeeId = @employeeId";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@employeeId",employeeIdBytes);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        while(await reader.ReadAsync()) {
                            orders.Add(new Order {
                                orderId = reader.GetGuid(reader.GetOrdinal("OrderId")),
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



        [HttpGet("admin/by-customer-username/{customerUsername}")] //update this
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Artist + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrdersByCustomerUsername(string customerUsername) {
            try {
                // Get the user ID based on the provided customer username
                byte[] userIdBytes = await GetUserIdByCustomerUsername(customerUsername);

                // If the user ID is empty, return NotFound
                if(userIdBytes == null) {
                    return NotFound($"User with username '{customerUsername}' not found.");
                }

                List<Order> orders = new List<Order>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = "SELECT * FROM orders WHERE CustomerId = @userId";

                    using(var command = new MySqlCommand(sql,connection)) {
                        command.Parameters.AddWithValue("@userId",userIdBytes);

                        using(var reader = await command.ExecuteReaderAsync()) {
                            while(await reader.ReadAsync()) {
                                orders.Add(new Order {
                                    orderId = reader.GetGuid(reader.GetOrdinal("OrderId")),
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

                if(orders.Count == 0) {
                    return NotFound($"No orders found for the user with username '{customerUsername}'.");
                }

                return Ok(orders);
            } catch(Exception ex) {
                _logger.LogError(ex,$"An error occurred while fetching orders for username '{customerUsername}'");
                return StatusCode(StatusCodes.Status500InternalServerError,$"An error occurred while fetching orders for username '{customerUsername}': {ex.Message}");
            }
        }


        private async Task<byte[]> GetUserIdByCustomerUsername(string customerUsername) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Username = @username AND Type <= 1";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@username",customerUsername);
                    var result = await command.ExecuteScalarAsync();

                    if(result != null && result != DBNull.Value) {
                        // Return the binary value directly
                        byte[] userIdBytes = (byte[])result;

                        // Debug.WriteLine to display the value of userIdBytes
                        Debug.WriteLine($"UserId bytes for username '{customerUsername}': {BitConverter.ToString(userIdBytes)}");

                        return userIdBytes;
                    }
                    else {
                        return null; // Customer not found or type not matching
                    }
                }
            }
        }


        [HttpGet("admin/by-type/{type}/{username}")] //update this
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Artist + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrdersByTypeAndUsername(string type,string username) {
            try {
                // Check if the provided type is valid
                if(!IsValidOrderType(type)) {
                    return BadRequest($"Invalid order type '{type}'. Allowed types are 'normal', 'rush', and 'cart'.");
                }

                // Get the user ID based on the provided customer username
                byte[] userIdBytes = await GetUserIdByCustomerUsername(username);

                // If the user ID is empty, return NotFound
                if(userIdBytes == null) {
                    return NotFound($"User with username '{username}' not found.");
                }

                List<Order> orders = new List<Order>();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    string sql = "SELECT * FROM orders WHERE type = @type AND CustomerId = @userId";

                    using(var command = new MySqlCommand(sql,connection)) {
                        command.Parameters.AddWithValue("@type",type);
                        command.Parameters.AddWithValue("@userId",userIdBytes);

                        using(var reader = await command.ExecuteReaderAsync()) {
                            while(await reader.ReadAsync()) {
                                orders.Add(new Order {
                                    orderId = reader.GetGuid(reader.GetOrdinal("OrderId")),
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

                if(orders.Count == 0) {
                    return NotFound($"No orders found for the user with username '{username}' and type '{type}'.");
                }

                return Ok(orders);
            } catch(Exception ex) {
                _logger.LogError(ex,$"An error occurred while fetching orders for username '{username}' and type '{type}'");
                return StatusCode(StatusCodes.Status500InternalServerError,$"An error occurred while fetching orders for username '{username}' and type '{type}': {ex.Message}");
            }
        }


        private bool IsValidOrderType(string type) {
            // Define valid order types
            List<string> validOrderTypes = new List<string> { "normal","rush","cart" };

            // Check if the provided type exists in the valid order types list
            return validOrderTypes.Contains(type.ToLower());
        }

        /*[HttpGet("admin/inactive")] //update this
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
                                orderId = reader.GetGuid(reader.GetOrdinal("OrderId")),
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
        }*/


        /*[HttpGet("customer/cart")] //update this 
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
                                orderId = reader.GetGuid(reader.GetOrdinal("OrderId")),
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
        */

        // suggested URL: "{orderId}/update-price"
        [HttpPatch("update-price")] //change this
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> UpdateOrderAddon([FromQuery] string orderId,[FromQuery] string name,[FromQuery] decimal price) {
            try {
                // Ensure the user is authorized
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if(string.IsNullOrEmpty(username)) {
                    return Unauthorized("User is not authorized");
                }

                // Fetch the user ID of the user performing the update
                string lastUpdatedBy = await GetLastupdater(username);
                if(lastUpdatedBy == null) {
                    return Unauthorized("Username not found");
                }

                // Convert the hexadecimal orderId to binary(16) format with '0x' prefix for MySQL UNHEX function
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Add "custom " prefix to the name
                string customName = "custom " + name;

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    // Check if the order exists with the given OrderId
                    string sqlCheck = "SELECT COUNT(*) FROM orders WHERE OrderId = UNHEX(@orderId)";
                    using(var checkCommand = new MySqlCommand(sqlCheck,connection)) {
                        checkCommand.Parameters.AddWithValue("@orderId",orderIdBinary);

                        int orderCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                        if(orderCount == 0) {
                            Debug.Write(orderIdBinary);
                            return NotFound("Order not found");
                        }
                    }

                    // Calculate the total price based on quantity and price
                    decimal total = price * 1; // Assuming quantity is always 1 for simplicity

                    // Insert the new addon into the orderaddons table with calculated total
                    string sqlInsert = "INSERT INTO orderaddons (OrderId, name, price, quantity, Total) VALUES (UNHEX(@orderId), @name, @price, @quantity, @total)";
                    using(var insertCommand = new MySqlCommand(sqlInsert,connection)) {
                        insertCommand.Parameters.AddWithValue("@orderId",orderIdBinary);
                        insertCommand.Parameters.AddWithValue("@name",customName); // Use customName with "custom " prefix
                        insertCommand.Parameters.AddWithValue("@price",price);
                        insertCommand.Parameters.AddWithValue("@quantity",1); // Hardcoded quantity as 1 for simplicity
                        insertCommand.Parameters.AddWithValue("@total",total);

                        await insertCommand.ExecuteNonQueryAsync();
                    }

                    // Update the status of the order in the database
                    string sqlUpdate = "UPDATE orders SET Status = 'confirmation', last_updated_by = @lastUpdatedBy, last_updated_at = @lastUpdatedAt WHERE OrderId = UNHEX(@orderId)";
                    using(var updateCommand = new MySqlCommand(sqlUpdate,connection)) {
                        updateCommand.Parameters.AddWithValue("@orderId",orderIdBinary);
                        updateCommand.Parameters.AddWithValue("@lastUpdatedBy",lastUpdatedBy);
                        updateCommand.Parameters.AddWithValue("@lastUpdatedAt",DateTime.UtcNow);

                        int rowsAffected = await updateCommand.ExecuteNonQueryAsync();
                        if(rowsAffected == 0) {
                            return NotFound("Order not found");
                        }
                    }
                }

                return Ok("Order addon added and order status updated to confirmation successfully");
            } catch(Exception ex) {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex,"An error occurred while adding the order addon");
                return StatusCode(500,"An error occurred while processing the request");
            }
        }

        [HttpPatch("{orderId}/approve")] // done
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> UpdateOrderxStatus(string orderId,[FromQuery] string action) {
            try {
                // Ensure the user is authorized
                var username = User.FindFirst(ClaimTypes.Name)?.Value;
                if(string.IsNullOrEmpty(username)) {
                    return Unauthorized("User is not authorized");
                }

                // Fetch the user ID of the user performing the update
                string lastUpdatedBy = await GetLastupdater(username);
                if(lastUpdatedBy == null) {
                    return Unauthorized("Username not found");
                }

                // Convert the hexadecimal orderId to binary(16) format with '0x' prefix for MySQL UNHEX function
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();
                Debug.Write("orderId here!!! " + orderIdBinary);

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    // Check if the order exists with the given OrderId
                    string sqlCheck = "SELECT COUNT(*) FROM orders WHERE order_id = UNHEX(@orderId)";
                    using(var checkCommand = new MySqlCommand(sqlCheck,connection)) {
                        checkCommand.Parameters.AddWithValue("@orderId",orderIdBinary);

                        int orderCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());
                        if(orderCount == 0) {
                            Debug.Write(orderIdBinary);
                            return NotFound("Order not found");
                        }
                    }

                    // Prepare the SQL update query based on the action
                    string sqlUpdate;
                    switch(action.ToLower()) {
                        case "half":
                            sqlUpdate = "UPDATE orders SET status = 'assigning artist', payment = 'half', last_updated_by = @lastUpdatedBy, is_active = 1, last_updated_at = @lastUpdatedAt WHERE order_id = UNHEX(@orderId)";
                            break;

                        case "full":
                            sqlUpdate = "UPDATE orders SET status = 'assigning artist', payment = 'full', last_updated_by = @lastUpdatedBy, is_active = 1, last_updated_at = @lastUpdatedAt WHERE order_id = UNHEX(@orderId)";
                            break;

                        default:
                            return BadRequest("Invalid action. Valid actions are 'half' or 'full'.");
                    }

                    // Execute the update command
                    using(var updateCommand = new MySqlCommand(sqlUpdate,connection)) {
                        updateCommand.Parameters.AddWithValue("@orderId",orderIdBinary);
                        updateCommand.Parameters.AddWithValue("@lastUpdatedBy",lastUpdatedBy);
                        updateCommand.Parameters.AddWithValue("@lastUpdatedAt",DateTime.UtcNow);

                        int rowsAffected = await updateCommand.ExecuteNonQueryAsync();
                        if(rowsAffected == 0) {
                            return NotFound("Order not found");
                        }
                    }

                    byte[] suborderid = await UpdateStatus1(orderIdBinary);

                    Debug.WriteLine(BitConverter.ToString(suborderid));

                    if(suborderid == null) {
                        return NotFound("No suborder ID found for the given order ID.");
                    }

                    await UpdateStatus2(suborderid,action);

                }

                return Ok("Order status updated successfully");
            } catch(Exception ex) {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex,"An error occurred while updating the order status");
                return StatusCode(500,"An error occurred while processing the request");
            }
        }

        private async Task UpdateStatus2(byte[] orderIdBinary,string action)//decide whether to use or nahh
        {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sqlUpdate;
                switch(action.ToLower()) {
                    case "half":
                        sqlUpdate = "UPDATE suborders SET status = 'assigning artist' WHERE suborder_id = @orderId";
                        break;

                    case "full":
                        sqlUpdate = "UPDATE suborders SET status = 'assigning artist' WHERE suborder_id = @orderId";
                        break;

                    default:
                        throw new ArgumentException("Invalid action. Valid actions are 'half' or 'full'.");
                }

                using(var command = new MySqlCommand(sqlUpdate,connection)) {
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<byte[]> UpdateStatus1(string orderIdBinary)//decide whether to use or nahh
        {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                // Specify the columns you want to select
                string sql = "SELECT suborder_id FROM suborders WHERE order_id = UNHEX(@orderId)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);

                    // Use ExecuteReaderAsync to execute the SELECT query
                    using(var reader = await command.ExecuteReaderAsync()) {
                        if(await reader.ReadAsync()) {
                            // Return the binary value of suborder_id directly
                            byte[] suborderIdBytes = (byte[])reader["suborder_id"];

                            // Debug.WriteLine to display the value of suborderIdBytes
                            Debug.WriteLine($"Suborder ID bytes for order ID '{orderIdBinary}': {BitConverter.ToString(suborderIdBytes)}");

                            return suborderIdBytes;
                        }
                        else {
                            // Return null or handle cases where no rows are found
                            return null;
                        }
                    }
                }
            }
        }


        private string ConvertGuidToBinary16(string guidString) {
            // Parse the input GUID string
            if(!Guid.TryParse(guidString,out Guid guid)) {
                throw new ArgumentException("Invalid GUID format",nameof(guidString));
            }

            // Convert the GUID to a byte array and then to a formatted binary(16) string
            byte[] guidBytes = guid.ToByteArray();
            string binary16String = BitConverter.ToString(guidBytes).Replace("-","");

            return binary16String;
        }


        /*[HttpPatch("customer/confirmation")] //debug this 
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
        */

        private byte[] FromHexString(string hexString) {
            // Remove the leading "0x" if present
            if(hexString.StartsWith("0x",StringComparison.OrdinalIgnoreCase)) {
                hexString = hexString.Substring(2);
            }

            // Convert the hexadecimal string to a byte array
            byte[] bytes = new byte[hexString.Length / 2];
            for(int i = 0; i < bytes.Length; i++) {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2,2),16);
            }
            return bytes;
        }


        [HttpPatch("suborders/{suborderId}/update-status")] //done
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Artist)]
        public async Task<IActionResult> PatchOrderStatus(string suborderId,[FromQuery] string action) {
            try {
                // Convert the orderId from GUID string to binary(16) format without '0x' prefix
                string orderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

                // Update the order status based on the action
                if(action.Equals("send",StringComparison.OrdinalIgnoreCase)) {
                    await UpdateOrderStatus(orderIdBinary,true); // Set isActive to true
                    await UpdateStatus(orderIdBinary,"for pick up");
                    await UpdateLastUpdatedAt(orderIdBinary);
                }
                else if(action.Equals("done",StringComparison.OrdinalIgnoreCase)) {
                    // Update the status in the database
                    await UpdateOrderStatus(orderIdBinary,false); // Set isActive to false
                    await UpdateStatus(orderIdBinary,"done");
                    await ProcessOrderCompletion(orderIdBinary);
                    // Update the last_updated_at column
                    await UpdateLastUpdatedAt(orderIdBinary);
                }
                else {
                    return BadRequest("Invalid action. Please choose 'send' or 'done'.");
                }

                return Ok($"Order with ID '{suborderId}' has been successfully updated to '{action}'.");
            } catch(Exception ex) {
                _logger.LogError(ex,$"An error occurred while processing the request to update order status for '{suborderId}'.");
                return StatusCode(500,$"An error occurred while processing the request to update order status for '{suborderId}'.");
            }
        }

        private async Task ProcessOrderCompletion(string orderIdBinary) {
            try {
                // Retrieve order details and insert into sales table
                var forSalesDetails = await GetOrderDetailsAndInsertIntoSales(orderIdBinary);

                if(forSalesDetails != null) {
                    _logger.LogInformation($"Order details inserted into sales table: {forSalesDetails.name}");
                }
                else {
                    _logger.LogWarning($"No details found for order with ID '{orderIdBinary}'");
                }
            } catch(Exception ex) {
                _logger.LogError(ex,$"An error occurred while processing order completion for '{orderIdBinary}'.");
                throw; // Re-throw the exception to propagate it to the calling method
            }
        }

        private async Task<forSales> GetOrderDetailsAndInsertIntoSales(string orderIdBytes) {
            try {
                // Retrieve order details from the orders table
                var forSalesDetails = await GetOrderDetails(orderIdBytes);

                // If order details found, insert into the sales table
                if(forSalesDetails != null) {
                    var existingTotal = await GetExistingTotal(forSalesDetails.name);

                    if(existingTotal.HasValue) {
                        // If the orderName already exists, update the Total
                        await UpdateTotalInSalesTable(forSalesDetails.name,existingTotal.Value + forSalesDetails.total);
                    }
                    else {
                        // If the orderName doesn't exist, insert a new record
                        await InsertIntoSalesTable(forSalesDetails);
                    }

                    return forSalesDetails;
                }
                else {
                    return null;
                }
            } catch(Exception ex) {
                _logger.LogError(ex,$"An error occurred while retrieving order details and inserting into sales table for '{orderIdBytes}'.");
                throw; // Re-throw the exception to propagate it to the calling method
            }
        }

        private async Task<forSales> GetOrderDetails(string orderIdBytes) //update this
        {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                using(var command = connection.CreateCommand()) {
                    command.CommandText = @"SELECT o.design_name, o.price, o.employee_id, o.created_at, o.quantity, 
                                    u.Contact, u.Email 
                                    FROM suborders o
                                    JOIN users u ON o.employee_id = u.UserId
                                    WHERE o.suborder_id = UNHEX(@orderId)";
                    command.Parameters.AddWithValue("@orderId",orderIdBytes);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        if(reader.Read()) {
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

                            return new forSales {
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


        private async Task InsertIntoSalesTable(forSales forSalesDetails) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                using(var command = connection.CreateCommand()) {
                    // Adjust column names based on your actual schema
                    command.CommandText = @"INSERT INTO sales (Name, Cost, Date, Contact, Email, Total) 
                                    VALUES (@name, @cost, @date, @contact, @email, @total)";
                    command.Parameters.AddWithValue("@name",forSalesDetails.name);
                    command.Parameters.AddWithValue("@cost",forSalesDetails.cost);
                    command.Parameters.AddWithValue("@date",forSalesDetails.date);
                    command.Parameters.AddWithValue("@contact",forSalesDetails.contact);
                    command.Parameters.AddWithValue("@email",forSalesDetails.email);
                    command.Parameters.AddWithValue("@total",forSalesDetails.total);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<int?> GetExistingTotal(string DesignName) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                using(var command = connection.CreateCommand()) {
                    command.CommandText = "SELECT Total FROM sales WHERE Name = @orderName";
                    command.Parameters.AddWithValue("@orderName",DesignName);

                    var result = await command.ExecuteScalarAsync();
                    return result != null ? Convert.ToInt32(result) : (int?)null;
                }
            }
        }

        private async Task UpdateTotalInSalesTable(string DesignName,int newTotal) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "UPDATE sales SET Total = @newTotal WHERE Name = @orderName";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@newTotal",newTotal);
                    command.Parameters.AddWithValue("@orderName",DesignName);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        private async Task UpdateStatus(string orderIdBinary,string status) {
            try {
                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    // Update the status of the suborders
                    string subOrderSql = "UPDATE suborders SET status = @subOrderStatus WHERE suborder_id = UNHEX(@orderId)";
                    using(var subOrderCommand = new MySqlCommand(subOrderSql,connection)) {
                        subOrderCommand.Parameters.AddWithValue("@subOrderStatus",status);
                        subOrderCommand.Parameters.AddWithValue("@orderId",orderIdBinary);
                        await subOrderCommand.ExecuteNonQueryAsync();
                    }
                }
            } catch(Exception ex) {
                _logger.LogError(ex,$"An error occurred while updating status for order with ID '{orderIdBinary}'.");
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


        [HttpPut("current-user/{suborderId}/manage-add-ons")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> ManageAddOnsByAddOnId(string suborderId, [FromBody] ManageAddOnQuantityWrapper manageWrapper)
        {
            // Convert suborderId to binary format
            string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

            // Loop through each AddOn in the manage list
            foreach (var manage in manageWrapper.manage)
            {
                // Log the process for each add-on
                _logger.LogInformation($"Managing AddOnId: {manage.addonId} for SubOrderId: {suborderId}");

                // Fetch the add-on price and name
                double addonPrice = await GetAddonPriceAsync(manage.addonId);
                string name = await AddonName(manage.addonId);

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();
                    try
                    {
                        // Calculate total price
                        double total = manage.quantity * addonPrice;

                        // Insert or update the order add-ons for the current add-on
                        await InsertOrUpdateOrderaddonWithSubOrderId(suborderIdBinary, manage.addonId, addonPrice, manage.quantity, name, total);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Transaction failed for AddOnId: {manage.addonId}, rolling back");
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
        public async Task<IActionResult> ManageAddOnsByPastryMaterialId(string pastryMaterialId,string suborderId,int modifiedAddOnId,[FromBody] ManageAddOnAction action) {
            // Log the start of the process
            _logger.LogInformation($"Starting ManageAddOnsByPastryMaterialId for pastryMaterialId: {pastryMaterialId}, OrderId: {suborderId}, and AddOnId: {modifiedAddOnId}");

            // Convert OrderId to binary format
            string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                using(var transaction = await connection.BeginTransactionAsync()) {
                    try {
                        // Retrieve size from orders table
                        string size = await GetOrderSize(connection,transaction,suborderIdBinary);

                        Debug.WriteLine(size);

                        // Retrieve add-ons based on pastryMaterialId and size
                        List<PastryMaterialAddOn> allAddOns = new List<PastryMaterialAddOn>();

                        // Check if size matches any sub-variant
                        List<PastryMaterialAddOn> pastryMaterialSubVariantAddOns = await GetPastryMaterialSubVariantAddOns(connection,transaction,pastryMaterialId,size);
                        if(pastryMaterialSubVariantAddOns.Any()) {
                            allAddOns.AddRange(pastryMaterialSubVariantAddOns);
                        }

                        // Always retrieve add-ons from pastymaterialaddons regardless of size
                        List<PastryMaterialAddOn> pastryMaterialAddOns = await GetPastryMaterialAddOns(connection,transaction,pastryMaterialId);
                        allAddOns.AddRange(pastryMaterialAddOns);

                        // Fetch add-on details only once for efficiency
                        Dictionary<int,(string Name, double Price)> addOnDetailsDict = new Dictionary<int,(string Name, double Price)>();
                        foreach(var addOn in allAddOns) {
                            var addOnDetails = await GetAddOnDetails(connection,transaction,addOn.AddOnId);
                            addOnDetailsDict[addOn.AddOnId] = addOnDetails;
                        }

                        // Process the action
                        foreach(var addOn in allAddOns) {
                            if(addOn.AddOnId == modifiedAddOnId) {
                                if(action.ActionType.ToLower() == "quantity") {
                                    // Fetch add-on details
                                    if(addOnDetailsDict.TryGetValue(addOn.AddOnId,out var addOnDetails)) {
                                        // Calculate total price
                                        double total = action.Quantity * addOnDetails.Price;

                                        // Insert or update quantity for the specified add-on in orderaddons
                                        await SetOrUpdateAddOn(connection,transaction,suborderIdBinary,addOn.AddOnId,action.Quantity,total,addOnDetailsDict);
                                    }
                                }
                                else if(action.ActionType.ToLower() == "remove") {
                                    // Set quantity to 0 and remove add-on from orderaddons
                                    await SetOrUpdateAddOn(connection,transaction,suborderIdBinary,addOn.AddOnId,0,0,addOnDetailsDict);
                                }
                                else {
                                    return BadRequest($"Unsupported action type '{action.ActionType}'.");
                                }
                            }
                            else {
                                // Insert add-on without modifying its quantity or total
                                var addOnDetails = addOnDetailsDict[addOn.AddOnId];
                                double total = addOn.Quantity * addOnDetails.Price;
                                await SetOrUpdateAddOn(connection,transaction,suborderIdBinary,addOn.AddOnId,addOn.Quantity,total,addOnDetailsDict);
                            }
                        }

                        await transaction.CommitAsync();
                    } catch(Exception ex) {
                        _logger.LogError(ex,"Transaction failed, rolling back");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }

            return Ok("Add-ons quantities successfully managed.");
        }


        private async Task<string> GetOrderSize(MySqlConnection connection,MySqlTransaction transaction,string orderId) {
            string sql = @"SELECT size
                   FROM suborders
                   WHERE suborder_id = UNHEX(@orderId)";

            using(var command = new MySqlCommand(sql,connection,transaction)) {
                command.Parameters.AddWithValue("@orderId",orderId);

                using(var reader = await command.ExecuteReaderAsync()) {
                    if(await reader.ReadAsync()) {
                        return reader.GetString("size");
                    }
                    else {
                        throw new Exception($"Order size not found for OrderId '{orderId}'.");
                    }
                }
            }
        }

        private async Task<List<PastryMaterialAddOn>> GetPastryMaterialAddOns(MySqlConnection connection,MySqlTransaction transaction,string pastryMaterialId) {
            List<PastryMaterialAddOn> pastryMaterialAddOns = new List<PastryMaterialAddOn>();

            string sql = @"SELECT add_ons_id AS AddOnId, amount AS DefaultQuantity
                   FROM pastymaterialaddons
                   WHERE pastry_material_id = @pastryMaterialId";

            using(var command = new MySqlCommand(sql,connection,transaction)) {
                command.Parameters.AddWithValue("@pastryMaterialId",pastryMaterialId);

                using(var reader = await command.ExecuteReaderAsync()) {
                    while(await reader.ReadAsync()) {
                        PastryMaterialAddOn addOn = new PastryMaterialAddOn {
                            AddOnId = reader.GetInt32("AddOnId"),
                            Quantity = reader.GetInt32("DefaultQuantity")
                        };

                        pastryMaterialAddOns.Add(addOn);
                    }
                }
            }

            return pastryMaterialAddOns;
        }

        private async Task<List<PastryMaterialAddOn>> GetPastryMaterialSubVariantAddOns(MySqlConnection connection,MySqlTransaction transaction,string pastryMaterialId,string size) {
            List<PastryMaterialAddOn> pastryMaterialAddOns = new List<PastryMaterialAddOn>();

            string sql = @"SELECT pmsa.add_ons_id AS AddOnId, pmsa.amount AS DefaultQuantity
                   FROM pastrymaterialsubvariantaddons pmsa
                   JOIN pastrymaterialsubvariants pmsv ON pmsa.pastry_material_sub_variant_id = pmsv.pastry_material_sub_variant_id
                   WHERE pmsv.pastry_material_id = @pastryMaterialId AND pmsv.sub_variant_name = @size";

            using(var command = new MySqlCommand(sql,connection,transaction)) {
                command.Parameters.AddWithValue("@pastryMaterialId",pastryMaterialId);
                command.Parameters.AddWithValue("@size",size);

                using(var reader = await command.ExecuteReaderAsync()) {
                    while(await reader.ReadAsync()) {
                        PastryMaterialAddOn addOn = new PastryMaterialAddOn {
                            AddOnId = reader.GetInt32("AddOnId"),
                            Quantity = reader.GetInt32("DefaultQuantity")
                        };

                        pastryMaterialAddOns.Add(addOn);
                    }
                }
            }

            return pastryMaterialAddOns;
        }

        private async Task<(string Name, double Price)> GetAddOnDetails(MySqlConnection connection,MySqlTransaction transaction,int addOnId) {
            string sql = @"SELECT name, price
                   FROM addons
                   WHERE add_ons_id = @addOnId";

            using(var command = new MySqlCommand(sql,connection,transaction)) {
                command.Parameters.AddWithValue("@addOnId",addOnId);

                using(var reader = await command.ExecuteReaderAsync()) {
                    if(await reader.ReadAsync()) {
                        string name = reader.GetString("name");
                        double price = reader.GetDouble("price");
                        return (name, price);
                    }
                    else {
                        throw new Exception($"Add-on details not found for ID '{addOnId}'.");
                    }
                }
            }
        }

        private async Task SetOrUpdateAddOn(MySqlConnection connection,MySqlTransaction transaction,string orderIdBinary,int addOnId,int quantity,double total,Dictionary<int,(string Name, double Price)> addOnDetailsDict) {
            // Retrieve add-on details from the dictionary
            if(!addOnDetailsDict.TryGetValue(addOnId,out var addOnDetails)) {
                throw new Exception($"Add-on details not found for AddOnId '{addOnId}'.");
            }

            if(quantity > 0) {
                // Check if the add-on already exists in orderaddons
                string selectSql = @"SELECT COUNT(*) 
            FROM orderaddons 
            WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnId";

                using(var selectCommand = new MySqlCommand(selectSql,connection,transaction)) {
                    selectCommand.Parameters.AddWithValue("@orderId",orderIdBinary);
                    selectCommand.Parameters.AddWithValue("@addOnId",addOnId);

                    int count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                    if(count == 0) {
                        // Insert new add-on into orderaddons
                        string insertSql = @"INSERT INTO orderaddons (order_id, add_ons_id, quantity, total, name, price)
                    VALUES (UNHEX(@orderId), @addOnId, @quantity, @total, @name, @price)";
                        using(var insertCommand = new MySqlCommand(insertSql,connection,transaction)) {
                            insertCommand.Parameters.AddWithValue("@orderId",orderIdBinary);
                            insertCommand.Parameters.AddWithValue("@addOnId",addOnId);
                            insertCommand.Parameters.AddWithValue("@quantity",quantity);
                            insertCommand.Parameters.AddWithValue("@total",total);
                            insertCommand.Parameters.AddWithValue("@name",addOnDetails.Name);
                            insertCommand.Parameters.AddWithValue("@price",addOnDetails.Price);
                            _logger.LogInformation($"Inserting add-on ID '{addOnId}' with quantity '{quantity}', total '{total}', name '{addOnDetails.Name}', and price '{addOnDetails.Price}' into orderaddons");
                            await insertCommand.ExecuteNonQueryAsync();
                        }
                    }
                    else {
                        // Update quantity and total for existing add-on in orderaddons
                        string updateSql = @"UPDATE orderaddons 
                    SET quantity = @quantity, total = @total
                    WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnId";

                        using(var updateCommand = new MySqlCommand(updateSql,connection,transaction)) {
                            updateCommand.Parameters.AddWithValue("@quantity",quantity);
                            updateCommand.Parameters.AddWithValue("@total",total);
                            updateCommand.Parameters.AddWithValue("@orderId",orderIdBinary);
                            updateCommand.Parameters.AddWithValue("@addOnId",addOnId);

                            _logger.LogInformation($"Updating quantity for add-on ID '{addOnId}' to '{quantity}', and total to '{total}' in orderaddons");

                            await updateCommand.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            else {
                // If quantity is 0, remove the add-on from orderaddons
                await RemoveAddOnFromOrderAddOns(connection,transaction,orderIdBinary,addOnId);
            }
        }



        private async Task RemoveAddOnFromOrderAddOns(MySqlConnection connection,MySqlTransaction transaction,string orderIdBinary,int addOnId) {
            string deleteSql = @"DELETE FROM orderaddons WHERE order_id = UNHEX(@orderId) AND add_ons_id = @addOnId";
            using(var deleteCommand = new MySqlCommand(deleteSql,connection,transaction)) {
                deleteCommand.Parameters.AddWithValue("@orderId",orderIdBinary);
                deleteCommand.Parameters.AddWithValue("@addOnId",addOnId);

                _logger.LogInformation($"Removing add-on ID '{addOnId}' from orderaddons");

                await deleteCommand.ExecuteNonQueryAsync();
            }
        }


        [HttpPatch("/culo-api/v1/current-user/manage-add-ons-by-material/suborders/{suborderId}")]//done 
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> UpdateOrderDetails(string suborderId,[FromBody] UpdateOrderDetailsRequest request) {
            try {
                _logger.LogInformation($"Starting UpdateOrderDetails for orderId: {suborderId}");

                // Convert orderId to binary(16) format without '0x' prefix
                string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    using(var transaction = await connection.BeginTransactionAsync()) {
                        try {
                            // Update order details in the orders table
                            await UpdateOrderDetailsInDatabase(connection,transaction,suborderIdBinary,request);

                            await transaction.CommitAsync();
                        } catch(Exception ex) {
                            _logger.LogError(ex,"Transaction failed, rolling back");
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }

                return Ok("Order details successfully updated.");
            } catch(Exception ex) {
                _logger.LogError(ex,$"Error updating order details for order with ID '{suborderId}'");
                return StatusCode(500,$"An error occurred while updating order details for order with ID '{suborderId}'.");
            }
        }

        private async Task UpdateOrderDetailsInDatabase(MySqlConnection connection,MySqlTransaction transaction,string orderIdBinary,UpdateOrderDetailsRequest request) {

            // Prepare SQL statement for updating orders table
            string updateSql = @"UPDATE suborders 
                         SET description = @description, 
                             quantity = @quantity,
                             size = @size,
                             flavor = @flavor,
                             color = @color, shape = @shape
                         WHERE suborder_id = UNHEX(@suborderId)";

            using(var command = new MySqlCommand(updateSql,connection,transaction)) {
                command.Parameters.AddWithValue("@description",request.Description);
                command.Parameters.AddWithValue("@quantity",request.Quantity);
                command.Parameters.AddWithValue("@size",request.Size);
                command.Parameters.AddWithValue("@flavor",request.Flavor);
                command.Parameters.AddWithValue("@color",request.color);
                command.Parameters.AddWithValue("@shape",request.shape);
                command.Parameters.AddWithValue("@suborderId",orderIdBinary);


                await command.ExecuteNonQueryAsync();

                _logger.LogInformation($"Updated order details in orders table for order with ID '{orderIdBinary}'");
            }
        }

        [HttpPatch("custom-orders/{customId}/set-price")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> PatchCustomOrder(string customId,[FromBody] CustomOrderUpdateRequest customReq) {
            if(customReq == null || string.IsNullOrWhiteSpace(customId)) {
                return BadRequest("Invalid request data.");
            }

            string suborderIdBinary = ConvertGuidToBinary16(customId).ToLower();

            string sql = @"
            UPDATE customorders 
            SET design_name = @designName, 
                price = @price 
            WHERE custom_id = UNHEX(@customId)";

            try {
                using(var connection = new MySqlConnection(connectionstring)) {
                    await connection.OpenAsync();

                    using(MySqlCommand cmd = new MySqlCommand(sql,connection)) {
                        cmd.Parameters.AddWithValue("@designName",customReq.DesignName);
                        cmd.Parameters.AddWithValue("@price",customReq.Price);
                        cmd.Parameters.AddWithValue("@customId",suborderIdBinary);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if(rowsAffected > 0) {
                            return Ok("Custom order updated successfully.");
                        }
                        else {
                            return NotFound("Custom order not found.");
                        }
                    }
                }
            } catch(Exception ex) {
                // Log the exception here (not shown)
                return StatusCode(500,"Internal server error: " + ex.Message);
            }
        }


        [HttpDelete("/culo-api/v1/current-user/orders/{orderId}/remove")] //debug this  
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
        public async Task<IActionResult> RemoveOrder(string orderId) {
            try {
                // Get the current user's username
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;
                if(string.IsNullOrEmpty(customerUsername)) {
                    return Unauthorized("No valid customer username found.");
                }

                // Convert the hex orderId to binary format
                string orderIdBinary = ConvertGuidToBinary16(orderId).ToLower();

                // Check if the order belongs to the current user
                bool isOrderOwnedByUser = await IsOrderOwnedByUser(customerUsername,orderIdBinary);
                if(!isOrderOwnedByUser) {
                    return Unauthorized("You do not have permission to delete this order.");
                }

                // Delete the order from the database
                bool deleteSuccess = await DeleteOrderByOrderId(orderIdBinary);
                if(deleteSuccess) {
                    return Ok("Order removed successfully.");
                }
                else {
                    return NotFound("Order not found.");
                }
            } catch(Exception ex) {
                _logger.LogError(ex,"Error removing cart");
                return StatusCode(500,$"An error occurred while processing the request.");
            }
        }

        private async Task<bool> IsOrderOwnedByUser(string customerUsername,string orderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
            SELECT COUNT(*) 
            FROM orders 
            WHERE order_id = UNHEX(@orderId) 
            AND customer_id = (SELECT UserId FROM users WHERE Username = @customerUsername)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);
                    command.Parameters.AddWithValue("@customerUsername",customerUsername);

                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        private async Task<bool> DeleteOrderByOrderId(string orderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "DELETE FROM orders WHERE order_id = UNHEX(@orderId)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);

                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        [HttpDelete("/culo-api/v1/current-user/cart/{suborderId}")] //debug this  
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
        public async Task<IActionResult> RemoveCart(string suborderId) {
            try {
                // Get the current user's username
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;
                if(string.IsNullOrEmpty(customerUsername)) {
                    return Unauthorized("No valid customer username found.");
                }

                // Convert the hex orderId to binary format
                string suborderIdBinary = ConvertGuidToBinary16(suborderId).ToLower();

                // Check if the order belongs to the current user
                bool isOrderOwnedByUser = await IsSuborderOwnedByUser(customerUsername,suborderIdBinary);
                if(!isOrderOwnedByUser) {
                    return Unauthorized("You do not have permission to delete this order.");
                }

                // Delete the order from the database
                bool deleteSuccess = await DeleteOrderBySuborderId(suborderIdBinary);
                if(deleteSuccess) {
                    return Ok("Order removed successfully.");
                }
                else {
                    return NotFound("Order not found.");
                }
            } catch(Exception ex) {
                _logger.LogError(ex,"Error removing cart");
                return StatusCode(500,$"An error occurred while processing the request.");
            }
        }

        private async Task<bool> IsSuborderOwnedByUser(string customerUsername,string orderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
            SELECT COUNT(*) 
            FROM suborders 
            WHERE suborder_id = UNHEX(@orderId) 
            AND customer_id = (SELECT UserId FROM users WHERE Username = @customerUsername)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);
                    command.Parameters.AddWithValue("@customerUsername",customerUsername);

                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        private async Task<bool> DeleteOrderBySuborderId(string orderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "DELETE FROM suborders WHERE suborder_id = UNHEX(@orderId)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);

                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        [HttpDelete("/culo-api/v1/current-user/cart")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
        public async Task<IActionResult> RemoveAllCart() {
            try {
                // Get the current user's username
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;
                if(string.IsNullOrEmpty(customerUsername)) {
                    return Unauthorized("No valid customer username found.");
                }

                // Check if the suborders belong to the current user
                bool isOrderOwnedByUser = await SuborderOwnedByUser(customerUsername);
                if(!isOrderOwnedByUser) {
                    return Unauthorized("You do not have permission to delete this order.");
                }

                // Delete all suborders belonging to the current user
                bool deleteSuccess = await DeleteAllSubordersByCustomerUsername(customerUsername);
                if(deleteSuccess) {
                    return Ok("All suborders removed successfully.");
                }
                else {
                    return NotFound("No suborders found for this user.");
                }
            } catch(Exception ex) {
                _logger.LogError(ex,"Error removing cart");
                return StatusCode(500,$"An error occurred while processing the request.");
            }
        }

        private async Task<bool> SuborderOwnedByUser(string customerUsername) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
        SELECT COUNT(*) 
        FROM suborders 
        WHERE customer_id = (SELECT UserId FROM users WHERE Username = @customerUsername)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@customerUsername",customerUsername);

                    var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return count > 0;
                }
            }
        }

        private async Task<bool> DeleteAllSubordersByCustomerUsername(string customerUsername) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = @"
        DELETE FROM suborders 
        WHERE order_id IS NULL AND customer_id = (SELECT UserId FROM users WHERE Username = @customerUsername)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@customerUsername",customerUsername);

                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }


        private async Task<bool> CheckOrderExists(string SuborderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                using(var command = connection.CreateCommand()) {
                    command.CommandText = "SELECT COUNT(*) FROM suborders WHERE suborder_id = UNHEX(@orderId)";
                    command.Parameters.AddWithValue("@orderId",SuborderIdBinary);

                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result) > 0;
                }
            }
        }

        private async Task<byte[]> GetEmployeeIdByUsername(string username) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Username = @username AND Type = 2";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@username",username);
                    var result = await command.ExecuteScalarAsync();

                    if(result != null && result != DBNull.Value) {
                        return (byte[])result;
                    }
                    else {
                        return null; // Employee not found
                    }
                }
            }
        }



        private async Task UpdateOrderEmployeeId(string orderIdBinary,byte[] employeeId,string employeeUsername) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                // Update the status in both suborders and orders tables
                string sqlSuborders = "UPDATE suborders SET employee_id = @employeeId, employee_name = @employeeName, status = 'baking' WHERE suborder_id = UNHEX(@orderId)";

                using(var command = new MySqlCommand(sqlSuborders,connection)) {
                    command.Parameters.AddWithValue("@employeeId",employeeId);
                    command.Parameters.AddWithValue("@employeeName",employeeUsername);
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateOrderStatusToBaking(string subOrderIdBinary) {
            string orderIdFi = null;

            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                // Step 1: Retrieve the order_id from the suborders table
                string sqlSubOrder = "SELECT order_id FROM suborders WHERE suborder_id = UNHEX(@subOrderId)";
                using(var command = new MySqlCommand(sqlSubOrder,connection)) {
                    command.Parameters.AddWithValue("@subOrderId",subOrderIdBinary);

                    using(var reader = await command.ExecuteReaderAsync()) {
                        if(await reader.ReadAsync()) {
                            // Convert the order_id directly as it is assumed to be a valid Guid in binary format
                            Guid orderId = new Guid((byte[])reader["order_id"]);

                            // Step 2: Convert the retrieved order_id to binary format
                            orderIdFi = ConvertGuidToBinary16(orderId.ToString()).ToLower();
                        }
                        else {
                            throw new ArgumentException("No suborder found with the provided ID",nameof(subOrderIdBinary));
                        }
                    }
                }

                // Step 3: Update the status in the orders table
                if(!string.IsNullOrEmpty(orderIdFi)) {
                    string sqlOrders = "UPDATE orders SET status = 'baking' WHERE order_id = UNHEX(@orderId)";
                    using(var command = new MySqlCommand(sqlOrders,connection)) {
                        command.Parameters.AddWithValue("@orderId",orderIdFi);
                        int rowsAffected = await command.ExecuteNonQueryAsync();
                        if(rowsAffected == 0) {
                            throw new ArgumentException("Order not found or status not updated",nameof(orderIdFi));
                        }
                    }
                }
            }
        }




        private async Task<bool> GetOrderStatus(string orderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "SELECT isActive FROM orders WHERE OrderId = @orderId";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);
                    var result = await command.ExecuteScalarAsync();

                    if(result != null && result != DBNull.Value) {
                        return (bool)result;
                    }
                    else {
                        return false; // Order not found or isActive is null
                    }
                }
            }
        }


        private async Task UpdateOrderStatus(string orderIdBinary,bool isActive) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "UPDATE suborders SET is_active = @isActive WHERE suborder_id = UNHEX(@orderId)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@isActive",isActive);
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        private async Task UpdateLastUpdatedAt(string orderIdBinary) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string sql = "UPDATE suborders SET last_updated_at = NOW() WHERE suborder_id = UNHEX(@orderId)";

                using(var command = new MySqlCommand(sql,connection)) {
                    command.Parameters.AddWithValue("@orderId",orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<string> getDesignName(string design) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string designQuery = "SELECT display_name FROM designs WHERE design_id = UNHEX(@displayName)";
                using(var designcommand = new MySqlCommand(designQuery,connection)) {
                    designcommand.Parameters.AddWithValue("@displayName",design);
                    object result = await designcommand.ExecuteScalarAsync();
                    if(result != null && result != DBNull.Value) {
                        return (string)result;
                    }
                    else {
                        return null; // Design not found
                    }
                }
            }
        }

        private async Task<byte[]> GetDesignIdByDesignName(string designName) {
            using(var connection = new MySqlConnection(connectionstring)) {
                await connection.OpenAsync();

                string designIdQuery = "SELECT design_id FROM designs WHERE display_name = @DisplayName";
                using(var designIdCommand = new MySqlCommand(designIdQuery,connection)) {
                    designIdCommand.Parameters.AddWithValue("@DisplayName",designName);
                    object result = await designIdCommand.ExecuteScalarAsync();
                    if(result != null && result != DBNull.Value) {
                        return (byte[])result;
                    }
                    else {
                        return null; // Design not found
                    }
                }
            }
        }




    }
}
