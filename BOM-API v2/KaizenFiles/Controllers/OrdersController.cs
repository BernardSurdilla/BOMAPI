using BillOfMaterialsAPI.Helpers;// Adjust the namespace according to your project structure
using BillOfMaterialsAPI.Models;
using CRUDFI.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using BOM_API_v2.KaizenFiles.Models;



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

        private readonly UserManager<APIUsers> userManager;
        private readonly RoleManager<IdentityRole> roleManager;

        public static IWebHostEnvironment _webhostEnvironment;


        public OrdersController(RoleManager<IdentityRole> roleManager, UserManager<APIUsers> userManager, IConfiguration configuration, ILogger<OrdersController> logger, DatabaseContext context, KaizenTables kaizenTables)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;

            _context = context;
            _kaizenTables = kaizenTables;
            this.userManager = userManager;
            this.roleManager = roleManager;

        }



        [HttpPost("/culo-api/v1/current-user/buy-now")]
        [ProducesResponseType(typeof(SuborderResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> BuyNow([FromBody] BuyNow buyNowRequest)
        {
            try
            {
                if (buyNowRequest.quantity < 1 || buyNowRequest.quantity > 3)
                {
                    return BadRequest("Quantity is too many. It must be between 1 and 3.");
                }

                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                var user = await userManager.FindByNameAsync(customerUsername);

                string customerId = user.Id;

                Debug.WriteLine("customer Id: " + customerId);

                // Validate and parse pickup date and time
                if (!DateTime.TryParseExact(buyNowRequest.pickupDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate) ||
                    !DateTime.TryParseExact(buyNowRequest.pickupTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                {
                    return BadRequest("Invalid pickup date or time format. Use 'yyyy-MM-dd' for date and 'h:mm tt' for time.");
                }
                DateTime pickupDateTime = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, parsedTime.Hour, parsedTime.Minute, 0);

                // Validate the order type
                if (!buyNowRequest.type.Equals("normal", StringComparison.OrdinalIgnoreCase) &&
                    !buyNowRequest.type.Equals("rush", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Invalid type. Please choose 'normal' or 'rush'.");
                }

                // Generate new orderId
                Guid orderId = Guid.NewGuid();
                string orderIdBinary = orderId.ToString().ToLower();

                Debug.WriteLine("OrderId Binary: " + orderIdBinary);

                // Calculate total price
                string subersId = await GetPastryMaterialIdByDesignIds(buyNowRequest.designId);
                string pastryMaterialId = await GetPastryMaterialIdBySubersIdAndSize(subersId, buyNowRequest.size);
                double totalPrice = await PriceCalculator.CalculatePastryMaterialPrice(pastryMaterialId, _context, _kaizenTables);

                Debug.WriteLine("Total Price is: " + totalPrice);

                // Insert the new order into the 'orders' table
                await InsertOrderWithOrderId(orderIdBinary, customerUsername, customerId, pickupDateTime, buyNowRequest.type, buyNowRequest.payment);

                // Generate suborderId
                Guid suborderId = Guid.NewGuid();
                string suborderIdBinary = suborderId.ToString().ToLower();

                Debug.WriteLine("OrderId Binary: " + suborderIdBinary);

                // Get design name and shape
                string designName = await getDesignName(buyNowRequest.designId);
                if (string.IsNullOrEmpty(designName))
                {
                    return BadRequest($"Design '{buyNowRequest.designId}' not found.");
                }
                string shape = await GetDesignShapes(buyNowRequest.designId);

                // Create the order object
                var order = new Order
                {
                    price = totalPrice,
                    quantity = buyNowRequest.quantity,
                    designName = designName,
                    size = buyNowRequest.size,
                    flavor = buyNowRequest.flavor,
                    isActive = true,
                    customerName = customerUsername,
                    color = buyNowRequest.color,
                    shape = shape,
                    description = string.IsNullOrEmpty(buyNowRequest.description) ? null : buyNowRequest.description, // Nullable Description,
                    status = "to pay"
                };

                // Insert the order with the determined pastryId
                await InsertsOrder(order, orderIdBinary, suborderIdBinary, buyNowRequest.designId, buyNowRequest.flavor, buyNowRequest.size, pastryMaterialId, customerId, buyNowRequest.color, shape);

                // Manage add-ons if any
                if (buyNowRequest.addonItem != null && buyNowRequest.addonItem.Any())
                {
                    foreach (var addon in buyNowRequest.addonItem)
                    {
                        await ManageAddOnsByPastryMaterialId(pastryMaterialId, suborderIdBinary, addon.id, addon.quantity);
                    }
                }

                // Prepare the suborder response
                var suborderResponse = new SuborderResponse
                {
                    suborderId = suborderIdBinary,
                    addonId = buyNowRequest.addonItem?.Select(a => a.id).ToList() // List of add-on IDs if add-ons exist
                };

                return Ok(suborderResponse); // Return the suborderId and add-ons
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


        private async Task InsertsOrder(Order order, string orderId, string suborderId, string designId, string flavor, string size, string pastryId, string customerId, string color, string shape)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();


                string sql = @"INSERT INTO suborders (
            suborder_id, custom_id,  order_id, customer_id, customer_name, employee_id, created_at, status, design_id, price, quantity, 
            last_updated_by, last_updated_at, is_active, description, flavor, size, color, shape, design_name, pastry_id) 
            VALUES (@suborderid, NULL, @orderid, @customerId, @CustomerName, NULL, NOW(), @status, @designId, @price, 
            @quantity, NULL, NOW(), @isActive, @Description, @Flavor, @Size, @color, @shape, @DesignName, @PastryId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborderid", suborderId);
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
                    command.Parameters.AddWithValue("@Description", order.description);
                    command.Parameters.AddWithValue("@Flavor", flavor);
                    command.Parameters.AddWithValue("@Size", size);
                    command.Parameters.AddWithValue("@DesignName", order.designName);
                    command.Parameters.AddWithValue("@PastryId", pastryId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        [HttpPost("/culo-api/v1/current-user/custom-orders")]
        [ProducesResponseType(typeof(customorderResponse), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> CreateCustomOrder([FromBody] PostCustomOrder customOrder)
        {
            try
            {
                if (customOrder.quantity < 1 || customOrder.quantity > 3)
                {
                    return BadRequest("Quantity is too many. It must be between 1 and 3.");
                }

                // Validate the customOrder object
                if (customOrder == null)
                {
                    return BadRequest("Custom order data is required.");
                }

                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                var user = await userManager.FindByNameAsync(customerUsername);

                string customerId = user.Id;

                // Process the uploaded image from base64 string
                byte[] pictureBinary = null;
                if (!string.IsNullOrEmpty(customOrder.pictureBase64))
                {
                    // Decode the base64 string
                    pictureBinary = Convert.FromBase64String(customOrder.pictureBase64);
                }

                // Create the custom order object
                var order = new Custom
                {
                    quantity = customOrder.quantity,
                    size = customOrder.size,
                    flavor = customOrder.flavor,
                    customerName = customerUsername,
                    color = customOrder.color,
                    shape = customOrder.shape,
                    description = customOrder.description,
                    cover = customOrder.cover,
                    price = 0,
                    isActive = true,
                };

                // Insert the order and related data into the database
                Guid sub = Guid.NewGuid();
                Guid design = Guid.NewGuid();
                Guid custom = Guid.NewGuid();
                string suborderIdBinary = sub.ToString().ToLower();
                string designIdBinary = design.ToString().ToLower();
                string customIdBinary = custom.ToString().ToLower();


                await InsertsCustomToOrder(order,suborderIdBinary, customIdBinary, designIdBinary, order.flavor, order.size, customerId, order.color, order.shape);
                await InsertsCustomImage(pictureBinary, designIdBinary);
                await InsertCustomOrder(customIdBinary, designIdBinary, customOrder.tier, customOrder.cover, suborderIdBinary);

                // Notify the user about the order approval
                string notifId = Guid.NewGuid().ToString().ToLower();
                string message = "Your order is added for approval";
                await NotifyAsync(notifId, customerId.ToLower(), message);

                var suborderResponse = new customorderResponse
                {
                    suborderId = suborderIdBinary
                };

                return Ok(suborderResponse);

            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while creating the custom order");
                return StatusCode(500, "An error occurred while processing the request"); // Return 500 Internal Server Error
            }
        }

        private async Task InsertsCustomImage(byte[] picture, string designId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO customimage (design_id, picture_data, is_active) VALUES (@designId, @picture, 1)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@designId", designId);
                    command.Parameters.AddWithValue("@picture", picture); // Use byte[] directly

                    await command.ExecuteNonQueryAsync();
                }
            }
        }


        private async Task InsertsCustomToOrder(Custom order, string suborderId, string customId, string designId, string flavor, string size, string customerId, string color, string shape)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();


                string sql = @"INSERT INTO suborders (
            suborder_id, custom_id, order_id, customer_id, customer_name, employee_id, created_at, status, design_id, price, quantity, 
            last_updated_by, last_updated_at, is_active, description, flavor, size, color, shape, design_name, pastry_id) 
            VALUES (@suborderid, @customId, NULL, @customerId, @CustomerName, NULL, NOW(), 'cart', @designId, @price, 
            @quantity, NULL, NOW(), @isActive, @Description, @Flavor, @Size, @color, @shape, NULL , NULL)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborderid", suborderId);
                    command.Parameters.AddWithValue("@customId", customId);
                    command.Parameters.AddWithValue("@customerId", customerId);
                    command.Parameters.AddWithValue("@CustomerName", order.customerName);
                    command.Parameters.AddWithValue("@designId", designId);
                    command.Parameters.AddWithValue("@price", order.price);
                    command.Parameters.AddWithValue("@quantity", order.quantity);
                    command.Parameters.AddWithValue("@isActive", order.isActive);
                    command.Parameters.AddWithValue("@color", order.color);
                    command.Parameters.AddWithValue("@shape", shape);
                    command.Parameters.AddWithValue("@Description", order.description);
                    command.Parameters.AddWithValue("@Flavor", flavor);
                    command.Parameters.AddWithValue("@Size", size);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task InsertCustomOrder(string customId, string designId, int tier, string cover, string suborderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO customorders ( custom_id, design_id, created_at, tier, cover, suborder_id ) VALUES (@customId, @designId, Now(), @tier, @cover, @suborderId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customId", customId);
                    command.Parameters.AddWithValue("@designId", designId);
                    command.Parameters.AddWithValue("@tier", tier);
                    command.Parameters.AddWithValue("@cover", cover);
                    command.Parameters.AddWithValue("@suborderId", suborderId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task InsertToOrderWithOrderId(string orderIdBinary, string customerName, string customerId, DateTime pickupDateTime, string type, string payment)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO orders (order_id, customer_id, customer_name, pickup_date, type, status, payment, created_at, last_updated_at) 
                       VALUES (@orderid, @customerId, @CustomerName, @pickupDateTime, @type, 'to review', @payment, NOW(), NOW())";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderid", orderIdBinary);
                    command.Parameters.AddWithValue("@payment", payment);
                    command.Parameters.AddWithValue("@customerId", customerId);
                    command.Parameters.AddWithValue("@pickupDateTime", pickupDateTime);
                    command.Parameters.AddWithValue("@CustomerName", customerName);
                    command.Parameters.AddWithValue("@type", type);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        [HttpGet("/culo-api/v1/current-user/custom-orders/{designId}/picture")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> GetCustomOrderPicture(string designId)
        {
            try
            {
                // Validate the designId
                if (string.IsNullOrEmpty(designId))
                {
                    return BadRequest("Design ID is required.");
                }

                // Retrieve the picture data
                var pictureData = await GetPictureDataByDesignId(designId);
                if (pictureData == null)
                {
                    return NotFound("No picture found for the given design ID.");
                }

                // Convert the picture data to base64
                string base64Picture = Convert.ToBase64String(pictureData);

                // Retrieve the design name
                var designName = await GetDesignNameByDesignId(designId);

                // Return the base64 string and design name in the response
                return Ok(new { designName, base64Picture});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving the custom order picture");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }

        private async Task<byte[]> GetPictureDataByDesignId(string designId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT picture_data FROM customimage WHERE design_id = @designId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@designId", designId);

                    // Execute the command and retrieve the picture data
                    object result = await command.ExecuteScalarAsync();
                    return result as byte[]; // Cast the result to byte[] (null if no record found)
                }
            }
        }

        private async Task<string> GetDesignNameByDesignId(string designId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT design_name FROM suborders WHERE design_id = @designId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@designId", designId);

                    // Execute the command and retrieve the design name
                    object result = await command.ExecuteScalarAsync();
                    return result as string; // Cast the result to string (null if no record found)
                }
            }
        }



        [HttpPost("/culo-api/v1/current-user/cart")]
        [ProducesResponseType(typeof(SuborderResponse), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> CreateOrder([FromBody] OrderDTO orderDto)
        {
            try
            {

                if (orderDto.quantity < 1 || orderDto.quantity > 3)
                {
                    return BadRequest("Quantity is too many. It must be between 1 and 3.");
                }

                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                var user = await userManager.FindByNameAsync(customerUsername);

                string customerId = user.Id;

                Debug.WriteLine("customer ID: " + customerId);

                if (customerId == null)
                {
                    return BadRequest("Customer not found");
                }

                

                // Convert designId from string to hex
                string designIdHex = orderDto.designId;
                string designName = await getDesignName(designIdHex);

                if (string.IsNullOrEmpty(designIdHex))
                {
                    return BadRequest($"Design '{orderDto.designId}' cannot be null or empty.");
                }

                Debug.WriteLine("desing id hex: " +designIdHex);
                Debug.WriteLine("design name: " + designName);
                string shape = await GetDesignShapes(designIdHex);

                // Get the pastry material ID using design ID and size
                string subersId = await GetPastryMaterialIdByDesignIds(designIdHex);
                string pastryMaterialId = await GetPastryMaterialIdBySubersIdAndSize(subersId, orderDto.size);
                string subVariantId = await GetPastryMaterialSubVariantId(subersId, orderDto.size);
                string pastryId = !string.IsNullOrEmpty(subVariantId) ? subVariantId : pastryMaterialId;

                Debug.Write("pastry material id: " + pastryId);

                // Calculate total price
                double totalPrice = await PriceCalculator.CalculatePastryMaterialPrice(pastryId, _context, _kaizenTables);
                Debug.WriteLine("Total Price is: " + totalPrice);

                // Generate suborderId
                Guid suborderId = Guid.NewGuid();
                string suborderIdBinary = suborderId.ToString().ToLower();

                // Create the order object
                var order = new Order
                {
                    quantity = orderDto.quantity,
                    price = totalPrice, // Use the calculated price
                    designName = designName,
                    size = orderDto.size,
                    flavor = orderDto.flavor,
                    isActive = true,
                    customerName = customerUsername,
                    color = orderDto.color,
                    shape = shape,
                    description = string.IsNullOrEmpty(orderDto.description) ? null : orderDto.description,
                    status = "cart"
                };

                // Insert the order into the database
                await InsertOrder(order, designIdHex, orderDto.flavor, orderDto.size, pastryId, customerId, orderDto.color, shape, suborderIdBinary);

                // Manage add-ons based on the add-on items in the request
                foreach (var addon in orderDto.addonItem)
                {
                    await ManageAddOnsByPastryMaterialId(pastryId, suborderIdBinary, addon.id, addon.quantity);
                }

                Guid notId = Guid.NewGuid();

                string notifId = notId.ToString().ToLower();

                Debug.WriteLine("notification id: " + notifId);

                // Send notification to the user
                string userId = customerId.ToLower();
                string message = $"{order.designName ?? "Design"} is added to your cart";
                //await NotifyAsync(notifId, userId, message);

                // Retrieve add_ons_id for the newly created suborder
                List<int> addOnIds = await GetAddOnIdsBySuborderIdAsync(suborderIdBinary);

                // Prepare the suborder response
                var suborderResponse = new SuborderResponse
                {
                    suborderId = suborderIdBinary,
                    addonId = addOnIds // Use the add-on IDs retrieved from the database (as integers)
                };

                return Ok(suborderResponse); // Return the suborder response
            }
            catch (Exception ex)
            {
                // Log and return an error message if an exception occurs
                _logger.LogError(ex, "An error occurred while creating the order");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }

        private async Task<List<int>> GetAddOnIdsBySuborderIdAsync(string suborderId)
        {
            var addOnIds = new List<int>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT add_ons_id FROM orderaddons WHERE order_id = @suborderId";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborderId", suborderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            if (reader["add_ons_id"] != DBNull.Value)
                            {
                                addOnIds.Add(Convert.ToInt32(reader["add_ons_id"]));
                            }
                        }
                    }
                }
            }

            return addOnIds;
        }


        private async Task<string> GetDesignShapes(string design)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string designQuery = "SELECT shape_name FROM designshapes WHERE design_id = @design_id";
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

        private async Task<string> GetPastryMaterialIdByDesignIds(string designIdHex)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT pastry_material_id FROM pastrymaterials WHERE design_id = @designId";
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

        private async Task InsertOrder(Order order, string designId, string flavor, string size, string pastryId, string customerId, string color, string shape, string suborderid)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO suborders (
            suborder_id, order_id, customer_id, customer_name, employee_id, created_at, status, design_id, price, quantity, 
            last_updated_by, last_updated_at, is_active, description, flavor, size, color, shape, design_name, pastry_id) 
            VALUES (@suborderid, NULL, @customerId, @CustomerName, NULL, NOW(), @status, @designId, @price, 
            @quantity, NULL, NOW(), @isActive, @Description, @Flavor, @Size, @color, @shape, @DesignName, @PastryId)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborderid", suborderid);
                    command.Parameters.AddWithValue("@customerId", customerId);
                    command.Parameters.AddWithValue("@CustomerName", order.customerName);
                    command.Parameters.AddWithValue("@designId", designId);
                    command.Parameters.AddWithValue("@status", order.status);
                    command.Parameters.AddWithValue("@price", order.price);
                    command.Parameters.AddWithValue("@quantity", order.quantity);
                    command.Parameters.AddWithValue("@isActive", order.isActive);
                    command.Parameters.AddWithValue("@color", color);
                    command.Parameters.AddWithValue("@shape", shape);
                    command.Parameters.AddWithValue("@Description", order.description);
                    command.Parameters.AddWithValue("@Flavor", flavor);
                    command.Parameters.AddWithValue("@Size", size);
                    command.Parameters.AddWithValue("@DesignName", order.designName);
                    command.Parameters.AddWithValue("@PastryId", pastryId);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<List<string>> GetAdmins()
        {
            List<string> userIds = new List<string>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT UserId FROM users WHERE Type IN (2, 3, 4)";

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



        [HttpPost("/culo-api/v1/current-user/cart/checkout")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> PatchOrderTypeAndPickupDate([FromBody] CheckOutRequest checkOutRequest)
        {
            try
            {
                //checks if suborder id list is empty
                if (checkOutRequest.suborderIds == null || !checkOutRequest.suborderIds.Any())
                {
                    return BadRequest("suborder ids cannot be null");
                }


                // Retrieve customer username from claims
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                var user = await userManager.FindByNameAsync(customerUsername);

                string customerId = user.Id;


                // Validate type
                if (!checkOutRequest.type.Equals("normal", StringComparison.OrdinalIgnoreCase) &&
                    !checkOutRequest.type.Equals("rush", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest("Invalid type. Please choose 'normal' or 'rush'.");
                }

                // Parse and validate the pickup date and time
                if (!DateTime.TryParseExact(checkOutRequest.pickupDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate) ||
                    !DateTime.TryParseExact(checkOutRequest.pickupTime, "h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedTime))
                {
                    return BadRequest("Invalid pickup date or time format. Use 'yyyy-MM-dd' for date and 'h:mm tt' for time.");
                }

                // Combine the parsed date and time into a single DateTime object
                DateTime pickupDateTime = new DateTime(parsedDate.Year, parsedDate.Month, parsedDate.Day, parsedTime.Hour, parsedTime.Minute, 0);

                // Generate a new GUID for the OrderId (this order will be shared across multiple suborders)
                Guid orderId = Guid.NewGuid();
                string orderIdBinary =orderId.ToString().ToLower();

                // Insert a new order
                await InsertOrderWithOrderId(orderIdBinary, customerUsername, customerId, pickupDateTime, checkOutRequest.type, checkOutRequest.payment);

                // Loop through each suborderid in the request and update them with the new orderId
                foreach (var suborderId in checkOutRequest.suborderIds)
                {
                    string suborderIdBinary = suborderId.ToString().ToLower();

                    // Check if the suborder exists in the suborders table
                    if (!await DoesSuborderExist(suborderIdBinary))
                    {
                        return NotFound($"Suborder with ID '{suborderId}' not found.");
                    }

                    // Update the suborder with the new orderId
                    await UpdateSuborderWithOrderId(suborderIdBinary, orderIdBinary);
                }

                return Ok($"Order for {checkOutRequest.suborderIds.Count} suborder(s) has been successfully created with order ID '{orderIdBinary}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the request.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        private async Task<bool> AreAllSubordersPricedAsync(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL query to check if any suborder has a price <= 0 for the given order ID
                string sql = @"SELECT COUNT(*) FROM suborders 
                       WHERE order_id = @orderId AND price <= 0";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    // Execute the query to count suborders with price <= 0
                    int subordersWithZeroOrNegativePrice = Convert.ToInt32(await command.ExecuteScalarAsync());

                    // If there are no suborders with price <= 0, return true; otherwise, return false
                    return subordersWithZeroOrNegativePrice == 0;
                }
            }
        }


        [HttpPost("/culo-api/v1/orders/{orderId}/approve-order")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin)]
        public async Task<IActionResult> ApprovingOrder(string orderId)
        {
            try
            {
                // Check if orderId is valid
                if (string.IsNullOrEmpty(orderId) || !Guid.TryParse(orderId, out var parsedOrderId))
                {
                    return BadRequest("Invalid or missing orderId.");
                }

                string orderIdBinary = orderId.ToLower();

                bool doesHavePrice = await AreAllSubordersPricedAsync(orderIdBinary);

                if (!doesHavePrice)
                {
                    return BadRequest("All suborders must have a price greater than 0.");
                }

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    // Insert new order into the 'orders' table
                    string sqlInsert = @"UPDATE orders SET status = 'to pay' WHERE order_id = @orderid AND status ='for approval'";

                    using (var command = new MySqlCommand(sqlInsert, connection))
                    {
                        command.Parameters.AddWithValue("@orderid", orderIdBinary);
                        await command.ExecuteNonQueryAsync();
                    }

                    // Retrieve suborder IDs
                    List<string> suborderIds = await GetSuborderIdAsync(orderIdBinary);
                    if (suborderIds == null || suborderIds.Count == 0)
                    {
                        return NotFound("No suborder ID found for the given order ID.");
                    }

                    // Update each suborder status
                    foreach (var suborderId in suborderIds)
                    {
                        Debug.WriteLine(suborderId);
                        await SetApprovedStatus(suborderId);
                    }

                    // Call the method to get customer ID and name
                    var (customerId, customerName) = await GetCustomerInfo(orderIdBinary);

                    if (customerId != null && customerId.Length > 0)
                    {
                        // Convert the byte[] customerId to a hex string
                        string userId = customerId.ToLower();

                        Debug.Write("customer id: " + userId);

                        Guid notId = Guid.NewGuid();

                        string notifId = notId.ToString().ToLower();

                        // Construct the message
                        string message = ((customerName ?? "Unknown") + " your order has been approved; assigning artist");

                        // Send the notification
                        await NotifyAsync(notifId ,userId, message);

                        // Return success response with the created order ID
                        return Ok($"Order for {orderIdBinary} has been successfully added for approval.");
                    }
                    else
                    {
                        // If customerId is null or empty, return a response
                        return NotFound("Customer information not found.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error and return 500 status code
                _logger.LogError(ex, $"An error occurred while creating order with ID {orderId}.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        private async Task SetApprovedStatus(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sqlUpdate = "UPDATE suborders SET status = 'to pay' WHERE suborder_id = @orderId";

                using (var command = new MySqlCommand(sqlUpdate, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
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
                            string customerId = reader.GetString("customer_id");
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

        async Task<List<string>> GetSuborderIdAsync(string orderIdBinary)
        {
            List<string> suborderIds = new List<string>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Specify the columns you want to select
                string sql = "SELECT suborder_id FROM suborders WHERE order_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    // Use ExecuteReaderAsync to execute the SELECT query
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Retrieve suborder_id as a string
                            string suborderId = reader["suborder_id"].ToString();

                            // Debug.WriteLine to display the value of suborderId
                            Debug.WriteLine($"Suborder ID for order ID '{orderIdBinary}': {suborderId}");

                            // Add each suborder_id to the list
                            suborderIds.Add(suborderId);
                        }
                    }
                }
            }

            // Return the list of suborder_id strings
            return suborderIds;
        }

        private async Task SetApprovalStatus(byte[] orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sqlUpdate = "UPDATE suborders SET status = 'for approval' WHERE suborder_id = @orderId";

                using (var command = new MySqlCommand(sqlUpdate, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<bool> DoesSuborderExist(string suborderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT COUNT(1) FROM suborders WHERE suborder_id = @suborderid";
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
                string sql = "UPDATE suborders SET order_id = @orderid, status = 'for approval' WHERE suborder_id = @suborderid";
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


        private async Task InsertOrderWithOrderId(string orderIdBinary, string customerName, string customerId, DateTime pickupDateTime, string type, string payment)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"INSERT INTO orders (order_id, customer_id, customer_name, pickup_date, type, payment, status, created_at, last_updated_at) 
                       VALUES (@orderid, @customerId, @CustomerName, @pickupDateTime, @type, @payment, 'for approval', NOW(), NOW())";

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

        /*[HttpPost("/culo-api/v1/current-user/{orderId}/confirm")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> ConfirmOrder(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
            {
                return BadRequest("OrderId cannot be null or empty.");
            }

            // Convert orderId to binary format for querying
            string orderIdBinary = orderId.ToLower();

            // Check if the order exists
            var orderExists = await CheckOrderExistx(orderIdBinary);

            if (!orderExists)
            {
                return NotFound("No orders found for the specified orderId.");
            }

            // Perform the update for confirmation
            await UpdateOrderxxStatus(orderIdBinary, "confirm");

            string suborderId = await UpdateOrderxxxxStatus(orderIdBinary);

            if (suborderId == null)
            {
                return NotFound("No suborder ID found for the given order ID.");
            }

            await UpdateOrderxxxStatus(suborderId, "confirm");

            // Retrieve all employee IDs with Type 3 or 4
            List<string> users = await GetEmployeeAllId();

            foreach (var user in users)
            {

                // Convert the userId to the binary form expected in the database
                string userIdBinary = user.ToLower();

                // Construct the notification message
                string message = (" new order that needed approval has been added");

                Guid notId = Guid.NewGuid();

                string notifId = notId.ToString().ToLower();

                // Send notification to the user
                await NotifyAsync(notifId, userIdBinary, message);
            }

            return Ok("Order confirmed successfully.");
        }*/

       
        [HttpPost("/culo-api/v1/current-user/{orderId}/cancel")]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> CancelOrder(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
            {
                return BadRequest("OrderId cannot be null or empty.");
            }

            // Convert orderId to binary format for querying
            string orderIdBinary = orderId.ToLower();

            // Check if the order exists
            var orderExists = await CheckOrderExistx(orderIdBinary);

            if (!orderExists)
            {
                return NotFound("No orders found for the specified orderId.");
            }

            // Perform the update for cancellation
            await UpdateOrderxxStatus(orderIdBinary, "cancel");

            // Retrieve the list of suborder IDs for the given order ID
            List<string> suborderIds = await UpdateOrderxxxxStatusAsync(orderIdBinary);

            if (suborderIds == null || !suborderIds.Any())
            {
                return NotFound("No suborder IDs found for the given order ID.");
            }

            // Iterate through each suborderId in the list
            foreach (var suborderId in suborderIds)
            {
                await UpdateOrderxxxStatus(suborderId, "cancel");
            }

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

                string sql = "UPDATE orders SET is_active = @isActive, status = 'cancelled' WHERE order_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@isActive", isActive);
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task UpdateOrderxxxStatus(string orderIdBinary, string action)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Determine the value of is_active based on the action
                int isActive = action.Equals("confirm", StringComparison.OrdinalIgnoreCase) ? 1 :
                               action.Equals("cancel", StringComparison.OrdinalIgnoreCase) ? 0 :
                               throw new ArgumentException("Invalid action. Please choose 'confirm' or 'cancel'.");

                string sql = "UPDATE suborders SET is_active = @isActive, status = 'cancelled' WHERE suborder_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@isActive", isActive);
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private async Task<List<string>> UpdateOrderxxxxStatusAsync(string orderIdBinary)
        {
            var suborderIds = new List<string>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Query to select suborder_id based on order_id
                string sql = "SELECT suborder_id FROM suborders WHERE order_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderIdBinary);

                    // Execute the query and retrieve the result
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Retrieve each suborder_id and add it to the list
                            string suborderId = reader["suborder_id"].ToString();
                            suborderIds.Add(suborderId);

                            // Log each retrieved suborder_id
                            Debug.WriteLine($"Suborder ID for order ID '{orderIdBinary}': {suborderId}");
                        }
                    }
                }
            }

            // Return the list of suborder_id strings
            return suborderIds;
        }



        private async Task<bool> CheckOrderExistx(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
        SELECT COUNT(1) 
        FROM orders 
        WHERE order_id = @orderIdBinary";

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
                if (request.Quantity < 1 || request.Quantity > 3)
                {
                    return BadRequest("Quantity is too many. It must be between 1 and 3.");
                }

                _logger.LogInformation($"Starting AddNewAddOnToOrder for orderId: {suborderId}");

                // Convert orderId to binary(16) format without '0x' prefix
                string suborderIdBinary = suborderId.ToLower();

                // Retrieve the add-on details from the AddOns table based on the name
                var addOnDSOS = await GetAddOnByNameFromDatabase(request.AddOnName);
                if (addOnDSOS == null)
                {
                    return BadRequest($"Add-on '{request.AddOnName}' not found in the AddOns table.");
                }

                // Calculate total price
                double total = request.Quantity * addOnDSOS.price * 2;

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
                                         WHERE order_id = @orderId AND add_ons_id = @addOnsId";

                            using (var selectCommand = new MySqlCommand(selectSql, connection, transaction))
                            {
                                selectCommand.Parameters.AddWithValue("@orderId", suborderIdBinary);
                                selectCommand.Parameters.AddWithValue("@addOnsId", addOnDSOS.id);

                                int count = Convert.ToInt32(await selectCommand.ExecuteScalarAsync());

                                if (count == 0)
                                {
                                    // Insert new add-on into orderaddons
                                    string insertSql = @"INSERT INTO orderaddons (order_id, add_ons_id, quantity, total, name, price)
                                                 VALUES (@orderId, @addOnsId, @quantity, @total, @name, @price)";

                                    using (var insertCommand = new MySqlCommand(insertSql, connection, transaction))
                                    {
                                        insertCommand.Parameters.AddWithValue("@orderId", suborderIdBinary);
                                        insertCommand.Parameters.AddWithValue("@addOnsId", addOnDSOS.id);
                                        insertCommand.Parameters.AddWithValue("@name", addOnDSOS.addOnName);
                                        insertCommand.Parameters.AddWithValue("@price", addOnDSOS.price);
                                        insertCommand.Parameters.AddWithValue("@quantity", request.Quantity);
                                        insertCommand.Parameters.AddWithValue("@total", total);

                                        _logger.LogInformation($"Inserting add-on '{request.AddOnName}' with quantity '{request.Quantity}', price '{addOnDSOS.price}', and total '{total}' into orderaddons");

                                        await insertCommand.ExecuteNonQueryAsync();
                                    }
                                }
                                else
                                {
                                    // Update existing add-on in orderaddons
                                    string updateSql = @"UPDATE orderaddons 
                                                 SET quantity = @quantity, total = @total 
                                                 WHERE order_id = @orderId AND add_ons_id = @addOnsId";

                                    using (var updateCommand = new MySqlCommand(updateSql, connection, transaction))
                                    {
                                        updateCommand.Parameters.AddWithValue("@quantity", request.Quantity);
                                        updateCommand.Parameters.AddWithValue("@total", total);
                                        updateCommand.Parameters.AddWithValue("@orderId", suborderIdBinary);
                                        updateCommand.Parameters.AddWithValue("@addOnsId", addOnDSOS.id);

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
                                id = reader.GetInt32("add_ons_id"),
                                addOnName = reader.GetString("name"),
                                price = reader.GetDouble("price")
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

        [HttpPost("suborders/{suborderId}/assign")]//done 
        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> AssignEmployeeToOrder(string suborderId, [FromBody] AssignEmp assign)
        {
            try
            {
                // Convert the orderId from GUID string to binary(16) format without '0x' prefix
                string suborderIdBinary = suborderId.ToLower();
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
                    return NotFound($"Employee with username '{employeeName}' not found. Please try another name.");
                }

                // Update the order with the employee ID and employee name
                await UpdateOrderEmployeeId(suborderIdBinary, empdBinary, employeeName);

                await UpdateOrderStatusToBaking(suborderIdBinary);

                Guid notId = Guid.NewGuid();

                string notifId = notId.ToString().ToLower();

                string userId = empdBinary;

                string message = ((employeeName ?? "Unknown") + " new order has been assigned to you");

                await NotifyAsync(notifId, userId, message);

                return Ok($"Employee with username '{employeeName}' has been successfully assigned to order with ID '{suborderId}'.");
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

                string sql = "SELECT UserName FROM users WHERE UserId = UNHEX(@id) AND Type = 2";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", empId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Retrieve the DisplayName as a string
                            EmpId = reader.GetString("UserName");
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
            string orderIdBinary = suborderId.ToLower();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "DELETE FROM orderaddons WHERE order_id = @suborderId AND add_ons_id = @addonId";

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

        private async Task<string> GetUserIdByAllUsername(string username)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT Id FROM aspnetusers WHERE Username = @username";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@username", username);
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        // Return the string value directly
                        string userId = (string)result;

                        // Debug.WriteLine to display the value of userId
                        Debug.WriteLine($"UserId for username '{username}': {userId}");

                        return userId;
                    }
                    else
                    {
                        return null; // User not found
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

                    string sql = @"SELECT suborder_id, order_id, customer_id, employee_id, created_at, pastry_id, status, design_id, design_name, price, quantity, last_updated_by, last_updated_at, is_active, description, flavor, size, customer_name, employee_name, shape, color 
                           FROM suborders";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                // Retrieve fields as strings (assuming suborder_id and others are varchar in GUID format)
                                string? orderId = reader.IsDBNull(reader.GetOrdinal("order_id")) ? null : reader.GetString(reader.GetOrdinal("order_id"));
                                string suborderId = reader.GetString(reader.GetOrdinal("suborder_id"));
                                string customerId = reader.IsDBNull(reader.GetOrdinal("customer_id")) ? string.Empty : reader.GetString(reader.GetOrdinal("customer_id"));
                                string employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id")) ? string.Empty : reader.GetString(reader.GetOrdinal("employee_id"));

                                // Nullable strings
                                string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name")) ? null : reader.GetString(reader.GetOrdinal("employee_name"));
                                string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by"));

                                // Construct Order object and add to list
                                orders.Add(new Order
                                {
                                    orderId = orderId, // Nullable string for orderId
                                    suborderId = suborderId, // Non-nullable string for suborderId
                                    customerId = customerId, // Nullable string for customerId
                                    employeeId = employeeId, // Nullable string for employeeId
                                    pastryId = reader.IsDBNull(reader.GetOrdinal("pastry_id"))
            ? null
            : reader.GetString(reader.GetOrdinal("pastry_id")),
                                    createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    status = reader.GetString(reader.GetOrdinal("status")),
                                    color = reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.GetString(reader.GetOrdinal("shape")),
                                    designId = reader.GetString(reader.GetOrdinal("design_id")), // Assuming designId is stored as binary(16) or UUID
                                    designName = reader.IsDBNull(reader.GetOrdinal("design_name"))
             ? null
             : reader.GetString(reader.GetOrdinal("design_name")),

                                    price = reader.GetDouble(reader.GetOrdinal("price")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = lastUpdatedBy,
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                    ? (DateTime?)null
                                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    description = reader.GetString(reader.GetOrdinal("description")),
                                    flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                    size = reader.GetString(reader.GetOrdinal("size")),
                                    customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                    employeeName = employeeName
                                });
                            }
                        }
                    }
                }

                return Ok(orders.Count > 0 ? orders : new List<Order>());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        /*[HttpGet("/culo-api/v1/current-user/custom-orders")]
        [ProducesResponseType(typeof(CustomPartial), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetAllCustomInitialOrdersByCustomerIds([FromQuery] string? search = null)
        {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(customerUsername))
            {
                return Unauthorized("User is not authorized");
            }

            string customerId = await GetUserIdByAllUsername(customerUsername);
            if (customerId == null || customerId.Length == 0)
            {
                return BadRequest("Customer not found");
            }

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
        }*/

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
                            // Retrieve fields as strings since they are varchar(255) in GUID format
                            string? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                              ? null
                                              : reader.GetString(reader.GetOrdinal("order_id"));

                            string customId = reader.GetString(reader.GetOrdinal("custom_id"));

                            string? customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                       ? null
                                                       : reader.GetString(reader.GetOrdinal("customer_id"));

                            string? designId = reader.IsDBNull(reader.GetOrdinal("design_id"))
                                               ? null
                                               : reader.GetString(reader.GetOrdinal("design_id"));

                            // Nullable price handling
                            double? Price = reader.IsDBNull(reader.GetOrdinal("price"))
                                            ? (double?)null
                                            : reader.GetDouble(reader.GetOrdinal("price"));

                            // Add the new CustomPartial object to the orders list
                            orders.Add(new CustomPartial
                            {
                                orderId = orderId, // Nullable string for orderId
                                customId = customId, // Non-nullable string for customId
                                customerId = customerIdFromDb, // Nullable string for customerId
                                createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                designId = designId, // Nullable string for designId
                                Price = Price,
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                color = reader.IsDBNull(reader.GetOrdinal("color")) ? string.Empty : reader.GetString(reader.GetOrdinal("color")),
                                designName = reader.IsDBNull(reader.GetOrdinal("design_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("design_name")),
                                shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? string.Empty : reader.GetString(reader.GetOrdinal("shape")),
                                tier = reader.IsDBNull(reader.GetOrdinal("tier")) ? string.Empty : reader.GetString(reader.GetOrdinal("tier")),
                                cover = reader.IsDBNull(reader.GetOrdinal("cover")) ? string.Empty : reader.GetString(reader.GetOrdinal("cover")),
                                description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                                size = reader.IsDBNull(reader.GetOrdinal("size")) ? string.Empty : reader.GetString(reader.GetOrdinal("size")),
                                flavor = reader.IsDBNull(reader.GetOrdinal("flavor")) ? string.Empty : reader.GetString(reader.GetOrdinal("flavor")),
                                picture = reader.IsDBNull(reader.GetOrdinal("picture_url")) ? string.Empty : reader.GetString(reader.GetOrdinal("picture_url")),
                                message = reader.IsDBNull(reader.GetOrdinal("message")) ? string.Empty : reader.GetString(reader.GetOrdinal("message"))
                            });
                        }
                    }
                }
            }

            return orders; // Return the list of orders
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
                    // Add the parameters to the SQL command
                    command.Parameters.AddWithValue("@name", name);
                    command.Parameters.AddWithValue("@id", id);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Handle fields as strings (varchar(255)) instead of GUIDs
                            string? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                              ? null
                                              : reader.GetString(reader.GetOrdinal("order_id"));

                            string customId = reader.GetString(reader.GetOrdinal("custom_id"));

                            string? customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                       ? null
                                                       : reader.GetString(reader.GetOrdinal("customer_id"));

                            string? designId = reader.IsDBNull(reader.GetOrdinal("design_id"))
                                               ? null
                                               : reader.GetString(reader.GetOrdinal("design_id"));

                            // Add the new CustomPartial object to the orders list
                            orders.Add(new CustomPartial
                            {
                                orderId = orderId, // Nullable string for orderId
                                customId = customId, // Non-nullable string for customId
                                customerId = customerIdFromDb, // Nullable string for customerId
                                createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                designId = designId, // Nullable string for designId
                                Price = reader.GetDouble(reader.GetOrdinal("price")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                designName = reader.IsDBNull(reader.GetOrdinal("design_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("design_name")),
                                color = reader.IsDBNull(reader.GetOrdinal("color")) ? string.Empty : reader.GetString(reader.GetOrdinal("color")),
                                shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? string.Empty : reader.GetString(reader.GetOrdinal("shape")),
                                tier = reader.IsDBNull(reader.GetOrdinal("tier")) ? string.Empty : reader.GetString(reader.GetOrdinal("tier")),
                                cover = reader.IsDBNull(reader.GetOrdinal("cover")) ? string.Empty : reader.GetString(reader.GetOrdinal("cover")),
                                description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
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
                            // Handle fields as strings (varchar(255)) instead of GUIDs
                            string? orderId = reader.IsDBNull(reader.GetOrdinal("order_id"))
                                              ? null
                                              : reader.GetString(reader.GetOrdinal("order_id"));

                            string customId = reader.GetString(reader.GetOrdinal("custom_id"));

                            string? customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id"))
                                                        ? null
                                                        : reader.GetString(reader.GetOrdinal("customer_id"));

                            string? employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))
                                                 ? null
                                                 : reader.GetString(reader.GetOrdinal("employee_id"));

                            string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                                   ? null
                                                   : reader.GetString(reader.GetOrdinal("employee_name"));

                            double? Price = reader.IsDBNull(reader.GetOrdinal("price")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("price"));

                            // Add the new CustomPartial object to the orders list
                            orders.Add(new CustomPartial
                            {
                                orderId = orderId, // Nullable string for orderId
                                customId = customId, // Non-nullable string for customId
                                customerId = customerIdFromDb, // Nullable string for customerId
                                employeeId = employeeId, // Nullable string for employeeId
                                employeeName = employeeName, // Nullable string for employeeName
                                createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                designId = reader.IsDBNull(reader.GetOrdinal("design_id")) ? null : reader.GetString(reader.GetOrdinal("design_id")), // Nullable string for designId
                                Price = Price,
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                designName = reader.IsDBNull(reader.GetOrdinal("design_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("design_name")),
                                customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                color = reader.IsDBNull(reader.GetOrdinal("color")) ? string.Empty : reader.GetString(reader.GetOrdinal("color")),
                                shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? string.Empty : reader.GetString(reader.GetOrdinal("shape")),
                                tier = reader.IsDBNull(reader.GetOrdinal("tier")) ? string.Empty : reader.GetString(reader.GetOrdinal("tier")),
                                cover = reader.IsDBNull(reader.GetOrdinal("cover")) ? string.Empty : reader.GetString(reader.GetOrdinal("cover")),
                                description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
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


        /*[HttpGet("custom-orders/{customid}/full")]
        [ProducesResponseType(typeof(CustomOrderFull), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetAllCustomOrdersByCustomerId(string customid)
        {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(customerUsername))
            {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            string customerId = await GetUserIdByAllUsername(customerUsername);
            if (string.IsNullOrEmpty(customerId))
            {
                return BadRequest("Customer not found");
            }

            // Convert customid to binary format
            string customidBinary = customid.ToLower();

            // Check if the custom order exists in the customorders table
            if (!await DoesCustomOrderExist(customidBinary))
            {
                return NotFound($"Custom order with ID '{customidBinary}' not found.");
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
            WHERE custom_id = @suborderIdBinary";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@suborderIdBinary", customidBinary);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string? orderId = reader.IsDBNull(reader.GetOrdinal("order_id")) ? null : Encoding.UTF8.GetString((byte[])reader["order_id"]);
                                string suborderId = Encoding.UTF8.GetString((byte[])reader["custom_id"]);
                                string customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id")) ? string.Empty : Encoding.UTF8.GetString((byte[])reader["customer_id"]);
                                string employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id")) ? string.Empty : Encoding.UTF8.GetString((byte[])reader["employee_id"]);
                                string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name")) ? null : reader.GetString(reader.GetOrdinal("employee_name"));
                                double? price = reader.IsDBNull(reader.GetOrdinal("price")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("price"));

                                orders.Add(new CustomOrderFull
                                {
                                    orderId = orderId,
                                    customId = suborderId,
                                    customerId = customerIdFromDb,
                                    employeeId = employeeId,
                                    employeeName = employeeName,
                                    createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    designId = Encoding.UTF8.GetString((byte[])reader["design_id"]),
                                    price = price,
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    designName = reader.IsDBNull(reader.GetOrdinal("design_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("design_name")),
                                    customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                    color = reader.IsDBNull(reader.GetOrdinal("color")) ? string.Empty : reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? string.Empty : reader.GetString(reader.GetOrdinal("shape")),
                                    tier = reader.IsDBNull(reader.GetOrdinal("tier")) ? string.Empty : reader.GetString(reader.GetOrdinal("tier")),
                                    cover = reader.IsDBNull(reader.GetOrdinal("cover")) ? string.Empty : reader.GetString(reader.GetOrdinal("cover")),
                                    description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
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
                        // Scan the orders table using the retrieved orderId from the custom orders table
                        foreach (var order in orders)
                        {
                            if (!string.IsNullOrEmpty(order.orderId))
                            {
                                // Convert orderId to binary format
                                string orderIdBinary = order.orderId.ToLower();
                                Debug.Write(orderIdBinary);

                                var orderDetails = await GetOrderDetailsByOrderId(connection, orderIdBinary);
                                if (orderDetails != null)
                                {
                                    // Populate additional fields from the orders table
                                    order.payment = orderDetails.payment;
                                    order.pickupDateTime = orderDetails.pickupDateTime;
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
        }*/

        private async Task<bool> DoesCustomOrderExist(string CustomorderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT COUNT(1) FROM customorders WHERE custom_id = @suborderid";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborderid", CustomorderId);
                    return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
                }
            }
        }

        [HttpGet("partial-details")]
        [ProducesResponseType(typeof(AdminInitial), StatusCodes.Status200OK)]
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
                        search.Equals("to review", StringComparison.OrdinalIgnoreCase) ||
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

                string sql = @"
            SELECT order_id, type, created_at, status, payment, pickup_date, last_updated_by, last_updated_at, is_active, customer_name 
            FROM orders 
            WHERE status IN('baking', 'assigning artist', 'done', 'for approval', 'to pay', 'for pick up', 'cancelled')
            ORDER BY 
                CASE 
                    WHEN status = 'for approval' THEN 1
                    WHEN status = 'assigning artist' THEN 2
                    WHEN status = 'baking' THEN 3
                    WHEN status = 'to pay' THEN 4
                    WHEN status = 'for pick up' THEN 5
                    WHEN status = 'done' THEN 6
                    WHEN status = 'cancelled' THEN 7
                    ELSE 8 -- Default for other statuses    
                END";

                using (var command = new MySqlCommand(sql, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string orderIdFromDb = reader["order_id"].ToString();
                            string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                            // Initialize an AdminInitial object with order details
                            AdminInitial order = new AdminInitial
                            {
                                orderId = orderIdFromDb,
                                createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                lastUpdatedBy = lastUpdatedBy ?? "",
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                ? (DateTime?)null
                                                : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                payment = reader.IsDBNull(reader.GetOrdinal("payment")) ? (string)null : reader.GetString(reader.GetOrdinal("payment")),
                                type = reader.GetString(reader.GetOrdinal("type")),
                                pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                status = reader.GetString(reader.GetOrdinal("status")),
                            };

                            // If the order ID is valid, fetch design details and total price
                            if (!string.IsNullOrEmpty(order.orderId))
                            {
                                // Pass the order ID string to FetchDesignAndTotalPriceAsync
                                List<AdminInitial> designDetails = await FetchDesignAndTotalAsync(order.orderId);
                                
                                // Append design details (like DesignName and Total) to the order
                                if (designDetails.Any())
                                {
                                    order.designId = designDetails.First().designId;
                                    order.designName = designDetails.First().designName;
                                }
                            }

                            orders.Add(order); // Add the order with design details to the list
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
    design_id, design_name 
FROM suborders WHERE order_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Add the order ID parameter to the SQL command
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string designId = reader["design_id"].ToString();

                            orders.Add(new AdminInitial
                            {
                                designId = designId, // Retrieve design_id as string
                                designName = !reader.IsDBNull(reader.GetOrdinal("design_name"))
        ? reader.GetString(reader.GetOrdinal("design_name"))
        : null,
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
WHERE s.status = @status AND o.status IN('baking', 'to review', 'for update', 'assigning artist', 'done', 'for approval')";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@status", status);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {

                            string orderId = reader["order_id"].ToString();

                            string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                            // Initialize an AdminInitial object with order details
                            AdminInitial order = new AdminInitial
                            {
                                orderId = orderId, // Set orderId as a string
                                createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                lastUpdatedBy = lastUpdatedBy ?? "",
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                ? (DateTime?)null
                                                : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                payment = reader.IsDBNull(reader.GetOrdinal("payment")) ? (string)null : reader.GetString(reader.GetOrdinal("payment")),
                                type = reader.GetString(reader.GetOrdinal("type")),
                                pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                status = reader.GetString(reader.GetOrdinal("status")),
                            };

                            // If the order ID is valid, fetch design details and total price
                            if (!string.IsNullOrEmpty(order.orderId))
                            {
                                // Pass the order ID as a string to FetchDesignAndTotalAsync
                                List<AdminInitial> designDetails = await FetchDesignAndTotalAsync(order.orderId);

                                // Append design details (like DesignName and Total) to the order
                                if (designDetails.Any())
                                {
                                    order.designId = designDetails.First().designId;
                                    order.designName = designDetails.First().designName;
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
        WHERE s.employee_name = @name AND o.status IN('baking', 'to review', 'for update', 'assigning artist', 'done', 'for approval')";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@name", name);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string orderId = reader["order_id"].ToString();

                            string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                            // Initialize an AdminInitial object with order details
                            AdminInitial order = new AdminInitial
                            {
                                orderId = orderId,
                                createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                lastUpdatedBy = lastUpdatedBy ?? "",
                                lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                ? (DateTime?)null
                                                : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                payment = reader.IsDBNull(reader.GetOrdinal("payment")) ? (string)null : reader.GetString(reader.GetOrdinal("payment")),
                                type = reader.GetString(reader.GetOrdinal("type")),
                                pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                status = reader.GetString(reader.GetOrdinal("status")),
                            };

                            // If the order ID is valid, fetch design details and total price
                            if (order.orderId != null)
                            {
                                // Convert order ID to string and pass to FetchDesignAndTotalPriceAsync
                                string orderIdString = order.orderId.ToLower();
                                List<AdminInitial> designDetails = await FetchDesignAndTotalAsync(orderIdString);

                                // Append design details (like DesignName and Total) to the order
                                if (designDetails.Any())
                                {
                                    order.designId = designDetails.First().designId;
                                    order.designName = designDetails.First().designName;
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
        [ProducesResponseType(typeof(CheckOutDetails), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetFullOrderDetailsByAdmin(string orderId)
        {
            // Convert the orderId to binary
            string orderIdBinary = orderId.ToLower();

            try
            {
                    // Standard order flow
                    var orderDetails = await GetOrderDetailx(orderIdBinary);

                if (orderDetails != null)
                {
                    // Initialize total sum for both sections
                    double totalSum = 0;

                    // Section 1: Calculate totals for orderItems
                    orderDetails.orderItems = await GetSubordersDetails(orderIdBinary);
                    foreach (var suborder in orderDetails.orderItems)
                    {
                        // Retrieve add-ons for each suborder
                        suborder.orderAddons = await GetOrderAddonsDetails(suborder.suborderId);

                        // Calculate the total for this suborder
                        double addOnsTotal = suborder.orderAddons.Sum(addon => addon.addOnTotal);
                        suborder.subOrderTotal = (suborder.price ?? 0) * suborder.quantity + addOnsTotal;

                        // Add to the overall total
                        totalSum += suborder.subOrderTotal;
                    }

                    // Section 2: Calculate totals for customItems
                    orderDetails.customItems = await GetSuborderDetail(orderIdBinary);
                    foreach (var suborders in orderDetails.customItems)
                    {
                        // Retrieve add-ons for each suborder
                        suborders.orderAddons = await GetOrderAddonsDetails(suborders.suborderId);

                        // Calculate the total for this suborder
                        double addOnsTotal = suborders.orderAddons.Sum(addon => addon.addOnTotal);
                        suborders.subOrderTotal = (suborders.price ?? 0) * suborders.quantity + addOnsTotal;

                        // Add to the overall total
                        totalSum += suborders.subOrderTotal;
                    }

                    // Assign the combined total to orderTotal
                    orderDetails.orderTotal = totalSum;
                }

                return Ok(orderDetails);
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
                       WHERE order_id = @orderBinary";

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
        [ProducesResponseType(typeof(Cart), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetCartOrdersByCustomerId()
        {
            // Fetch customer username from claims
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(customerUsername))
            {
                return Unauthorized("User is not authorized");
            }

            var user = await userManager.FindByNameAsync(customerUsername);

            string customerId = user.Id;
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

                    // First, fetch all the suborders
                    string sql = @"
SELECT 
    s.suborder_id, s.order_id, s.customer_id, s.employee_id, s.created_at, 
    s.status, s.design_id, s.design_name, s.price, s.quantity, 
    s.last_updated_by, s.last_updated_at, s.is_active, s.description, 
    s.flavor, s.size, s.customer_name, s.employee_name, s.shape, s.color, 
    s.pastry_id, c.cover -- Retrieving the 'cover' column from customorders
FROM 
    suborders s
LEFT JOIN 
    customorders c ON s.suborder_id = c.suborder_id -- Joining with customorders on suborder_id
WHERE 
    s.customer_id = @customerId 
    AND s.status IN ('cart');
";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerId", customerId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string suborderId = reader["suborder_id"].ToString();
                                string designId = reader["design_id"].ToString();

                                double ingredientPrice = reader.GetDouble(reader.GetOrdinal("price"));

                                orders.Add(new Cart
                                {
                                    suborderId = suborderId,
                                    pastryId = reader.IsDBNull(reader.GetOrdinal("pastry_id")) ? null : reader.GetString(reader.GetOrdinal("pastry_id")),
                                    status = reader.GetString(reader.GetOrdinal("status")),
                                    color = reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.GetString(reader.GetOrdinal("shape")),
                                    designId = designId,
                                    designName = reader.IsDBNull(reader.GetOrdinal("design_name"))
             ? null
             : reader.GetString(reader.GetOrdinal("design_name")),

                                    price = ingredientPrice, // Temporarily set price without addons
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                                    flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                    size = reader.GetString(reader.GetOrdinal("size")),
                                    cover = reader.IsDBNull(reader.GetOrdinal("cover")) ? null : reader.GetString(reader.GetOrdinal("cover")),
                                });
                            }
                        }
                    }

                    // Now fetch the addons for each suborder and update the prices
                    foreach (var order in orders)
                    {
                        double addonsTotal = await GetAddonsTotalForSuborder(connection, order.suborderId);
                        order.price += addonsTotal; // Update the final price
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


        private async Task<double> GetAddonsTotalForSuborder(MySqlConnection connection, string suborderId)
        {
            string sql = @"SELECT IFNULL(SUM(total), 0) FROM orderaddons WHERE order_id = @suborderId";

            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@suborderId", suborderId.ToString());

                object result = await command.ExecuteScalarAsync();
                return result != null ? Convert.ToDouble(result) : 0.0;
            }
        }


        // Private async method to calculate addon price for the order
        private async Task<double?> GetAddonPriceAsync(string orderIdBinary)
        {
            double? addonPrice = null;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
        SELECT SUM(price * quantity) AS TotalAddonPrice
        FROM orderaddons
        WHERE order_id = @orderId";

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
        [ProducesResponseType(typeof(CustomerInitial), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToPayInitialOrdersByCustomerIds()
        {
            try
            {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                var user = await userManager.FindByNameAsync(customerUsername);

                string customerId = user.Id;
                if (customerId == null || customerId.Length == 0)
                {
                    return BadRequest("Customer not found");
                }

                // Fetch all orders (no search or filtering logic)
                List<CustomerInitial> orders = await FetchInitialToPayCustomerOrdersAsync(customerId);

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

        private async Task<List<CustomerInitial>> FetchInitialToPayCustomerOrdersAsync(string customerid)
        {
            List<CustomerInitial> orders = new List<CustomerInitial>();

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
                            string orderId = reader["order_id"].ToString();

                            // Initialize a CustomerInitial object with order details
                            CustomerInitial order = new CustomerInitial
                            {
                                orderId = orderId,
                                type = !reader.IsDBNull(reader.GetOrdinal("type"))
                                        ? reader.GetString(reader.GetOrdinal("type"))
                                        : null,
                                pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                payment = !reader.IsDBNull(reader.GetOrdinal("payment"))
                                           ? reader.GetString(reader.GetOrdinal("payment"))
                                           : null,
                                status = reader.IsDBNull(reader.GetOrdinal("status"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("status")),
                                price = new Prices() // Initialize the price list
                            };

                            string orderIdString = orderId.ToLower();

                            // If the order ID is valid, fetch the total price and design details
                            if (!string.IsNullOrEmpty(orderId))
                            {

                                double totalPrice = await CalculateTotalPriceForOrderAsync(orderIdString);

                                // Calculate half price
                                double halfPrice = totalPrice / 2;

                                // Add the total price to the Prices list in the CustomerInitial object
                                order.price = new Prices
                                {
                                    full = totalPrice,  // Assign the total price to the full property
                                    half = halfPrice    // Assign the half price to the half property
                                };

                                CustomerInitial fullOrderDetails = await GetFullOrderDetailsByCustomerAsync(order.orderId);
                                order.orderItems = fullOrderDetails.orderItems;  // Capture orderItems

                            }

                            CustomerInitial customOrderDetails = await GetCustomOrderDetailsByCustomerAsync(order.orderId);
                            order.customItems = customOrderDetails.customItems;

                            orders.Add(order); // Add the order with design details to the list
                        }
                    }
                }
            }

            return orders;
        }

        private async Task<double> CalculateTotalPriceForOrderAsync(string orderId)
        {
            double totalPrice = 0;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Retrieve the suborder_id and sum of price * quantity from suborders table
                string sqlSuborders = @"SELECT suborder_id, (price * quantity) AS SuborderTotal
                                FROM suborders
                                WHERE order_id = @orderId";
                using (var command = new MySqlCommand(sqlSuborders, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Get the suborder_id as a string for further usage
                            string suborderId = reader["suborder_id"].ToString().ToLower();

                            // Sum the total price for the suborder (price * quantity)
                            totalPrice += reader.GetDouble(reader.GetOrdinal("SuborderTotal"));

                            // Calculate the addon price for this suborder (nullable)
                            double? addonPrice = await CalculateTotalAddonPriceForSuborderAsync(suborderId);

                            // Only add addonPrice to total if it's not null
                            if (addonPrice.HasValue)
                            {
                                totalPrice += addonPrice.Value;
                            }
                        }
                    }
                }
            }

            return totalPrice;
        }

        private async Task<double?> CalculateTotalAddonPriceForSuborderAsync(string suborderId)
        {
            double? addonTotal = null;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Sum all the total column from orderaddons table for this suborder
                string sqlAddons = @"SELECT SUM(total) AS AddonTotal
                             FROM orderaddons
                             WHERE order_id = @suborderId";
                using (var command = new MySqlCommand(sqlAddons, connection))
                {
                    command.Parameters.AddWithValue("@suborderId", suborderId);

                    object result = await command.ExecuteScalarAsync();
                    if (result != DBNull.Value)
                    {
                        addonTotal = Convert.ToDouble(result);
                    }
                }
            }

            return addonTotal;
        }

        [HttpGet("/culo-api/v1/current-user/to-receive")]
        [ProducesResponseType(typeof(CustomerInitial), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToReceiveInitialOrdersByCustomerIds()
        {
            try
            {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                var user = await userManager.FindByNameAsync(customerUsername);

                string customerId = user.Id;
                if (customerId == null || customerId.Length == 0)
                {
                    return BadRequest("Customer not found");
                }

                // Fetch all orders (no search or filtering logic)
                List<CustomerInitial> orders = await FetchInitialToReceiveCustomerOrdersAsync(customerId);

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

        private async Task<List<CustomerInitial>> FetchInitialToReceiveCustomerOrdersAsync(string customerid)
        {
            List<CustomerInitial> orders = new List<CustomerInitial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @" SELECT order_id, customer_id, type, created_at, status, payment, pickup_date, last_updated_by, last_updated_at, is_active, customer_name 
                        FROM orders WHERE status IN ('for pick up', 'done', 'baking') AND customer_id = @customer_id";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customer_id", customerid);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string orderId = reader["order_id"].ToString();

                            // Initialize a CustomerInitial object with order details
                            CustomerInitial order = new CustomerInitial
                            {
                                orderId = orderId,
                                type = !reader.IsDBNull(reader.GetOrdinal("type"))
                                        ? reader.GetString(reader.GetOrdinal("type"))
                                        : null,
                                pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                payment = !reader.IsDBNull(reader.GetOrdinal("payment"))
                                           ? reader.GetString(reader.GetOrdinal("payment"))
                                           : null,
                                status = !reader.IsDBNull(reader.GetOrdinal("status"))
                                        ? reader.GetString(reader.GetOrdinal("status"))
                                        : null,
                                price = new Prices() // Initialize the price list
                            };

                            string orderIdString = orderId.ToLower();

                            // If the order ID is valid, fetch the total price and design details
                            if (!string.IsNullOrEmpty(orderId))
                            {

                                double totalPrice = await CalculateTotalPriceForOrderAsync(orderIdString);

                                // Calculate half price
                                double halfPrice = totalPrice / 2;

                                // Add the total price to the Prices list in the CustomerInitial object
                                order.price = new Prices
                                {
                                    full = totalPrice,  // Assign the total price to the full property
                                    half = halfPrice    // Assign the half price to the half property
                                };
                            }


                            CustomerInitial fullOrderDetails = await GetFullOrderDetailsByCustomerAsync(order.orderId);
                            order.orderItems = fullOrderDetails.orderItems;  // Capture orderItems

                            CustomerInitial customOrderDetails = await GetCustomOrderDetailsByCustomerAsync(order.orderId);
                            order.customItems = customOrderDetails.customItems;

                            orders.Add(order); // Add the order with design details to the list
                        }
                    }
                }
            }

            return orders;
        }

        [HttpGet("/culo-api/v1/current-user/for-approval")]
        [ProducesResponseType(typeof(CustomerInitial), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToApproveInitialOrdersByCustomerIds()
        {
            try
            {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                Debug.WriteLine(customerUsername);

                var user = await userManager.FindByNameAsync(customerUsername);

                string customerId = user.Id;
                if (customerId == null || customerId.Length == 0)
                {
                    return BadRequest("Customer not found");
                }

                // Fetch all orders (no search or filtering logic)
                List<CustomerInitial> orders = await FetchInitialToApproveCustomerOrdersAsync(customerId);

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

        private async Task<bool> IsCustomIdNull(string suborderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT custom_id FROM suborders WHERE order_id = @suborder_id";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborder_id", suborderId);

                    // Execute the command and retrieve the custom_id
                    object result = await command.ExecuteScalarAsync();

                    // Check if the custom_id is null or not
                    return result == DBNull.Value || result == null;
                }
            }
        }

        private async Task<List<CustomerInitial>> FetchInitialToApproveCustomerOrdersAsync(string customerid)
        {
            List<CustomerInitial> orders = new List<CustomerInitial>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @" SELECT order_id, customer_id, type, created_at, status, payment, pickup_date, last_updated_by, last_updated_at, is_active, customer_name 
                        FROM orders WHERE status IN ('for approval') AND customer_id = @customer_id";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@customer_id", customerid);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string orderId = reader["order_id"].ToString();

                            // Initialize a CustomerInitial object with order details
                            CustomerInitial order = new CustomerInitial
                            {
                                orderId = orderId,
                                type = !reader.IsDBNull(reader.GetOrdinal("type"))
                                        ? reader.GetString(reader.GetOrdinal("type"))
                                        : null,
                                pickup = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                         ? (DateTime?)null
                                         : reader.GetDateTime(reader.GetOrdinal("pickup_date")),
                                payment = !reader.IsDBNull(reader.GetOrdinal("payment"))
                                           ? reader.GetString(reader.GetOrdinal("payment"))
                                           : null,
                                price = new Prices() // Initialize the price list
                            };

                            // Convert order ID to string and pass to CalculateTotalPriceForOrderAsync
                            string orderIdString = order.orderId.ToLower();

                            bool isNull = await IsCustomIdNull(orderIdString);

                            // If the order ID is valid, fetch the total price and design details
                            if (!string.IsNullOrEmpty(orderId))
                            {
                                double totalPrice = await CalculateTotalPriceForOrderAsync(orderIdString);

                                // Calculate half price
                                double halfPrice = totalPrice / 2;

                                // Add the total price to the Prices list in the CustomerInitial object
                                order.price = new Prices
                                {
                                    full = totalPrice,  // Assign the total price to the full property
                                    half = halfPrice    // Assign the half price to the half property
                                };
                            }

                            CustomerInitial fullOrderDetails = await GetFullOrderDetailsByCustomerAsync(order.orderId);
                            order.orderItems = fullOrderDetails.orderItems;  // Capture orderItems

                            CustomerInitial customOrderDetails = await GetApprovalCustomOrderDetailsByCustomerAsync(order.orderId);
                            order.customItems = customOrderDetails.customItems;


                            orders.Add(order); // Add the order with design details to the list
                        }
                    }
                }
            }

            return orders;
        }



        /*[HttpGet("/culo-api/v1/current-user/for-approval")]
        [ProducesResponseType(typeof(CustomInitial), StatusCodes.Status200OK)]
        //[Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetForApprovalInitialOrdersByCustomerIds()
        {
            try
            {
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                // Get the customer's ID using the extracted username
                string customerId = await GetUserIdByAllUsername(customerUsername);
                if (customerId == null || customerId.Length == 0)
                {
                    return BadRequest("Customer not found");
                }

                // Fetch all orders (no search or filtering logic)
                List<CustomInitial> orders = await FetchInitialForApprovalCustomerOrdersAsync(customerId);

                // If no orders are found, return an empty list
                if (orders.Count == 0)
                    return Ok(new List<toPayInitial>());

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}. Stack Trace: {ex.StackTrace}");

            }
        }*/

        private async Task<List<CustomItem>> FetchforapprovalCustomItemsForSuborderAsync(string suborderId)
        {
            List<CustomItem> orders = new List<CustomItem>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @" SELECT suborder_id, order_id, status, design_id, design_name, price, quantity, description, flavor, size, color, SUM(quantity * price) AS Total, shape
                FROM suborders WHERE suborder_id = @suborderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborderId", suborderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string orderId = reader["order_id"].ToString();
                            string suborderIds = reader["suborder_id"].ToString();

                            // Initialize a CustomItem object with order details
                            CustomItem order = new CustomItem
                            {
                                suborderId = suborderIds,
                                orderId = orderId,
                                designId = reader["design_id"].ToString(),
                                designName = !reader.IsDBNull(reader.GetOrdinal("design_name"))
                                    ? reader.GetString(reader.GetOrdinal("design_name"))
                                    : null,
                                price = reader.GetDouble(reader.GetOrdinal("price")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                                flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                size = reader.GetString(reader.GetOrdinal("size")),
                                color = reader.GetString(reader.GetOrdinal("color")),
                                shape = reader.GetString(reader.GetOrdinal("shape")),
                                customTotal = reader.GetDouble(reader.GetOrdinal("Total")),
                            };

                            // Fetch tier and cover details from customorders using suborderId
                            var (tier, cover) = await FetchTierAndCoverBySuborderIdAsync(order.suborderId);
                            order.tier = tier;
                            order.cover = cover;

                            // If the design ID is valid, fetch the picture data
                            if (!string.IsNullOrEmpty(order.designId))
                            {
                                byte[]? pictureData = await GetPictureDataByDesignId(order.designId);
                                if (pictureData != null)
                                {
                                    // Convert the picture data to a base64 string
                                    order.pictureDate = Convert.FromBase64String(Convert.ToBase64String(pictureData));
                                }
                            }

                            orders.Add(order); // Add the order with all details to the list
                        }
                    }
                }
            }

            return orders;
        }

        private async Task<List<CustomItem>> FetchCustomItemsForSuborderAsync(string suborderId)
        {
            List<CustomItem> orders = new List<CustomItem>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @" SELECT suborder_id, order_id, status, design_id, design_name, price, quantity, description, flavor, size, color, SUM(quantity * price) AS Total, shape
                FROM suborders WHERE suborder_id = @suborderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborderId", suborderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string orderId = reader["order_id"].ToString();
                            string suborderIds = reader["suborder_id"].ToString();

                            // Initialize a CustomItem object with order details
                            CustomItem order = new CustomItem
                            {
                                suborderId = suborderIds,
                                orderId = orderId,
                                designId = reader["design_id"].ToString(),
                                designName = !reader.IsDBNull(reader.GetOrdinal("design_name"))
                                    ? reader.GetString(reader.GetOrdinal("design_name"))
                                    : null,
                                price = reader.GetDouble(reader.GetOrdinal("price")),
                                status = reader.IsDBNull(reader.GetOrdinal("status"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("status")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                                flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                size = reader.GetString(reader.GetOrdinal("size")),
                                color = reader.GetString(reader.GetOrdinal("color")),
                                shape = reader.GetString(reader.GetOrdinal("shape")),
                                customTotal = reader.GetDouble(reader.GetOrdinal("Total")),
                            };

                            // Fetch tier and cover details from customorders using suborderId
                            var (tier, cover) = await FetchTierAndCoverBySuborderIdAsync(order.suborderId);
                            order.tier = tier;
                            order.cover = cover;

                            // If the design ID is valid, fetch the picture data
                            if (!string.IsNullOrEmpty(order.designId))
                            {
                                byte[]? pictureData = await GetPictureDataByDesignId(order.designId);
                                if (pictureData != null)
                                {
                                    // Convert the picture data to a base64 string
                                    order.pictureDate = Convert.FromBase64String(Convert.ToBase64String(pictureData));
                                }
                            }

                            orders.Add(order); // Add the order with all details to the list
                        }
                    }
                }
            }

            return orders;
        }

        private async Task<(int? tier, string cover)> FetchTierAndCoverBySuborderIdAsync(string suborderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT tier, cover FROM customorders WHERE suborder_id = @suborder_id";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborder_id", suborderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int? tier = reader.IsDBNull(reader.GetOrdinal("tier"))
    ? (int?)null
    : reader.GetInt32(reader.GetOrdinal("tier"));

                            string cover = reader.IsDBNull(reader.GetOrdinal("cover"))
                                ? null
                                : reader.GetString(reader.GetOrdinal("cover"));

                            return (tier, cover);
                        }
                    }
                }
            }

            return (null, null); // Return null values if nothing is found
        }

        private async Task<CustomerInitial> GetFullOrderDetailsByCustomerAsync(string orderId)
        {
            string orderIdBinary = orderId.ToLower();
            CustomerInitial customerInitial = new CustomerInitial(); // Initialize CustomerInitial object

            try
            {
                // Retrieve the list of suborder IDs associated with the given orderId
                List<string> suborderIds = await GetSuborderIdsByOrderIdAsync(orderIdBinary);

                // Loop through each suborder ID to get details and populate orderItems
                foreach (var suborderId in suborderIds)
                {
                    // Retrieve suborder details, assuming this returns a List<OrderItem>
                    List<OrderItem> suborderDetails = await GetSuborderDetails(suborderId);
                    
                    Debug.WriteLine(suborderDetails);

                    // Loop through each OrderItem in the retrieved suborderDetails
                    foreach (var orderItem in suborderDetails)
                    {
                        // Retrieve add-ons for the current orderItem (OrderAddons)
                        orderItem.orderAddons = await GetOrderAddonsDetails(suborderId); // Populate orderAddons within OrderItem

                        // Add the populated orderItem (including add-ons) to customerInitial orderItems
                        customerInitial.orderItems.Add(orderItem); // Adding OrderItem to orderItems
                    }
                }

                return customerInitial; // Return populated customerInitial with orderItems and customItems
            }
            catch (Exception ex)
            {
                // Log the exception and return null or throw it to be handled by the calling method
                throw new Exception($"An error occurred while fetching order details: {ex.Message}");
            }
        }

        private async Task<CustomerInitial> GetApprovalCustomOrderDetailsByCustomerAsync(string orderId)
        {
            string orderIdBinary = orderId.ToLower();
            CustomerInitial customerInitial = new CustomerInitial(); // Initialize CustomerInitial object

            try
            {
                // Retrieve the list of suborder IDs associated with the given orderId
                List<string> suborderIds = await GetCustomSuborderIdsByOrderIdAsync(orderIdBinary);

                Debug.WriteLine("Retrieved Suborder IDs: " + string.Join(", ", suborderIds));

                foreach (var suborderID in suborderIds)
                {
                    // Check if custom_id is not null
                    bool isCustomIdNull = await IsCustomIdNullAsync(suborderID);
                    if (!isCustomIdNull)
                    {
                        // Fetch custom items if custom_id is present
                        List<CustomItem> customItems = await FetchforapprovalCustomItemsForSuborderAsync(suborderID);

                        // Add each custom item to customerInitial.customItems
                        foreach (var customItem in customItems)
                        {
                            customItem.orderAddons = await GetOrderAddonsDetails(suborderID);
                            customerInitial.customItems.Add(customItem); // Add individual CustomItem
                        }
                    }
                }

                return customerInitial; // Return populated customerInitial with orderItems and customItems
            }
            catch (Exception ex)
            {
                // Log the exception and return null or throw it to be handled by the calling method
                throw new Exception($"An error occurred while fetching order details: {ex.Message}");
            }
        }

        private async Task<CustomerInitial> GetCustomOrderDetailsByCustomerAsync(string orderId)
        {
            string orderIdBinary = orderId.ToLower();
            CustomerInitial customerInitial = new CustomerInitial(); // Initialize CustomerInitial object

            try
            {
                // Retrieve the list of suborder IDs associated with the given orderId
                List<string> suborderIds = await GetCustomSuborderIdsByOrderIdAsync(orderIdBinary);

                foreach (var suborderID in suborderIds)
                {
                    // Check if custom_id is not null
                    bool isCustomIdNull = await IsCustomIdNullAsync(suborderID);
                    if (!isCustomIdNull)
                    {
                        // Fetch custom items if custom_id is present
                        List<CustomItem> customItems = await FetchCustomItemsForSuborderAsync(suborderID);

                        // Add each custom item to customerInitial.customItems
                        foreach (var customItem in customItems)
                        {
                            customItem.orderAddons = await GetOrderAddonsDetails(suborderID);
                            customerInitial.customItems.Add(customItem); // Add individual CustomItem
                        }
                    }
                }

                return customerInitial; // Return populated customerInitial with orderItems and customItems
            }
            catch (Exception ex)
            {
                // Log the exception and return null or throw it to be handled by the calling method
                throw new Exception($"An error occurred while fetching order details: {ex.Message}");
            }
        }


        private async Task<List<string>> GetSuborderIdsByOrderIdAsync(string orderId)
        {
            List<string> suborderIds = new List<string>();

            // Database connection logic
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL query to retrieve suborder IDs for the given order ID
                string sql = "SELECT suborder_id FROM suborders WHERE order_id = @orderId AND custom_id IS NULL";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            suborderIds.Add(reader["suborder_id"].ToString());
                        }
                    }
                }
            }

            return suborderIds;
        }

        private async Task<List<string>> GetCustomSuborderIdsByOrderIdAsync(string orderId)
        {
            List<string> suborderIds = new List<string>();

            // Database connection logic
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // SQL query to retrieve suborder IDs for the given order ID
                string sql = "SELECT suborder_id FROM suborders WHERE order_id = @orderId AND custom_id IS NOT NULL";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            suborderIds.Add(reader["suborder_id"].ToString());
                        }
                    }
                }
            }

            return suborderIds;
        }


        private async Task<bool> IsCustomIdNullAsync(string suborderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT custom_id FROM suborders WHERE suborder_id = @suborder_id";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@suborder_id", suborderId);

                    // Execute the command and retrieve the custom_id
                    object result = await command.ExecuteScalarAsync();

                    // Check if the custom_id is null or not
                    return result == DBNull.Value || result == null;
                }
            }
        }


        [HttpGet("/culo-api/v1/current-user/to-process")]
        [ProducesResponseType(typeof(toPayInitial), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToProcessInitialOrdersByCustomerIds()
        {
            // Fetch customer username from claims
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(customerUsername))
            {
                return Unauthorized("User is not authorized");
            }

            var user = await userManager.FindByNameAsync(customerUsername);

            string customerId = user.Id;
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
        design_id, design_name, price, quantity, 
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
                                string orderId = reader["order_id"].ToString();
                                string suborderId = reader["suborder_id"].ToString();
                                string designId = reader["design_id"].ToString();
                                string customerIdFromDb = reader["customer_id"].ToString();

                                string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                                double ingredientPrice = reader.GetDouble(reader.GetOrdinal("price"));

                                /// Calculate addonPrice using the private async method
                                string orderIdBinary = reader["order_id"].ToString().ToLower();
                                double? addonPrice = await GetAddonPriceAsync(orderIdBinary);

                                // Calculate final price (handle null addonPrice)
                                double finalPrice = ingredientPrice + (addonPrice ?? 0);


                                orders.Add(new toPayInitial
                                {
                                    Id = orderId, // Handle null values for orderId
                                    suborderId = suborderId,
                                    customerId = customerIdFromDb,
                                    createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    pastryId = reader.IsDBNull(reader.GetOrdinal("pastry_id"))
            ? null
            : reader.GetString(reader.GetOrdinal("pastry_id")),
                                    designId = designId,
                                    designName = reader.GetString(reader.GetOrdinal("design_name")),
                                    price = finalPrice,
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = lastUpdatedBy ?? "",
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                    ? (DateTime?)null
                                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    customerName = reader.GetString(reader.GetOrdinal("customer_name"))
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
        [ProducesResponseType(typeof(Full), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToProcessOrdersByCustomerId(string suborderid)
        {
            // Fetch customer username from claims
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(customerUsername))
            {
                return Unauthorized("User is not authorized");
            }

            var user = await userManager.FindByNameAsync(customerUsername);

            string customerId = user.Id;
            if (customerId == null || customerId.Length == 0)
            {
                return BadRequest("Customer not found");
            }

            // Convert suborderId to binary format
            string suborderIdBinary = suborderid.ToLower();

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
                design_id, design_name, price, quantity, 
                last_updated_by, last_updated_at, is_active, description, 
                flavor, size, customer_name, employee_name, shape, color, pastry_id 
            FROM suborders 
            WHERE customer_id = @customerId AND status IN ('assigning artist','baking', 'for approval') AND suborder_id = @suborderIdBinary";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerId", customerId);
                        command.Parameters.AddWithValue("@suborderIdBinary", suborderIdBinary);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string orderId = reader["order_id"].ToString();
                                string suborderIdFromDb = reader["suborder_id"].ToString();
                                string designId = reader["design_id"].ToString();
                                string customerIdFromDb = reader["customer_id"].ToString();
                                string employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id"))? null: reader["employee_id"].ToString();

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
                                    customerId = customerIdFromDb,
                                    employeeId = employeeId,
                                    employeeName = employeeName ?? string.Empty,
                                    createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    status = reader.GetString(reader.GetOrdinal("status")),
                                    pastryId = reader.IsDBNull(reader.GetOrdinal("pastry_id"))
            ? null
            : reader.GetString(reader.GetOrdinal("pastry_id")),
                                    color = reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.GetString(reader.GetOrdinal("shape")),
                                    designId = designId,
                                    designName = reader.IsDBNull(reader.GetOrdinal("design_name"))
             ? null
             : reader.GetString(reader.GetOrdinal("design_name")),

                                    price = reader.GetDouble(reader.GetOrdinal("price")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = lastUpdatedBy ?? string.Empty,
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                    ? (DateTime?)null
                                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    description = reader.IsDBNull(reader.GetOrdinal("description")) ? string.Empty : reader.GetString(reader.GetOrdinal("description")),
                                    flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                    size = reader.GetString(reader.GetOrdinal("size")),
                                    customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                });
                            }
                        }
                    }

                    if (orders.Count > 0)
                    {
                        // Scan the orders table using the retrieved orderId from the suborders table
                        foreach (var order in orders)
                        {
                            if (!string.IsNullOrEmpty(order.orderId))
                            {
                                // Convert orderId to binary format
                                string orderIdBinary = order.orderId.ToLower();
                                Debug.Write(orderIdBinary);

                                var orderDetails = await GetOrderDetailsByOrderId(connection, orderIdBinary);
                                if (orderDetails != null)
                                {
                                    // Populate additional fields from the orders table
                                    order.payment = orderDetails.payment;
                                    order.pickupDateTime = orderDetails.pickupDateTime;
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

        /*[HttpGet("/culo-api/v1/current-user/to-receive")]
        [ProducesResponseType(typeof(toPayInitial), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToReceiveInitialOrdersByCustomerId()
        {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(customerUsername))
            {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            string customerId = await GetUserIdByAllUsername(customerUsername);
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
                    design_id, design_name, price, quantity, 
                    last_updated_by, last_updated_at, is_active, customer_name, pastry_id 
                FROM suborders 
                WHERE customer_id = @customerId AND status = 'for pick up'";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerId", customerId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string orderId = reader["order_id"].ToString();
                                string suborderId = reader["suborder_id"].ToString();
                                string designId = reader["design_id"].ToString();
                                string customerIdFromDb = reader["customer_id"].ToString();

                                string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("last_updated_by"));

                                double ingredientPrice = reader.GetDouble(reader.GetOrdinal("price"));

                                // Calculate addonPrice using the private async method
                                string orderIdBinary = reader["order_id"].ToString().ToLower();
                                double? addonPrice = await GetAddonPriceAsync(orderIdBinary);

                                // Calculate final price (handle null addonPrice)
                                double finalPrice = ingredientPrice + (addonPrice ?? 0);

                                // Add the order details to the list
                                orders.Add(new toPayInitial
                                {
                                    Id = orderId, // Handle null values for orderId
                                    suborderId = suborderId,
                                    customerId = customerIdFromDb,
                                    createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    pastryId = reader.IsDBNull(reader.GetOrdinal("pastry_id"))
            ? null
            : reader.GetString(reader.GetOrdinal("pastry_id")),
                                    designId = designId,
                                    designName = reader.IsDBNull(reader.GetOrdinal("design_name"))
             ? null
             : reader.GetString(reader.GetOrdinal("design_name")),

                                    price = finalPrice,
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = lastUpdatedBy ?? "",
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                    ? (DateTime?)null
                                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    customerName = reader.GetString(reader.GetOrdinal("customer_name"))
                                });
                            }
                        }
                    }

                    if (orders.Count > 0)
                    {
                        // Scan the orders table using the retrieved orderId from the suborders table
                        foreach (var order in orders)
                        {
                            if (!string.IsNullOrEmpty(order.Id))
                                {
                                // Convert orderId to binary format for the query
                                string orderIdBinary = order.Id.ToLower();

                                var orderDetails = await GetOrderDetailsByOrderId(connection, orderIdBinary);
                                if (orderDetails != null)
                                {
                                    // Populate additional fields from the orders table
                                    order.payment = orderDetails.payment;
                                    order.pickupDateTime = orderDetails.pickupDateTime;
                                }
                            }
                        }
                    }

                    // Return the orders list
                    return Ok(orders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving initial orders.");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }*/


        /*[HttpGet("/culo-api/v1/current-user/to-receive/{suborderid}/full")]
        [ProducesResponseType(typeof(Full), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetToReceiveOrdersByCustomerId(string suborderid)
        {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(customerUsername))
            {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            string customerId = await GetUserIdByAllUsername(customerUsername);
            if (customerId == null || customerId.Length == 0)
            {
                return BadRequest("Customer not found");
            }

            // Convert suborderId to binary format
            string suborderIdBinary = suborderid.ToLower();

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
                design_id, design_name, price, quantity, 
                last_updated_by, last_updated_at, is_active, description, 
                flavor, size, customer_name, employee_name, shape, color, pastry_id 
            FROM suborders 
            WHERE customer_id = @customerId AND status IN ('for pick up') AND suborder_id = @suborderIdBinary";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@customerId", customerId);
                        command.Parameters.AddWithValue("@suborderIdBinary", suborderIdBinary);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string orderId = reader["order_id"].ToString();
                                string suborderIdFromDb = reader["suborder_id"].ToString();
                                string designId = reader["design_id"].ToString();
                                string customerIdFromDb = reader["customer_id"].ToString();
                                string employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id")) ? null : reader["employee_id"].ToString();

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
                                    customerId = customerIdFromDb,
                                    employeeId = employeeId,
                                    employeeName = employeeName ?? string.Empty,
                                    createdAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                                    status = reader.GetString(reader.GetOrdinal("status")),
                                    pastryId = reader.IsDBNull(reader.GetOrdinal("pastry_id"))
            ? null
            : reader.GetString(reader.GetOrdinal("pastry_id")),
                                    color = reader.GetString(reader.GetOrdinal("color")),
                                    shape = reader.GetString(reader.GetOrdinal("shape")),
                                    designId = designId,
                                    designName = reader.IsDBNull(reader.GetOrdinal("design_name"))
             ? null
             : reader.GetString(reader.GetOrdinal("design_name")),
                                    price = reader.GetDouble(reader.GetOrdinal("price")),
                                    quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                    lastUpdatedBy = lastUpdatedBy ?? string.Empty,
                                    lastUpdatedAt = reader.IsDBNull(reader.GetOrdinal("last_updated_at"))
                                                    ? (DateTime?)null
                                                    : reader.GetDateTime(reader.GetOrdinal("last_updated_at")),
                                    isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                    description = reader.GetString(reader.GetOrdinal("description")),
                                    flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                    size = reader.GetString(reader.GetOrdinal("size")),
                                    customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                });
                            }
                        }
                    }

                    if (orders.Count > 0)
                    {
                        // Scan the orders table using the retrieved orderId from the suborders table
                        foreach (var order in orders)
                        {
                            if (!string.IsNullOrEmpty(order.orderId))
                            {
                                // Convert orderId to binary format
                                string orderIdBinary = order.orderId.ToLower();
                                Debug.Write(orderIdBinary);

                                var orderDetails = await GetOrderDetailsByOrderId(connection, orderIdBinary);
                                if (orderDetails != null)
                                {
                                    // Populate additional fields from the orders table
                                    order.payment = orderDetails.payment;
                                    order.pickupDateTime = orderDetails.pickupDateTime;
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
        }*/

        private async Task<OrderDetails?> GetOrderDetailsByOrderId(MySqlConnection connection, string orderIdBinary)
        {
            string sql = @"
        SELECT order_id, status, payment, type, pickup_date 
        FROM orders 
        WHERE order_id = @orderIdBinary";

            using (var command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@orderIdBinary", orderIdBinary);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {

                        string orderIdFromDb = reader["order_id"].ToString();

                        return new OrderDetails
                        {
                            orderId = orderIdFromDb,
                            status = reader.GetString(reader.GetOrdinal("status")),
                            payment = reader.IsDBNull(reader.GetOrdinal("payment")) ? (string) null :reader.GetString(reader.GetOrdinal("payment")),
                            type = reader.GetString(reader.GetOrdinal("type")),
                            pickupDateTime = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                            ? (DateTime?)null
                                            : reader.GetDateTime(reader.GetOrdinal("pickup_date"))
                        };
                    }
                }
            }

            return null;
        }

        [HttpGet("/culo-api/v1/current-user/artist/to-do")]
        [ProducesResponseType(typeof(Artist), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Artist + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetOrderxByCustomerId()
        {
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(customerUsername))
            {
                return Unauthorized("User is not authorized");
            }

            // Get the customer's ID using the extracted username
            string customerIdHex = await GetUserIdByUsername(customerUsername);
            if (customerIdHex == null)
            {
                return BadRequest("Customer not found");
            }

            Debug.WriteLine("employee id: " + customerIdHex);

            try
            {
                List<Artist> orders = new List<Artist>();



                // Retrieve order IDs
                List<string> orderIds = await GetOrderIdsAsync();

                // If no order IDs found, return an empty list
                if (orderIds.Count == 0)
                    return Ok(new List<Order>());

                // Update order status if all suborders match for each order ID
                foreach (var orderId in orderIds)
                {
                    await UpdateOrderStatusIfAllSubordersMatchAsync(orderId);
                }

                // Retrieve suborder details for each order ID
                foreach (var orderId in orderIds)
                {
                    var suborderDetails = await GetSuborderDetailsAsync(orderId, customerIdHex);
                    orders.AddRange(suborderDetails);
                }

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        private async Task UpdateOrderStatusIfAllSubordersMatchAsync(string orderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT status 
            FROM suborders 
            WHERE order_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // Use a HashSet to store unique statuses
                        HashSet<string> statuses = new HashSet<string>();

                        while (await reader.ReadAsync())
                        {
                            statuses.Add(reader.GetString("status"));
                        }

                        // Check if all statuses are the same
                        if (statuses.Count == 1)
                        {
                            // Get the common status
                            string commonStatus = statuses.First();

                            // Update the orders table with the common status
                            // Use a new connection for the update
                            using (var updateConnection = new MySqlConnection(connectionstring))
                            {
                                await updateConnection.OpenAsync();

                                string updateSql = @"
                            UPDATE orders 
                            SET status = @commonStatus 
                            WHERE order_id = @orderId";

                                using (var updateCommand = new MySqlCommand(updateSql, updateConnection))
                                {
                                    updateCommand.Parameters.AddWithValue("@commonStatus", commonStatus);
                                    updateCommand.Parameters.AddWithValue("@orderId", orderId);
                                    await updateCommand.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }
                }
            }
        }



        // Private async method to retrieve order IDs
        private async Task<List<string>> GetOrderIdsAsync()
        {
            List<string> orderIds = new List<string>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT order_id 
            FROM orders 
            WHERE pickup_date > NOW() 
            ORDER BY pickup_date ASC";

                using (var command = new MySqlCommand(sql, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            orderIds.Add(reader["order_id"].ToString());
                        }
                    }
                }
            }

            return orderIds;
        }

        // Private async method to retrieve suborder details for a given order ID
        private async Task<List<Artist>> GetSuborderDetailsAsync(string orderId, string customerIdHex)
        {
            List<Artist> suborders = new List<Artist>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT 
                s.suborder_id, s.order_id, s.customer_id, s.employee_id, s.created_at, s.status, 
                s.design_id, s.design_name, s.price, s.quantity, 
                s.last_updated_by, s.last_updated_at, s.is_active, s.description, 
                s.flavor, s.size, s.customer_name, s.employee_name, s.shape, s.color, s.pastry_id,
                o.pickup_date
            FROM suborders s
            JOIN orders o ON s.order_id = o.order_id
            WHERE s.order_id = @orderId 
                AND s.employee_id = @customerId 
                AND s.status IN ('baking', 'for pick up', 'done') 
            ORDER BY 
                CASE 
                    WHEN s.status = 'baking' THEN 0
                    WHEN s.status = 'for pick up' THEN 1
                    WHEN s.status = 'done' THEN 2
                    ELSE 3
                END";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);
                    command.Parameters.AddWithValue("@customerId", customerIdHex);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string orderIdFromDb = reader["order_id"].ToString();
                            string suborderId = reader["suborder_id"].ToString();
                            string designId = reader["design_id"].ToString();
                            string customerIdFromDb = reader["customer_id"].ToString();
                            DateTime pickupDate = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                ? default
                                : reader.GetDateTime(reader.GetOrdinal("pickup_date"));

                            string? employeeName = reader.IsDBNull(reader.GetOrdinal("employee_name"))
                                                    ? null
                                                    : reader.GetString(reader.GetOrdinal("employee_name"));

                            string? lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by"))
                                                     ? null
                                                     : reader.GetString(reader.GetOrdinal("last_updated_by"));

                            suborders.Add(new Artist
                            {
                                orderId = orderIdFromDb,
                                suborderId = suborderId,
                                customerId = customerIdFromDb,
                                status = reader.GetString(reader.GetOrdinal("status")),
                                color = reader.GetString(reader.GetOrdinal("color")),
                                shape = reader.GetString(reader.GetOrdinal("shape")),
                                designId = designId,
                                designName = reader.IsDBNull(reader.GetOrdinal("design_name"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("design_name")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                isActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                                description = reader.IsDBNull(reader.GetOrdinal("description"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("description")),
                                flavor = reader.GetString(reader.GetOrdinal("flavor")),
                                size = reader.GetString(reader.GetOrdinal("size")),
                                customerName = reader.GetString(reader.GetOrdinal("customer_name")),
                                pickupDate = pickupDate // Assuming Artist has a pickupDate property
                            });
                        }
                    }
                }
            }

            return suborders;
        }



        private async Task<string> GetUserIdByUsername(string username)
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


        [HttpGet("{orderId}/final-details")]
        [ProducesResponseType(typeof(CheckOutDetails), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Customer + "," + UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetFinalOrderDetailsByOrderId(string orderId)
        {
            // Fetch customer username from claims
            var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(customerUsername))
            {
                return Unauthorized("User is not authorized");
            }

            var user = await userManager.FindByNameAsync(customerUsername);

            string customerId = user.Id;
            if (customerId == null || customerId.Length == 0)
            {
                return BadRequest("Customer not found");
            }

            string orderIdBinary =  orderId.ToLower();

            try
            {
                // Retrieve basic order details
                var orderDetails = await GetOrderDetailx(orderIdBinary);

                if (orderDetails != null)
                {
                    // Retrieve suborders
                    orderDetails.orderItems = await GetSuborderDetails(orderIdBinary);

                    // Initialize total sum
                    double totalSum = 0;

                    foreach (var suborder in orderDetails.orderItems)
                    {
                        // Retrieve add-ons for each suborder
                        suborder.orderAddons = await GetOrderAddonsDetails(suborder.suborderId);

                        // Calculate the total for this suborder
                        double addOnsTotal = suborder.orderAddons.Sum(addon => addon.addOnTotal);
                        suborder.subOrderTotal = (suborder.price ?? 0) * suborder.quantity + addOnsTotal;


                        // Add to the overall total
                        totalSum += suborder.subOrderTotal;

                    }

                    // Set the total in CheckOutDetails
                    orderDetails.orderTotal = totalSum;
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
    WHERE order_id = @orderIdBinary";

                using (var command = new MySqlCommand(orderSql, connection))
                {
                    command.Parameters.AddWithValue("@orderIdBinary", orderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string orderIdFromDb = reader["order_id"].ToString();

                            return new CheckOutDetails
                            {
                                orderId = orderIdFromDb,
                                status = reader.GetString(reader.GetOrdinal("status")),
                                paymentMethod = reader.GetString(reader.GetOrdinal("payment")),
                                orderType = reader.GetString(reader.GetOrdinal("type")),
                                pickupDateTime = reader.IsDBNull(reader.GetOrdinal("pickup_date"))
                                    ? (DateTime?)null
                                    : reader.GetDateTime(reader.GetOrdinal("pickup_date"))
                            };
                        }
                    }
                }
            }
            return null;
        }

        private async Task<List<CustomItem>> GetSuborderDetail(string orderIdBinary)
        {
            var suborders = new List<CustomItem>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string suborderSql = @"
    SELECT 
        suborder_id, order_id, customer_id, employee_id, created_at, status, 
        design_id, design_name, price, quantity, 
        last_updated_by, last_updated_at, is_active, description, 
        flavor, size, customer_name, employee_name, shape, color, pastry_id 
    FROM suborders 
    WHERE order_id = @orderIdBinary AND custom_id IS NOT NULL";

                using (var command = new MySqlCommand(suborderSql, connection))
                {
                    command.Parameters.AddWithValue("@orderIdBinary", orderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {

                            string? orderIdFromDb = reader.IsDBNull(reader.GetOrdinal("order_id")) ? null : reader["order_id"].ToString();
                            string? suborderId = reader.IsDBNull(reader.GetOrdinal("suborder_id")) ? null : reader["suborder_id"].ToString();
                            string? designId = reader.IsDBNull(reader.GetOrdinal("design_id")) ? null : reader["design_id"].ToString();
                            string? customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id")) ? null : reader["customer_id"].ToString();
                            string? employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id")) ? null : reader["employee_id"].ToString();

                            var suborder = new CustomItem
                            {
                                suborderId = suborderId,
                                orderId = orderIdFromDb,
                                color = reader.IsDBNull(reader.GetOrdinal("color")) ? null : reader.GetString(reader.GetOrdinal("color")),
                                shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? null : reader.GetString(reader.GetOrdinal("shape")),
                                designId = designId,
                                designName = reader.IsDBNull(reader.GetOrdinal("design_name"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("design_name")),
                                status = reader.IsDBNull(reader.GetOrdinal("status"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("status")),
                                price = reader.IsDBNull(reader.GetOrdinal("price")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("price")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                                flavor = reader.IsDBNull(reader.GetOrdinal("flavor")) ? null : reader.GetString(reader.GetOrdinal("flavor")),
                                size = reader.IsDBNull(reader.GetOrdinal("size")) ? null : reader.GetString(reader.GetOrdinal("size")),
                                subOrderTotal = (reader.IsDBNull(reader.GetOrdinal("price")) ? 0 : reader.GetDouble(reader.GetOrdinal("price"))) *
                                                (reader.IsDBNull(reader.GetOrdinal("quantity")) ? 0 : reader.GetInt32(reader.GetOrdinal("quantity"))) // Calculate Total
                            };

                            var (tier, cover) = await FetchTierAndCoverBySuborderIdAsync(suborder.suborderId);
                            suborder.tier = tier;
                            suborder.cover = cover;

                            if (!string.IsNullOrEmpty(suborder.designId))
                            {
                                byte[]? pictureData = await GetPictureDataByDesignId(suborder.designId);
                                if (pictureData != null)
                                {
                                    // Convert the picture data to a base64 string
                                    suborder.pictureDate = Convert.FromBase64String(Convert.ToBase64String(pictureData));
                                }
                            }

                            suborders.Add(suborder);
                        }
                    }
                }
            }

            return suborders;
        }

        private async Task<List<OrderItem>> GetSubordersDetails(string orderIdBinary)
        {
            var suborders = new List<OrderItem>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string suborderSql = @"
    SELECT 
        suborder_id, order_id, customer_id, employee_id, created_at, status, 
        design_id, design_name, price, quantity, 
        last_updated_by, last_updated_at, is_active, description, 
        flavor, size, customer_name, employee_name, shape, color, pastry_id 
    FROM suborders 
    WHERE order_id = @orderIdBinary AND custom_id IS NULL";

                using (var command = new MySqlCommand(suborderSql, connection))
                {
                    command.Parameters.AddWithValue("@orderIdBinary", orderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {

                            string? orderIdFromDb = reader.IsDBNull(reader.GetOrdinal("order_id")) ? null : reader["order_id"].ToString();
                            string? suborderId = reader.IsDBNull(reader.GetOrdinal("suborder_id")) ? null : reader["suborder_id"].ToString();
                            string? designId = reader.IsDBNull(reader.GetOrdinal("design_id")) ? null : reader["design_id"].ToString();
                            string? customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id")) ? null : reader["customer_id"].ToString();
                            string? employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id")) ? null : reader["employee_id"].ToString();

                            var suborder = new OrderItem
                            {
                                suborderId = suborderId,
                                orderId = orderIdFromDb,
                                pastryId = reader.IsDBNull(reader.GetOrdinal("pastry_id"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("pastry_id")),
                                color = reader.IsDBNull(reader.GetOrdinal("color")) ? null : reader.GetString(reader.GetOrdinal("color")),
                                shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? null : reader.GetString(reader.GetOrdinal("shape")),
                                designId = designId,
                                designName = reader.IsDBNull(reader.GetOrdinal("design_name"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("design_name")),
                                status = reader.IsDBNull(reader.GetOrdinal("status"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("status")),
                                price = reader.IsDBNull(reader.GetOrdinal("price")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("price")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                                flavor = reader.IsDBNull(reader.GetOrdinal("flavor")) ? null : reader.GetString(reader.GetOrdinal("flavor")),
                                size = reader.IsDBNull(reader.GetOrdinal("size")) ? null : reader.GetString(reader.GetOrdinal("size")),
                                subOrderTotal = (reader.IsDBNull(reader.GetOrdinal("price")) ? 0 : reader.GetDouble(reader.GetOrdinal("price"))) *
                                                (reader.IsDBNull(reader.GetOrdinal("quantity")) ? 0 : reader.GetInt32(reader.GetOrdinal("quantity"))) // Calculate Total
                            };


                            suborders.Add(suborder);
                        }
                    }
                }
            }

            return suborders;
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
        design_id, design_name, price, quantity, 
        last_updated_by, last_updated_at, is_active, description, 
        flavor, size, customer_name, employee_name, shape, color, pastry_id 
    FROM suborders 
    WHERE suborder_id = @orderIdBinary";

                using (var command = new MySqlCommand(suborderSql, connection))
                {
                    command.Parameters.AddWithValue("@orderIdBinary", orderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {

                            string? orderIdFromDb = reader.IsDBNull(reader.GetOrdinal("order_id")) ? null : reader["order_id"].ToString();
                            string? suborderId = reader.IsDBNull(reader.GetOrdinal("suborder_id")) ? null : reader["suborder_id"].ToString();
                            string? designId = reader.IsDBNull(reader.GetOrdinal("design_id")) ? null : reader["design_id"].ToString();
                            string? customerIdFromDb = reader.IsDBNull(reader.GetOrdinal("customer_id")) ? null : reader["customer_id"].ToString();
                            string? employeeId = reader.IsDBNull(reader.GetOrdinal("employee_id")) ? null : reader["employee_id"].ToString();

                            var suborder = new OrderItem
                            {
                                suborderId = suborderId,
                                orderId = orderIdFromDb,
                                pastryId = reader.IsDBNull(reader.GetOrdinal("pastry_id"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("pastry_id")),
                                color = reader.IsDBNull(reader.GetOrdinal("color")) ? null : reader.GetString(reader.GetOrdinal("color")),
                                shape = reader.IsDBNull(reader.GetOrdinal("shape")) ? null : reader.GetString(reader.GetOrdinal("shape")),
                                designId = designId,
                                designName = reader.IsDBNull(reader.GetOrdinal("design_name"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("design_name")),
                                status = reader.IsDBNull(reader.GetOrdinal("status"))
                                    ? null
                                    : reader.GetString(reader.GetOrdinal("status")),
                                price = reader.IsDBNull(reader.GetOrdinal("price")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("price")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
                                flavor = reader.IsDBNull(reader.GetOrdinal("flavor")) ? null : reader.GetString(reader.GetOrdinal("flavor")),
                                size = reader.IsDBNull(reader.GetOrdinal("size")) ? null : reader.GetString(reader.GetOrdinal("size")),
                                subOrderTotal = (reader.IsDBNull(reader.GetOrdinal("price")) ? 0 : reader.GetDouble(reader.GetOrdinal("price"))) *
                                                (reader.IsDBNull(reader.GetOrdinal("quantity")) ? 0 : reader.GetInt32(reader.GetOrdinal("quantity"))) // Calculate Total
                            };


                            suborders.Add(suborder);
                        }
                    }
                }
            }

            return suborders;
        }

        private async Task<List<OrderAddon1>> GetOrderAddonsDetails(string suborderId)
        {
            var addons = new List<OrderAddon1>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string addOnsSql = @"
            SELECT add_ons_id, quantity, total, name, price
            FROM orderaddons
            WHERE order_id = @suborderIdBinary";

                using (var command = new MySqlCommand(addOnsSql, connection))
                {
                    command.Parameters.AddWithValue("@suborderIdBinary", suborderId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            addons.Add(new OrderAddon1
                            {
                                id = reader.GetInt32(reader.GetOrdinal("add_ons_id")),
                                quantity = reader.GetInt32(reader.GetOrdinal("quantity")),
                                addOnTotal = reader.GetDouble(reader.GetOrdinal("total")),
                                name = reader.GetString(reader.GetOrdinal("name")),
                                price = reader.IsDBNull(reader.GetOrdinal("price")) ? (double?)null : reader.GetDouble(reader.GetOrdinal("price"))
                            });
                        }
                    }
                }
            }

            return addons;
        }

        [HttpGet("employees-name")] //done
        [ProducesResponseType(typeof(employee), StatusCodes.Status200OK)]
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
        [ProducesResponseType(typeof(TotalOrders), StatusCodes.Status200OK)]
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
                            totalQuantities.total = Convert.ToInt32(result);
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
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetTotalQuantityForDay([FromQuery] int year, [FromQuery] int month, [FromQuery] int day)
        {
            try
            {
                // Create a DateTime object from the provided query parameters
                DateTime specificDay = new DateTime(year, month, day);

                // Call the method to get total quantity for the specific day
                int total = await GetTotalQuantityForSpecificDay(specificDay);

                // Create an instance of OrderResponse
                var response = new OrderResponse
                {
                    day = specificDay.ToString("dddd"), // Get the full name of the day (e.g., Monday)
                    totalOrders = total // Set the total orders quantity
                };

                // If total is 0, set TotalSales to 0
                if (total == 0)
                {
                    response.totalOrders = 0; // Ensure TotalSales is 0
                }

                // Return the response
                return Ok(response);
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
        [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
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
                    return Ok(new List<OrderResponse>()); // Return an empty array
                }

                // Create a list of OrderResponse instances for the week
                var response = weekQuantities.Select(q => new OrderResponse
                {
                    day = q.Key, // Use the day name directly as q.Key is a string
                    totalOrders = q.Value // Set the total orders quantity
                }).ToList();

                // Return the result as a list of OrderResponse
                return Ok(response);
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
        [ProducesResponseType(typeof(MonthOrdersResponse), StatusCodes.Status200OK)]
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
                    return Ok(new List<MonthOrdersResponse>()); // Return an empty array
                }

                // Return result in the desired format using MonthOrdersResponse
                var response = dailyQuantities.Select(q => new MonthOrdersResponse
                {
                    day = q.Key, // Day number
                    totalOrders = q.Value // Total orders for that day
                }).ToList();

                return Ok(response);
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
        [ProducesResponseType(typeof(YearOrdersResponse), StatusCodes.Status200OK)]
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
                    return Ok(new List<YearOrdersResponse>()); // Return an empty array
                }

                // Create a list of YearOrdersResponse instances for the year
                var response = yearlyQuantities.Select(q => new YearOrdersResponse
                {
                    month = q.Key, // Month name
                    totalOrders = q.Value // Total orders for that month
                }).ToList();

                return Ok(response);
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

        [HttpPost("current-user/{orderId}/order-received")]
        public async Task<IActionResult> MarkOrderAsReceived(string orderId)
        {
            try
            {
                await UpdateOrderStatusAsync(orderId, "done", false);
                // Retrieve the list of suborder IDs for the given order ID
                List<string> suborderIds = await GetSuborderIdByOrderIdAsync(orderId);

                if (suborderIds == null || !suborderIds.Any())
                {
                    return NotFound($"No suborders found for the given order ID: {orderId}");
                }

                // Loop through each suborder and update its status to "done" and set is_active to false
                foreach (var suborderId in suborderIds)
                {
                    await UpdateDoneStatus(suborderId, "done", false);
                    // Process the order completion by inserting relevant details into the sales table
                    await ProcessOrderCompletion(suborderId);
                }
                _logger.LogInformation($"Order '{orderId}' marked as received and processed successfully.");
                return Ok($"Order '{orderId}' has been marked as received and processed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while marking order '{orderId}' as received.");
                return StatusCode(500, "An error occurred while processing the order. Please try again.");
            }
        }

        private async Task<List<string>> GetSuborderIdByOrderIdAsync(string orderId)
        {
            var suborderIds = new List<string>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Query to select suborder_id based on order_id
                string sql = "SELECT suborder_id FROM suborders WHERE order_id = @orderId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@orderId", orderId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Retrieve suborder_id and add it to the list
                            suborderIds.Add(reader.GetString("suborder_id"));
                        }
                    }
                }
            }

            return suborderIds;
        }


        private async Task UpdateDoneStatus(string orderIdBinary, string status,bool isActive)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    // Update the status of the suborders
                    string subOrderSql = "UPDATE suborders SET status = @subOrderStatus, is_active = @isActive, last_updated_at = NOW() WHERE suborder_id = @orderId";
                    using (var subOrderCommand = new MySqlCommand(subOrderSql, connection))
                    {
                        subOrderCommand.Parameters.AddWithValue("@subOrderStatus", status);
                        subOrderCommand.Parameters.AddWithValue("@orderId", orderIdBinary);
                        subOrderCommand.Parameters.AddWithValue("@isActive", isActive);

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

        private async Task UpdateOrderStatusAsync(string orderId, string status, bool isActive)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    // SQL command to update the status and is_active fields in the orders table
                    string sqlUpdateOrder = @"
                UPDATE orders 
                SET status = @subOrderStatus, 
                    is_active = @isActive, 
                    last_updated_at = NOW() 
                WHERE order_id = @orderId";

                    using (var command = new MySqlCommand(sqlUpdateOrder, connection))
                    {
                        command.Parameters.AddWithValue("@subOrderStatus", status);
                        command.Parameters.AddWithValue("@isActive", isActive);
                        command.Parameters.AddWithValue("@orderId", orderId);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while updating order status for order ID '{orderId}'.");
                throw;
            }
        }


        [HttpPatch("suborders/{suborderId}/update-status")] //done
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Artist + "," + UserRoles.Customer)]
        public async Task<IActionResult> PatchOrderStatus(string suborderId, [FromQuery] string action)
        {
            try
            {
                string orderIdBinary = suborderId.ToLower();

                Debug.WriteLine(orderIdBinary);

                Debug.WriteLine("suborder id: " + orderIdBinary);

                // Update the order status based on the action
                if (action.Equals("send", StringComparison.OrdinalIgnoreCase))
                {
                    await UpdateOrderStatus(orderIdBinary, true); // Set isActive to true
                    await UpdateStatus(orderIdBinary, "for pick up");
                    await UpdateLastUpdatedAt(orderIdBinary);
                    string orderId = await RetrieveOrderIdAsGuidFromSuborders(orderIdBinary);

                    Debug.Write(orderId);
                    await DataManipulation.SubtractPastryMaterialIngredientsByOrderId(orderId, _context, _kaizenTables);
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
                    string userId = customerId.ToLower();

                    Guid notId = Guid.NewGuid();

                    string notifId = notId.ToString().ToLower();

                    // Check the value of 'action' and send the corresponding notification
                    if (action.Equals("send", StringComparison.OrdinalIgnoreCase))
                    {

                        // Construct the message for 'send' action
                        string message = ("your order is ready for pick up");

                        // Send the notification
                        await NotifyAsync(notifId, userId, message);
                    }
                    else if (action.Equals("done", StringComparison.OrdinalIgnoreCase))
                    {
                        // Construct the message for 'done' action
                        string message = ("order received");

                        // Send the notification
                        await NotifyAsync(notifId, userId, message);
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

        private async Task<string> RetrieveOrderIdAsGuidFromSuborders(string subOrderIdBinary)
        {
            string orderId = null;

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sqlSubOrder = "SELECT order_id FROM suborders WHERE suborder_id = @subOrderId";
                using (var command = new MySqlCommand(sqlSubOrder, connection))
                {
                    command.Parameters.AddWithValue("@subOrderId", subOrderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            orderId = reader["order_id"].ToString();
                        }
                        else
                        {
                            throw new ArgumentException("No suborder found with the provided ID", nameof(subOrderIdBinary));
                        }
                    }
                }
            }

            return orderId;  // Return the GUID string of the order_id
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
                                    JOIN users u ON UNHEX(o.employee_id) = u.UserId
                                    WHERE o.suborder_id = @orderId";
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
                    string subOrderSql = "UPDATE suborders SET status = @subOrderStatus WHERE suborder_id = @orderId";
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

            foreach (var addOn in manageAddOns)
            {
                if (addOn.quantity < 1 || addOn.quantity > 3)
                {
                    return BadRequest($"Quantity must be between 1 and 3.");
                }
            }

            // Convert suborderId to binary format
            string suborderIdBinary = suborderId.ToLower();

            // Loop through each AddOn in the manage list
            foreach (var manage in manageAddOns)
            {
                // Log the process for each add-on
                _logger.LogInformation($"Managing AddOnId: {manage.id} for SubOrderId: {suborderId}");

                // Fetch the add-on price and name
                double addonPrice = await GetAddonPriceAsync(manage.id);
                string name = await AddonName(manage.id);

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();
                    try
                    {
                        // Calculate total price
                        double total = manage.quantity * addonPrice * 2;

                        // Insert or update the order add-ons for the current add-on
                        await InsertOrUpdateOrderaddonWithSubOrderId(suborderIdBinary, manage.id, addonPrice, manage.quantity, name, total);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Transaction failed for AddOnId: {manage.id}, rolling back");
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
                            WHERE order_id = @orderId AND add_ons_id = @addOnId";

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
                                     WHERE order_id = @orderId AND add_ons_id = @addOnId";

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
                                     VALUES (@orderId, @addOnId, @quantity, @total, @name, @price)";

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

        private async Task ManageAddOnsByPastryMaterialId(string pastryMaterialId, string suborderIdBinary, int? id, int? quantity)
        {
            // Log the start of the process
            _logger.LogInformation($"Starting ManageAddOnsByPastryMaterialId for pastryMaterialId: {pastryMaterialId}, suborderId: {suborderIdBinary}");

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // If id and quantity are null or 0, insert default add-ons
                        if (id == null || quantity == null || id == 0 || quantity == 0)
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
                                var addOnDetails = await GetAddOnDetails(connection, transaction, addOn.id);
                                addOnDetailsDict[addOn.id] = addOnDetails;
                            }

                            // Insert the default add-ons without modifying quantity or removing
                            foreach (var addOn in allAddOns)
                            {
                                // Fetch add-on details
                                var addOnDetails = addOnDetailsDict[addOn.id];

                                // Calculate total price based on the default quantity
                                double total = addOn.quantity * addOnDetails.Price * 2;

                                // Insert the default add-on to the orderaddons table
                                await InsertAddOn(connection, transaction, suborderIdBinary, addOn.id, addOn.quantity, total, addOnDetailsDict);
                            }

                            // Commit the transaction
                            await transaction.CommitAsync();
                        }
                        else
                        {
                            // If both id and quantity are provided, handle them individually
                            var addOnDetails = await GetAddOnDetails(connection, transaction, id.Value);

                            // Calculate total price based on provided quantity
                            double total = quantity.Value * addOnDetails.Price;

                            // Insert the provided add-on
                            await InsertAddOn(connection, transaction, suborderIdBinary, id.Value, quantity.Value, total, new Dictionary<int, (string Name, double Price)> { { id.Value, addOnDetails } });

                            // Commit the transaction
                            await transaction.CommitAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error and rollback the transaction
                        _logger.LogError(ex, "Transaction failed, rolling back");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }

            _logger.LogInformation("Add-ons successfully managed.");
        }



        private async Task<string> GetOrderSize(MySqlConnection connection, MySqlTransaction transaction, string orderId)
        {
            string sql = @"SELECT size
                   FROM suborders
                   WHERE suborder_id = @orderId";

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
                            id = reader.GetInt32("AddOnId"),
                            quantity = reader.GetInt32("DefaultQuantity")
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
                            id = reader.GetInt32("AddOnId"),
                            quantity = reader.GetInt32("DefaultQuantity")
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

        private async Task InsertAddOn(MySqlConnection connection, MySqlTransaction transaction, string orderIdBinary, int addOnId, int quantity, double total, Dictionary<int, (string Name, double Price)> addOnDetailsDict)
        {
            // Retrieve add-on details from the dictionary
            if (!addOnDetailsDict.TryGetValue(addOnId, out var addOnDetails))
            {
                throw new Exception($"Add-on details not found for AddOnId '{addOnId}'.");
            }

            if (quantity > 0)
            {
                // Insert new add-on into orderaddons
                string insertSql = @"INSERT INTO orderaddons (order_id, add_ons_id, quantity, total, name, price)
                             VALUES (@orderId, @addOnId, @quantity, @total, @name, @price)";
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
                _logger.LogInformation($"Skipping add-on ID '{addOnId}' because quantity is 0.");
            }
        }


        [HttpPatch("/culo-api/v1/current-user/{suborderId}/suborders")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> UpdateOrderDetails(string suborderId, [FromBody] UpdateOrderDetailsRequest? request)
        {
            try
            {
                _logger.LogInformation($"Starting UpdateOrderDetails for orderId: {suborderId}");

                // Convert orderId to binary(16) format without '0x' prefix
                string suborderIdBinary = suborderId.ToLower();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    using (var transaction = await connection.BeginTransactionAsync())
                    {
                        try
                        {
                            // Update order details in the orders table if request has data
                            if (request != null)
                            {
                                await UpdateOrderDetailsInDatabase(connection, transaction, suborderIdBinary, request);
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
            var updateParts = new List<string>();
            var parameters = new List<MySqlParameter>();

            // Dynamically build the update SQL statement based on which fields are provided
            if (request.description != null)
            {
                updateParts.Add("description = @description");
                parameters.Add(new MySqlParameter("@description", request.description));
            }
            if (request.quantity.HasValue)
            {
                updateParts.Add("quantity = @quantity");
                parameters.Add(new MySqlParameter("@quantity", request.quantity));
            }
            if (request.size != null)
            {
                updateParts.Add("size = @size");
                parameters.Add(new MySqlParameter("@size", request.size));
            }
            if (request.flavor != null)
            {
                updateParts.Add("flavor = @flavor");
                parameters.Add(new MySqlParameter("@flavor", request.flavor));
            }
            if (request.color != null)
            {
                updateParts.Add("color = @color");
                parameters.Add(new MySqlParameter("@color", request.color));
            }
            if (request.shape != null)
            {
                updateParts.Add("shape = @shape");
                parameters.Add(new MySqlParameter("@shape", request.shape));
            }

            if (updateParts.Any())
            {
                // Only update if there are fields to update
                string updateSql = $"UPDATE suborders SET {string.Join(", ", updateParts)} WHERE suborder_id = @suborderId";
                parameters.Add(new MySqlParameter("@suborderId", orderIdBinary));

                using (var command = new MySqlCommand(updateSql, connection, transaction))
                {
                    command.Parameters.AddRange(parameters.ToArray());

                    await command.ExecuteNonQueryAsync();
                    _logger.LogInformation($"Updated order details in orders table for order with ID '{orderIdBinary}'");
                }
            }
        }


        [HttpPatch("custom-orders/{suborderId}/set-price")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin + "," + UserRoles.Customer)]
        public async Task<IActionResult> PatchCustomOrder(string suborderId, [FromBody] CustomOrderUpdateRequest customReq)
        {
            if (customReq == null || string.IsNullOrWhiteSpace(suborderId))
            {
                return BadRequest("Invalid request data.");
            }

            string suborderIdBinary = suborderId.ToLower();

            string sql = @"
            UPDATE suborders 
            SET design_name = @designName, 
                price = @price 
            WHERE suborder_id = @customId";

            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    using (MySqlCommand cmd = new MySqlCommand(sql, connection))
                    {
                        cmd.Parameters.AddWithValue("@designName", customReq.designName);
                        cmd.Parameters.AddWithValue("@price", customReq.price);
                        cmd.Parameters.AddWithValue("@customId", suborderIdBinary);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        var (customerId, customerName) = await GetCustomerInfoForCustomOrders(suborderIdBinary);

                        if (!string.IsNullOrEmpty(customerId))
                        {

                            Guid notId = Guid.NewGuid();

                            string notifId = notId.ToString().ToLower();

                            // Convert the customerId to the binary format needed
                            string userId = customerId.ToLower();

                            // Construct the message
                            string message = (" your order has been approved; view final details");

                            // Send the notification
                            await NotifyAsync(notifId, userId, message);
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

        private async Task<string> GetOrderIdBySuborderIdAsync(string suborderId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Prepare the SQL query to retrieve order_id using suborder_id
                string sqlQuery = "SELECT order_id FROM suborders WHERE suborder_id = @suborderId";

                using (var command = new MySqlCommand(sqlQuery, connection))
                {
                    command.Parameters.AddWithValue("@suborderId", suborderId);

                    // Execute the query and get the result
                    object result = await command.ExecuteScalarAsync();

                    // Check if a value was found
                    if (result != null && result != DBNull.Value)
                    {
                        return result.ToString();
                    }
                    else
                    {
                        // If no value was found, return null
                        return null;
                    }
                }
            }
        }


        private async Task<string> UpdateOrderStatusToPayAsync(string orderIdBinary)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Prepare the SQL update query
                string sqlUpdate = "UPDATE orders SET status = 'to pay' WHERE order_id = @orderId";

                // Execute the update command
                using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                {
                    updateCommand.Parameters.AddWithValue("@orderId", orderIdBinary);

                    int rowsAffected = await updateCommand.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        // If no rows were affected, it means the order was not found
                        return "Order not found";
                    }
                    else
                    {
                        // Return success if the update was successful
                        return "Order status updated successfully";
                    }
                }
            }
        }


        private async Task<(string customerId, string customerName)> GetCustomerInfoForCustomOrders(string order)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string designQuery = "SELECT customer_name, customer_id FROM suborders WHERE suborder_id = @orderId";
                using (var designcommand = new MySqlCommand(designQuery, connection))
                {
                    designcommand.Parameters.AddWithValue("@orderId", order);
                    using (var reader = await designcommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string customerId = reader.GetString("customer_id");
                            string customerName = reader.GetString("customer_name");
                            return (customerId, customerName);
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
                string orderIdBinary = orderId.ToLower();

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
            WHERE order_id = @orderId 
            AND customer_id = (SELECT Id FROM aspnetusers WHERE Username = @customerUsername)";

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

                string sql = "DELETE FROM orders WHERE order_id = @orderId";

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
                string suborderIdBinary = suborderId.ToLower();

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
            WHERE suborder_id = @orderId 
            AND customer_id = (SELECT Id FROM aspnetusers WHERE Username = @customerUsername)";

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

                string sql = "DELETE FROM suborders WHERE suborder_id = @orderId AND status = 'cart'";

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
        WHERE status = 'cart' AND customer_id = (SELECT Id FROM aspnetusers WHERE Username = @customerUsername)";

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
                    command.CommandText = "SELECT COUNT(*) FROM suborders WHERE suborder_id = @orderId";
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
                string sqlSuborders = "UPDATE suborders SET employee_id = @employeeId, employee_name = @employeeName, status = 'baking' WHERE suborder_id = @orderId";

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
                string sqlSubOrder = "SELECT order_id FROM suborders WHERE suborder_id = @subOrderId";
                using (var command = new MySqlCommand(sqlSubOrder, connection))
                {
                    command.Parameters.AddWithValue("@subOrderId", subOrderIdBinary);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Convert the order_id directly as it is assumed to be a valid Guid in binary format
                            string orderId = reader["order_id"].ToString();

                            // Convert the retrieved order_id to binary format
                            orderIdFi = orderId.ToLower();
                        }
                        else
                        {
                            throw new ArgumentException("No suborder found with the provided ID", nameof(subOrderIdBinary));
                        }
                    }
                }

                // Step 2: Check if all suborders with the retrieved order_id have status "baking"
                if (!string.IsNullOrEmpty(orderIdFi))
                {
                    string sqlCheckStatus = "SELECT COUNT(*) FROM suborders WHERE order_id = @orderId AND status <> 'baking'";
                    using (var command = new MySqlCommand(sqlCheckStatus, connection))
                    {
                        command.Parameters.AddWithValue("@orderId", orderIdFi);

                        int nonBakingCount = Convert.ToInt32(await command.ExecuteScalarAsync());

                        // If nonBakingCount is 0, it means all suborders are in 'baking' status
                        if (nonBakingCount > 0)
                        {
                            // Skip updating if any suborder is not in 'baking' status
                            return; // Or you can throw an exception or log as needed
                        }
                    }

                    // Step 3: Update the status in the orders table
                    string sqlOrders = "UPDATE orders SET status = 'baking' WHERE order_id = @orderId";
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

                string sql = "UPDATE suborders SET is_active = @isActive WHERE suborder_id = @orderId";

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

                string sql = "UPDATE suborders SET last_updated_at = NOW() WHERE suborder_id = @orderId";

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

                string designQuery = "SELECT display_name FROM designs WHERE design_id = @displayName";
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

        private async Task<(string customerId, string customerName)> GetCustomerInfoBySubOrderId(string order)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string designQuery = "SELECT customer_name, customer_id FROM suborders WHERE suborder_id = @orderId";
                using (var designcommand = new MySqlCommand(designQuery, connection))
                {
                    designcommand.Parameters.AddWithValue("@orderId", order);
                    using (var reader = await designcommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            // Retrieve customer_id as byte[]
                            string customerId = reader.GetString("customer_id");
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

        private async Task<List<string>> GetEmployeeAllId()
        {
            var empIds = new List<string>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();  // Open the connection only once

                string sql = "SELECT Id FROM aspnetroles WHERE Name IN (Admin, Manager)";

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
