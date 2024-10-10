using BOM_API_v2.Services;
using CRUDFI.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace BOM_API_v2.KaizenFiles.Controllers
{
    [Route("ingredients")]
    [ApiController]
    [Authorize]
    public class IngredientsController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<IngredientsController> _logger;
        private readonly IActionLogger dbLogger;

        public IngredientsController(IConfiguration configuration, ILogger<IngredientsController> logger, IActionLogger dbLogger)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> CreateIngredient([FromBody] IngriDTO ingredientDto)
        {
            try
            {
                // Validate the model state
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState); // Return validation errors
                }

                // Extract the username from the token
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("User is not authorized");
                }

                // Fetch the user ID of the user performing the update
                string lastUpdatedBy = username;

                if (lastUpdatedBy == null)
                {
                    return Unauthorized("User ID not found");
                }

                // Determine the status based on quantity and thresholds
                string status;
                if (ingredientDto.quantity <= Convert.ToInt32(ingredientDto.bad))
                {
                    status = "critical";
                }
                else if (ingredientDto.quantity > Convert.ToInt32(ingredientDto.bad) && ingredientDto.quantity < Convert.ToInt32(ingredientDto.good))
                {
                    status = "mid";
                }
                else if (ingredientDto.quantity >= Convert.ToInt32(ingredientDto.good))
                {
                    status = "good";
                }
                else
                {
                    status = "normal"; // Default status if none of the conditions are met
                }

                Guid Id = Guid.NewGuid();
                string id = Id.ToString().ToLower();
                Guid itemId = Guid.NewGuid();
                string itemid = itemId.ToString().ToLower();

                // Call the refactored method to handle the database logic
                await InsertOrUpdateIngredientAsync(ingredientDto, lastUpdatedBy, id, itemid);
                await InsertIngredientAsync(ingredientDto, lastUpdatedBy, status, itemid);
                await InsertOrUpdateThresholdConfigAsync(ingredientDto.name, Convert.ToInt32(ingredientDto.good), Convert.ToInt32(ingredientDto.bad), itemid);

                return Ok("Ingredient and threshold configuration added successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the request");
                ModelState.AddModelError("ingredient", "Sorry, but we encountered an exception while processing your request");
                return BadRequest(ModelState);
            }
        }

        private async Task InsertIngredientAsync(IngriDTO ingredientDto, string lastUpdatedBy, string status, string id)
        {
            string sqlInsert = "INSERT INTO item(Id, item_name, quantity, price, status, type, created_at, last_updated_by, last_updated_at, measurements) " +
                               "VALUES(@id, @item_name, @quantity, @price, @status, @type, @createdAt, @last_updated_by, @last_updated_at, @measurements)";

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync(); // Open the connection asynchronously

                using (var insertCommand = new MySqlCommand(sqlInsert, connection))
                {
                    insertCommand.Parameters.AddWithValue("@id", id);
                    insertCommand.Parameters.AddWithValue("@item_name", ingredientDto.name);
                    insertCommand.Parameters.AddWithValue("@quantity", ingredientDto.quantity);
                    insertCommand.Parameters.AddWithValue("@price", ingredientDto.price);
                    insertCommand.Parameters.AddWithValue("@status", status); // Use the calculated status
                    insertCommand.Parameters.AddWithValue("@type", ingredientDto.type);
                    insertCommand.Parameters.AddWithValue("@createdAt", DateTime.UtcNow); // Assuming the current date time for createdAt
                    insertCommand.Parameters.AddWithValue("@last_updated_by", lastUpdatedBy);
                    insertCommand.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow);
                    insertCommand.Parameters.AddWithValue("@measurements", ingredientDto.measurements);

                    await insertCommand.ExecuteNonQueryAsync();
                }
            }
        }


        private async Task InsertOrUpdateIngredientAsync(IngriDTO ingredientDto, string lastUpdatedBy, string id, string itemId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();
                   
                // Check if the ingredient already exists in the database
                string sqlCheck = "SELECT COUNT(*) FROM batches WHERE item_id = @item_name";
                using (var checkCommand = new MySqlCommand(sqlCheck, connection))
                {
                    checkCommand.Parameters.AddWithValue("@item_name", ingredientDto.name);
                    int ingredientCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());

                    if (ingredientCount > 0)
                    {
                        // If the ingredient exists, update the existing record
                        string sqlUpdate = "UPDATE batches SET quantity = @quantity, price = @price, last_modified_by = @last_updated_by, last_modified = @last_updated_at WHERE item_id = @id";
                        using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                        {
                            updateCommand.Parameters.AddWithValue("@quantity", ingredientDto.quantity);
                            updateCommand.Parameters.AddWithValue("@price", ingredientDto.price);
                            updateCommand.Parameters.AddWithValue("@id", itemId);
                            updateCommand.Parameters.AddWithValue("@last_updated_by", lastUpdatedBy);
                            updateCommand.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow);

                            await updateCommand.ExecuteNonQueryAsync();
                        }
                    }
                    else
                    {
                        // If the ingredient does not exist, insert a new record
                        string sqlInsert = "INSERT INTO batches(id, item_id, quantity, price,  created, last_modified_by, last_modified) VALUES(@id, @item_id, @quantity, @price, @createdAt, @last_updated_by, @last_updated_at)";
                        using (var insertCommand = new MySqlCommand(sqlInsert, connection))
                        {
                            insertCommand.Parameters.AddWithValue("@id", id);
                            insertCommand.Parameters.AddWithValue("@item_id", itemId);
                            insertCommand.Parameters.AddWithValue("@quantity", ingredientDto.quantity);
                            insertCommand.Parameters.AddWithValue("@price", ingredientDto.price);
                            insertCommand.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
                            insertCommand.Parameters.AddWithValue("@last_updated_by", lastUpdatedBy);
                            insertCommand.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow);

                            await insertCommand.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }




        private async Task InsertOrUpdateThresholdConfigAsync(string name, int good, int bad, string itemId)
        {
            Guid configId = Guid.NewGuid(); // Generating new Guid for config ID
            string config = configId.ToString().ToLower();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync(); // Open the connection asynchronously

                // Check if the item_id already exists in the thresholdconfig table
                string sqlThresholdCheck = "SELECT COUNT(*) FROM thresholdconfig WHERE item_id = @item_id";
                int thresholdCount;
                using (var thresholdCheckCommand = new MySqlCommand(sqlThresholdCheck, connection))
                {
                    thresholdCheckCommand.Parameters.AddWithValue("@item_id", itemId);
                    thresholdCount = Convert.ToInt32(await thresholdCheckCommand.ExecuteScalarAsync());
                }

                if (thresholdCount > 0)
                {
                    // If the item_id exists, update the thresholdconfig record
                    string sqlUpdateThreshold = "UPDATE thresholdconfig SET good_threshold = @goodThreshold, critical_threshold = @badThreshold WHERE item_id = @item_id";
                    using (var updateThresholdCommand = new MySqlCommand(sqlUpdateThreshold, connection))
                    {
                        updateThresholdCommand.Parameters.AddWithValue("@goodThreshold", good);
                        updateThresholdCommand.Parameters.AddWithValue("@badThreshold", bad);
                        updateThresholdCommand.Parameters.AddWithValue("@item_id", itemId);

                        await updateThresholdCommand.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    // If the item_id does not exist, insert a new thresholdconfig record
                    string sqlInsertThreshold = "INSERT INTO thresholdconfig (Id, item_id, item, good_threshold, critical_threshold) VALUES (@id, @item_id, @itemName, @goodThreshold, @badThreshold)";
                    using (var insertThresholdCommand = new MySqlCommand(sqlInsertThreshold, connection))
                    {
                        insertThresholdCommand.Parameters.AddWithValue("@id", config);
                        insertThresholdCommand.Parameters.AddWithValue("@item_id", itemId);
                        insertThresholdCommand.Parameters.AddWithValue("@itemName", name);
                        insertThresholdCommand.Parameters.AddWithValue("@goodThreshold", good);
                        insertThresholdCommand.Parameters.AddWithValue("@badThreshold", bad);

                        await insertThresholdCommand.ExecuteNonQueryAsync();
                    }
                }
            }
        }




        [HttpGet]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetAllIngredients()
        {
            try
            {
                List<IngriDTP> ingredientsDtoList = new List<IngriDTP>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    // Fetch all ingredients
                    string sql = "SELECT * FROM Item"; // Adjust SQL query as per your database schema

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                double quantity = Convert.ToDouble(reader["quantity"]);


                                IngriDTP ingredientDto = new IngriDTP
                                {
                                    id = reader["id"].ToString(),
                                    name = reader["item_name"].ToString(),
                                    quantity = quantity,
                                    price = Convert.ToDecimal(reader["price"]),
                                    status = reader["status"].ToString(),
                                    type = reader["type"].ToString(),
                                    createdAt = Convert.ToDateTime(reader["created_at"]),
                                    lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                    lastUpdatedAt = reader["last_updated_at"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_at"]) : DateTime.MinValue,
                                    measurements = reader["measurements"].ToString(),
                                    isActive = Convert.ToBoolean(reader["is_active"])
                                };

                                ingredientsDtoList.Add(ingredientDto);
                            }
                        }
                    }
                }

                return Ok(ingredientsDtoList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching all ingredients.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching ingredients data.");
            }
        }



        [HttpGet("active")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetActiveIngredients()
        {
            try
            {
                List<string> itemId = await GetItemIdsAsync();

                if (itemId != null)
                {
                    foreach (string item in itemId)
                    {
                        await UpdateItemFromBatchesAsync(item);
                    }
                }
               
                List<Ingri> activeIngredients = GetActiveIngredientsFromDatabase();

                if (activeIngredients == null || !activeIngredients.Any())
                    return NotFound("No active ingredients found");

                // Map Ingri entities to IngriDTP DTOs
                var ingredientsDto = new List<IngriDTP>();

                foreach (var ingredient in activeIngredients)
                {
                    // Fetch thresholds for each item
                    var thresholds = await GetThresholdsForItemAsync(ingredient.id);

                    ingredientsDto.Add(new IngriDTP
                    {
                        id = ingredient.id,
                        name = ingredient.name,
                        quantity = ingredient.quantity,
                        measurements = ingredient.measurements,
                        price = ingredient.price,
                        status = ingredient.status,
                        type = ingredient.type,
                        createdAt = ingredient.createdAt,
                        isActive = ingredient.isActive,
                        lastUpdatedBy = ingredient.lastUpdatedBy,
                        lastUpdatedAt = ingredient.lastUpdatedAt,
                        goodThreshold = thresholds.goodThreshold, // Assuming you add this field in your DTO
                        criticalThreshold = thresholds.criticalThreshold // Assuming you add this field in your DTO
                    });
                }

                return Ok(ingredientsDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching active ingredients");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }

        private async Task<List<string>> GetItemIdsAsync()
        {
            var itemIds = new List<string>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Select Ids from item table where status is 'critical' and item exists in batches table
                string sqlSelectIds = @"
            SELECT i.Id 
            FROM item i 
            INNER JOIN batches b ON i.Id = b.item_id 
            WHERE i.status = 'critical'";

                using (var command = new MySqlCommand(sqlSelectIds, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Assuming the Id is stored as a string
                            string id = reader.GetString("Id");
                            itemIds.Add(id);
                        }
                    }
                }
            }

            return itemIds;
        }


        private async Task UpdateItemFromBatchesAsync(string itemId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                // Step 1: Retrieve the values of price and quantity from the batches table
                string sqlSelectBatch = "SELECT id, price, quantity FROM batches WHERE item_id = @id AND is_active = 1";
                decimal batchPrice = 0;
                int batchQuantity = 0;
                string batchid = "";

                using (var selectCommand = new MySqlCommand(sqlSelectBatch, connection))
                {
                    selectCommand.Parameters.AddWithValue("@id", itemId);

                    using (var reader = await selectCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            batchPrice = reader.GetDecimal("price");
                            batchQuantity = reader.GetInt32("quantity");
                            batchid = reader["id"].ToString();
                        }
                    }
                }

                // Step 2: Retrieve the current quantity from the item table
                string sqlSelectItemQuantity = "SELECT quantity FROM item WHERE Id = @id";
                int currentItemQuantity = 0;

                using (var selectItemCommand = new MySqlCommand(sqlSelectItemQuantity, connection))
                {
                    selectItemCommand.Parameters.AddWithValue("@id", itemId);

                    using (var reader = await selectItemCommand.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            currentItemQuantity = reader.GetInt32("quantity");
                        }
                    }
                }

                // Step 3: Add the batch quantity to the current item quantity
                int updatedQuantity = currentItemQuantity + batchQuantity;

                // Step 4: Update the Item table with the new quantity and batch price
                string sqlUpdateItem = "UPDATE item SET price = @price, quantity = @quantity, last_updated_by = @last_updated_by, last_updated_at = @last_updated_at, status = 'good' WHERE Id = @id";

                using (var updateCommand = new MySqlCommand(sqlUpdateItem, connection))
                {
                    updateCommand.Parameters.AddWithValue("@price", batchPrice);
                    updateCommand.Parameters.AddWithValue("@quantity", updatedQuantity);
                    updateCommand.Parameters.AddWithValue("@last_updated_by", "System Update");
                    updateCommand.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow);
                    updateCommand.Parameters.AddWithValue("@id", itemId);

                    await updateCommand.ExecuteNonQueryAsync();
                }

                // Step 5: Update is_active = 0 in the batches table for the selected batch
                string sqlUpdateBatch = "UPDATE batches SET is_active = 0 WHERE id = @batchid";

                using (var updateBatchCommand = new MySqlCommand(sqlUpdateBatch, connection))
                {
                    updateBatchCommand.Parameters.AddWithValue("@batchid", batchid);

                    await updateBatchCommand.ExecuteNonQueryAsync();
                }
            }
        }


        private List<Ingri> GetActiveIngredientsFromDatabase()
        {
            List<Ingri> activeIngredients = new List<Ingri>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                connection.Open();

                string sql = "SELECT * FROM Item WHERE is_active = @isActive";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@isActive", true);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Ingri ingredient = new Ingri
                            {
                                id = reader["id"].ToString(),
                                name = reader["item_name"].ToString(),
                                quantity = Convert.ToDouble(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                createdAt = Convert.ToDateTime(reader["created_at"]),
                                lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                lastUpdatedAt = reader["last_updated_at"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_at"]) : DateTime.MinValue,
                                measurements = reader["measurements"].ToString(),
                                isActive = Convert.ToBoolean(reader["is_active"])
                            };

                            activeIngredients.Add(ingredient);
                        }
                    }
                }
            }

            // Sort the ingredients by status
            activeIngredients = activeIngredients.OrderBy(ingredient =>
            {
                return ingredient.status switch
                {
                    "critical" => 1,
                    "mid" => 2,
                    "good" => 3,
                    _ => 4 // Default case for any other statuses
                };
            }).ToList();

            return activeIngredients;
        }


        private async Task<(int goodThreshold, int criticalThreshold)> GetThresholdsForItemAsync(string itemId)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT good_threshold, critical_threshold 
            FROM thresholdconfig 
            WHERE item_id = @itemId";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@itemId", itemId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int goodThreshold = reader.GetInt32(reader.GetOrdinal("good_threshold"));
                            int criticalThreshold = reader.GetInt32(reader.GetOrdinal("critical_threshold"));

                            return (goodThreshold, criticalThreshold);
                        }
                        else
                        {
                            throw new Exception($"Thresholds not found for itemId {itemId}");
                        }
                    }
                }
            }
        }

        [HttpGet("by-name")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult GetIngredientByName([FromQuery] string name)
        {
            try
            {
                List<Ingri> ingredients = GetIngredientsFromDatabaseByName(name);

                if (ingredients == null || !ingredients.Any())
                    return NotFound();

                // Map Ingri entities to IngriDTP DTOs
                var ingredientsDto = ingredients.Select(ingredient => new IngriDTP
                {
                    name = ingredient.name,
                    quantity = ingredient.quantity,
                    measurements = ingredient.measurements,
                    price = ingredient.price,
                    status = ingredient.status,
                    type = ingredient.type,
                    createdAt = ingredient.createdAt,
                    lastUpdatedBy = ingredient.lastUpdatedBy,
                    lastUpdatedAt = ingredient.lastUpdatedAt
                }).ToList();

                return Ok(ingredientsDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the ingredient by name");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }


        [HttpGet("by-status/{status}")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult GetIngredientsByStatus(string status)
        {
            try
            {
                List<Ingri> ingredients = GetIngredientsFromDatabaseByStatus(status);

                if (ingredients == null || !ingredients.Any())
                    return NotFound();

                // Map Ingri entities to IngriDTP DTOs
                var ingredientsDto = ingredients.Select(ingredient => new IngriDTP
                {
                    name = ingredient.name,
                    quantity = ingredient.quantity,
                    measurements = ingredient.measurements,
                    price = ingredient.price,
                    status = ingredient.status,
                    type = ingredient.type,
                    createdAt = ingredient.createdAt,
                    lastUpdatedBy = ingredient.lastUpdatedBy,
                    lastUpdatedAt = ingredient.lastUpdatedAt
                }).ToList();

                return Ok(ingredientsDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching ingredients by status");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }


        [HttpGet("by-type/{type}")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult GetIngredientsByType(string type)
        {
            try
            {
                List<Ingri> ingredients = GetIngredientsFromDatabaseByType(type);

                if (ingredients == null || !ingredients.Any())
                    return NotFound();

                // Map Ingri entities to IngriDTP DTOs
                var ingredientsDto = ingredients.Select(ingredient => new IngriDTP
                {
                    name = ingredient.name,
                    quantity = ingredient.quantity,
                    measurements = ingredient.measurements,
                    price = ingredient.price,
                    status = ingredient.status,
                    type = ingredient.type,
                    createdAt = ingredient.createdAt,
                    lastUpdatedBy = ingredient.lastUpdatedBy,
                    lastUpdatedAt = ingredient.lastUpdatedAt
                }).ToList();

                return Ok(ingredientsDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching ingredients by type");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }


        [HttpPatch("{id}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> UpdateIngredient(string id, [FromBody] IngriDTOs? updatedIngredient)
        {
            try
            {
                // Retrieve the existing ingredient from the database
                Ingri existingIngredient = GetIngredientFromDatabase(id);

                if (existingIngredient == null)
                {
                    return NotFound();
                }

                // Extract the username from the token
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("User is not authorized");
                }

                // Get the last updated by user
                string lastUpdatedBy = await GetLastupdater(username);
                if (lastUpdatedBy == null)
                {
                    return Unauthorized("Username not found");
                }

                // Map properties from IngriDTO to Ingri, only if the updated values are provided
                if (updatedIngredient != null)
                {
                    if (!string.IsNullOrEmpty(updatedIngredient.name))
                    {
                        existingIngredient.name = updatedIngredient.name;
                    }
                    if (updatedIngredient.quantity.HasValue)
                    {
                        existingIngredient.quantity = updatedIngredient.quantity.Value;
                        existingIngredient.isActive = true;  // Set to active when quantity is updated
                    }

                    if (updatedIngredient.price.HasValue)
                    {
                        existingIngredient.price = updatedIngredient.price.Value;
                    }
                    if (!string.IsNullOrEmpty(updatedIngredient.type))
                    {
                        existingIngredient.type = updatedIngredient.type;
                    }
                    if (!string.IsNullOrEmpty(updatedIngredient.measurements))
                    {
                        existingIngredient.measurements = updatedIngredient.measurements;
                    }
                }

                // Set the last updated fields
                existingIngredient.lastUpdatedBy = lastUpdatedBy;
                existingIngredient.lastUpdatedAt = DateTime.UtcNow;

                // Update the ingredient in the database
                UpdateIngredientInDatabase(existingIngredient);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the ingredient");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }


        [HttpPatch("threshold/update/{Id}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> UpdateThresholdConfig(string Id, [FromBody] thresholdUpdate thresholds)
        {
            try
            {
                // Ensure that the thresholds are valid
                if (thresholds.good <= thresholds.critical || thresholds.critical <= 0)
                {
                    return BadRequest("Invalid threshold values. Ensure goodThreshold is greater than criticalThreshold and criticalThreshold is positive.");
                }

                // Update the threshold configuration in the database
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sqlUpdate = "UPDATE thresholdconfig SET good_threshold = @goodThreshold, critical_threshold = @criticalThreshold WHERE item_id = @Id";

                    using (var command = new MySqlCommand(sqlUpdate, connection))
                    {
                        command.Parameters.AddWithValue("@goodThreshold", thresholds.good);
                        command.Parameters.AddWithValue("@criticalThreshold", thresholds.critical);
                        command.Parameters.AddWithValue("@Id", Id);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                        {
                            return NotFound("Threshold configuration not found.");
                        }
                    }
                }

                return Ok("Threshold configuration updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the threshold configuration.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }




        [HttpDelete("{id}")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> DeleteIngredient(string id)
        {
            try
            {
                // Extract the username from the token
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("User is not authorized");
                }

                // Get the last updated by user
                string lastUpdatedBy = await GetLastupdater(username);
                if (lastUpdatedBy == null)
                {
                    return Unauthorized("Username not found");
                }

                DateTime lastUpdatedAt = DateTime.UtcNow;

                using (var connection = new MySqlConnection(connectionstring))
                {
                    connection.Open();

                    // Check if the ingredient exists
                    string sqlCheck = "SELECT COUNT(*) FROM Item WHERE Id = @id";
                    using (var checkCommand = new MySqlCommand(sqlCheck, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@id", id);
                        int ingredientCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (ingredientCount == 0)
                        {
                            return NotFound("Ingredient not found");
                        }
                    }

                    // Set isActive to false instead of deleting
                    string sqlUpdate = "UPDATE Item SET is_active = @isActive, last_updated_by = @last_updated_by, last_updated_at = @last_updated_at  WHERE Id = @id";
                    using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                    {
                        updateCommand.Parameters.AddWithValue("@id", id);
                        updateCommand.Parameters.AddWithValue("@isActive", false);
                        updateCommand.Parameters.AddWithValue("@last_updated_by", lastUpdatedBy);
                        updateCommand.Parameters.AddWithValue("@last_updated_at", lastUpdatedAt);
                        updateCommand.ExecuteNonQuery();
                    }
                }

                return Ok("Ingredient status updated to inactive successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the ingredient status");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }

        [HttpPatch]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> ReactivateIngredient([FromQuery] string restore)
        {
            try
            {
                // Extract the username from the token
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("User is not authorized");
                }

                // Get the last updated by user
                string lastUpdatedBy = await GetLastupdater(username);
                if (lastUpdatedBy == null)
                {
                    return Unauthorized("Username not found");
                }

                DateTime lastUpdatedAt = DateTime.UtcNow;

                using (var connection = new MySqlConnection(connectionstring))
                {
                    connection.Open();

                    // Check if the ingredient exists
                    string sqlCheck = "SELECT COUNT(*) FROM Item WHERE Id = @id";
                    using (var checkCommand = new MySqlCommand(sqlCheck, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@id", restore);
                        int ingredientCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (ingredientCount == 0)
                        {
                            return NotFound("Ingredient not found");
                        }
                    }

                    // Reactivate the ingredient by setting isActive to true
                    string sqlUpdate = "UPDATE Item SET is_active = @isActive, last_updated_by = @last_updated_by, last_updated_at = @last_updated_at WHERE Id = @id";
                    using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                    {
                        updateCommand.Parameters.AddWithValue("@id", restore);
                        updateCommand.Parameters.AddWithValue("@isActive", true); // Reactivate the ingredient
                        updateCommand.Parameters.AddWithValue("@last_updated_by", lastUpdatedBy);
                        updateCommand.Parameters.AddWithValue("@last_updated_at", lastUpdatedAt);
                        updateCommand.ExecuteNonQuery();
                    }
                }

                return Ok($"Ingredient with ID {restore} has been successfully reactivated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while reactivating ingredient with ID {restore}.");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }


        [HttpGet("inactive")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult GetInactiveIngredients()
        {
            try
            {
                List<Ingri> ingredients = GetInactiveIngredientsFromDatabase();

                if (ingredients == null || !ingredients.Any())
                {
                    return NotFound("No inactive ingredients found");
                }

                // Map Ingri entities to IngriDTP DTOs
                var ingredientsDto = ingredients.Select(ingredient => new IngriDTP
                {
                    name = ingredient.name,
                    quantity = ingredient.quantity,
                    measurements = ingredient.measurements,
                    price = ingredient.price,
                    status = ingredient.status,
                    type = ingredient.type,
                    createdAt = ingredient.createdAt,
                    lastUpdatedBy = ingredient.lastUpdatedBy,
                    lastUpdatedAt = ingredient.lastUpdatedAt
                }).ToList();

                return Ok(ingredientsDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching inactive ingredients");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }


        private List<Ingri> GetInactiveIngredientsFromDatabase()
        {
            List<Ingri> ingredients = new List<Ingri>();

            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    connection.Open();

                    string sql = "SELECT * FROM Item WHERE is_active = @isActive";
                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@isActive", false);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Ingri ingredient = new Ingri
                                {
                                    id = reader["id"].ToString(),
                                    name = reader["item_name"].ToString(),
                                    quantity = Convert.ToDouble(reader["quantity"]),
                                    price = Convert.ToDecimal(reader["price"]),
                                    status = reader["status"].ToString(),
                                    type = reader["type"].ToString(),
                                    createdAt = Convert.ToDateTime(reader["created_at"]),
                                    lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                    lastUpdatedAt = reader["last_updated_at"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_at"]) : DateTime.MinValue,
                                    measurements = reader["measurements"].ToString(),
                                    isActive = Convert.ToBoolean(reader["is_active"]) // Ensure this matches your database type
                                };

                                ingredients.Add(ingredient);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
                _logger.LogError(ex, "An error occurred while fetching inactive ingredients from the database");
                throw; // Optionally rethrow the exception or handle it gracefully
            }

            return ingredients;
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


        private List<Ingri> GetIngredientsFromDatabaseByName(string name)
        {
            List<Ingri> ingredients = new List<Ingri>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                connection.Open();

                string sql = "SELECT * FROM Item WHERE item_name LIKE @name";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@name", "%" + name + "%");

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Ingri ingredient = new Ingri
                            {
                                id = reader["id"].ToString(),
                                name = reader["item_name"].ToString(),
                                quantity = Convert.ToDouble(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                createdAt = Convert.ToDateTime(reader["created_at"]),
                                lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                lastUpdatedAt = reader["last_updated_at"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_at"]) : DateTime.MinValue,
                                measurements = reader["measurements"].ToString(),
                                isActive = Convert.ToInt32(reader["quantity"]) > 0
                            };

                            ingredients.Add(ingredient);
                        }
                    }
                }
            }

            return ingredients;
        }

        private List<Ingri> GetIngredientsFromDatabaseByStatus(string status)
        {
            List<Ingri> ingredients = new List<Ingri>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                connection.Open();

                string sql = "SELECT * FROM Item WHERE status LIKE @status";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@status", "%" + status + "%");

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Ingri ingredient = new Ingri
                            {
                                id = reader["id"].ToString(),
                                name = reader["item_name"].ToString(),
                                quantity = Convert.ToDouble(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                createdAt = Convert.ToDateTime(reader["created_at"]),
                                lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                lastUpdatedAt = reader["last_updated_at"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_at"]) : DateTime.MinValue,
                                measurements = reader["measurements"].ToString(),
                                isActive = Convert.ToInt32(reader["quantity"]) > 0
                            };

                            ingredients.Add(ingredient);
                        }
                    }
                }
            }

            return ingredients;
        }

        private List<Ingri> GetIngredientsFromDatabaseByType(string type)
        {
            List<Ingri> ingredients = new List<Ingri>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                connection.Open();

                string sql = "SELECT * FROM Item WHERE type LIKE @type";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@type", "%" + type + "%");

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Ingri ingredient = new Ingri
                            {
                                id = reader["id"].ToString(),
                                name = reader["item_name"].ToString(),
                                quantity = Convert.ToDouble(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                createdAt = Convert.ToDateTime(reader["created_at"]),
                                lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                lastUpdatedAt = reader["last_updated_at"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_at"]) : DateTime.MinValue,
                                measurements = reader["measurements"].ToString(),
                                isActive = Convert.ToInt32(reader["quantity"]) > 0
                            };

                            ingredients.Add(ingredient);
                        }
                    }
                }
            }

            return ingredients;
        }

        private Ingri GetIngredientFromDatabase(string id)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                connection.Open();

                string sql = "SELECT * FROM Item WHERE Id = @id";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", id);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Ingri
                            {
                                id = reader["id"].ToString(),
                                name = reader["item_name"].ToString(),
                                quantity = Convert.ToDouble(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                createdAt = Convert.ToDateTime(reader["created_at"]),
                                lastUpdatedBy = reader.IsDBNull(reader.GetOrdinal("last_updated_by")) ? null : reader.GetString(reader.GetOrdinal("last_updated_by")),
                                lastUpdatedAt = reader["last_updated_at"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_at"]) : DateTime.MinValue,
                                measurements = reader["measurements"].ToString(),
                                isActive = Convert.ToInt32(reader["quantity"]) > 0
                            };
                        }
                    }
                }
            }

            return null;
        }

        private void UpdateIngredientInDatabase(Ingri ingredient)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                connection.Open();

                string sql = "UPDATE Item SET item_name = @item_name, quantity = @quantity, price = @price, type = @type, measurements = @measurements, last_updated_by = @last_updated_by, last_updated_at = @last_updated_at, is_active = @isActive WHERE Id = @id";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", ingredient.id);
                    command.Parameters.AddWithValue("@item_name", ingredient.name);
                    command.Parameters.AddWithValue("@quantity", ingredient.quantity);
                    command.Parameters.AddWithValue("@price", ingredient.price);
                    command.Parameters.AddWithValue("@type", ingredient.type);
                    command.Parameters.AddWithValue("@last_updated_by", ingredient.lastUpdatedBy);
                    command.Parameters.AddWithValue("@last_updated_at", ingredient.lastUpdatedAt);
                    command.Parameters.AddWithValue("@measurements", ingredient.measurements);
                    command.Parameters.AddWithValue("@isActive", ingredient.isActive);

                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
