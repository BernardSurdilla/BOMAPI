using BOM_API_v2.Services;
using CRUDFI.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;
using System.Diagnostics;
using System.Security.Claims;

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

                using (var connection = new MySqlConnection(connectionstring))
                {
                    connection.Open();

                    // Check if the ingredient already exists in the database
                    string sqlCheck = "SELECT COUNT(*) FROM item WHERE item_name = @item_name";
                    using (var checkCommand = new MySqlCommand(sqlCheck, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@item_name", ingredientDto.name);
                        int ingredientCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (ingredientCount > 0)
                        {
                            // If the ingredient exists, update the existing record
                            string sqlUpdate = "UPDATE item SET quantity = @quantity, price = @price, last_updated_by = @last_updated_by,status = @status, last_updated_at = @last_updated_at WHERE item_name = @item_name";
                            using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@quantity", ingredientDto.quantity);
                                updateCommand.Parameters.AddWithValue("@price", ingredientDto.price);
                                updateCommand.Parameters.AddWithValue("@item_name", ingredientDto.name);
                                updateCommand.Parameters.AddWithValue("@last_updated_by", lastUpdatedBy);
                                updateCommand.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow);
                                updateCommand.Parameters.AddWithValue("@status", status);

                                updateCommand.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // If the ingredient does not exist, insert a new record
                            string sqlInsert = "INSERT INTO item(item_name, quantity, price, status, type, created_at, last_updated_by, last_updated_at, measurements) VALUES(@item_name, @quantity, @price, @status, @type, @createdAt, @last_updated_by, @last_updated_at, @measurements)";
                            using (var insertCommand = new MySqlCommand(sqlInsert, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@item_name", ingredientDto.name);
                                insertCommand.Parameters.AddWithValue("@quantity", ingredientDto.quantity);
                                insertCommand.Parameters.AddWithValue("@price", ingredientDto.price);
                                insertCommand.Parameters.AddWithValue("@status", status); // Use the calculated status
                                insertCommand.Parameters.AddWithValue("@type", ingredientDto.type);
                                insertCommand.Parameters.AddWithValue("@createdAt", DateTime.UtcNow); // Assuming the current date time for createdAt
                                insertCommand.Parameters.AddWithValue("@last_updated_by", lastUpdatedBy);
                                insertCommand.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow);
                                insertCommand.Parameters.AddWithValue("@measurements", ingredientDto.measurements);

                                insertCommand.ExecuteNonQuery();
                            }
                        }
                    }

                    await InsertOrUpdateThresholdConfigAsync(connection, ingredientDto.name, Convert.ToInt32(ingredientDto.good), Convert.ToInt32(ingredientDto.bad));

                }

                return Ok("Ingredient and threshold configuration added successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the request");
                ModelState.AddModelError("ingredient", "Sorry, but we encountered an exception while processing your request");
                return BadRequest(ModelState);
            }
        }

        private async Task InsertOrUpdateThresholdConfigAsync(MySqlConnection connection, string name, int good, int bad)
        {
            // Retrieve the item ID for thresholdconfig insertion or update
            string sqlGetItemId = "SELECT Id FROM item WHERE item_name = @item_name";
            int itemId;
            using (var getItemIdCommand = new MySqlCommand(sqlGetItemId, connection))
            {
                getItemIdCommand.Parameters.AddWithValue("@item_name", name);
                itemId = Convert.ToInt32(await getItemIdCommand.ExecuteScalarAsync());
            }

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
                string sqlInsertThreshold = "INSERT INTO thresholdconfig (item_id, item, good_threshold, critical_threshold) VALUES (@item_id, @itemName, @goodThreshold, @badThreshold)";
                using (var insertThresholdCommand = new MySqlCommand(sqlInsertThreshold, connection))
                {
                    insertThresholdCommand.Parameters.AddWithValue("@item_id", itemId);
                    insertThresholdCommand.Parameters.AddWithValue("@itemName", name);
                    insertThresholdCommand.Parameters.AddWithValue("@goodThreshold", good);
                    insertThresholdCommand.Parameters.AddWithValue("@badThreshold", bad);

                    await insertThresholdCommand.ExecuteNonQueryAsync();
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
                                    id = Convert.ToInt32(reader["id"]),
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
                                id = Convert.ToInt32(reader["id"]),
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

            return activeIngredients;
        }

        private async Task<(int goodThreshold, int criticalThreshold)> GetThresholdsForItemAsync(int itemId)
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



        [HttpGet("{id}")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult GetIngredientById(int id)
        {
            try
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
                                var ingredient = new
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    name = reader["item_name"].ToString(),
                                    Quantity = Convert.ToDouble(reader["quantity"]),
                                    Price = Convert.ToDecimal(reader["price"]),
                                    Status = reader["status"].ToString(),
                                    CreatedAt = Convert.ToDateTime(reader["created_at"]),
                                    LastUpdatedBy = reader["last_updated_by"] != DBNull.Value ? reader["last_updated_by"].ToString() : null,
                                    LastUpdatedAt = reader["last_updated_at"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_at"]) : (DateTime?)null,
                                    Measurements = reader["measurements"].ToString()
                                };
                                return Ok(ingredient);
                            }
                            else
                            {
                                return NotFound();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the ingredient by ID");
                return StatusCode(500, "An error occurred while processing the request");
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
        public async Task<IActionResult> UpdateIngredient(int id, [FromBody] IngriDTOs? updatedIngredient)
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
        public async Task<IActionResult> UpdateThresholdConfig(int Id, [FromBody] thresholdUpdate thresholds)
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
        public async Task<IActionResult> DeleteIngredient(int id)
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
        public async Task<IActionResult> ReactivateIngredient([FromQuery] int restore)
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
                                    id = Convert.ToInt32(reader["Id"]),
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
                                id = Convert.ToInt32(reader["Id"]),
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
                                id = Convert.ToInt32(reader["Id"]),
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
                                id = Convert.ToInt32(reader["Id"]),
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

        private Ingri GetIngredientFromDatabase(int id)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                connection.Open();

                string sql = "SELECT * FROM Item WHERE id = @id";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", id);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Ingri
                            {
                                id = reader.GetInt32(reader.GetOrdinal("Id")),
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
