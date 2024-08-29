using BOM_API_v2.Services;
using CRUDFI.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using BOM_API_v2.Helpers;
using System.Text.Json;
using System.Text;
using System.Data;
using UnitsNet;

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
        public IActionResult CreateIngredient([FromBody] IngriDTO ingredientDto)
        {
            try
            {
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

                // Fetch threshold values from the database
                var thresholds = GetThresholdValues();

                // Determine the status based on quantity and thresholds
                string status;
                if (ingredientDto.quantity <= thresholds.CriticalThreshold)
                {
                    status = "critical";
                }
                else if (ingredientDto.quantity >= thresholds.MidThreshold && ingredientDto.quantity < thresholds.GoodThreshold)
                {
                    status = "mid";
                }
                else if (ingredientDto.quantity >= thresholds.GoodThreshold)
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
                    string sqlCheck = "SELECT COUNT(*) FROM Item WHERE item_name = @item_name";
                    using (var checkCommand = new MySqlCommand(sqlCheck, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@item_name", ingredientDto.name);
                        int ingredientCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (ingredientCount > 0)
                        {
                            // If the ingredient exists, update the existing record
                            string sqlUpdate = "UPDATE Item SET quantity = quantity + @quantity, price = @price, last_updated_by = @last_updated_by, last_updated_at = @last_updated_at WHERE item_name = @item_name";
                            using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@quantity", ingredientDto.quantity);
                                updateCommand.Parameters.AddWithValue("@price", ingredientDto.price);
                                updateCommand.Parameters.AddWithValue("@item_name", ingredientDto.name);
                                updateCommand.Parameters.AddWithValue("@last_updated_by", lastUpdatedBy);
                                updateCommand.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow);

                                updateCommand.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // If the ingredient does not exist, insert a new record
                            string sqlInsert = "INSERT INTO Item(item_name, quantity, price, status, type, created_at, last_updated_by, last_updated_at, measurements) VALUES(@item_name, @quantity, @price, @status, @type, @createdAt, @last_updated_by, @last_updated_at, @measurements)";
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
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the request");
                ModelState.AddModelError("ingredient", "Sorry, but we encountered an exception while processing your request");
                return BadRequest(ModelState);
            }
        }


        private (int GoodThreshold, int MidThreshold, int CriticalThreshold) GetThresholdValues()
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                connection.Open();

                string sql = "SELECT good_threshold, mid_threshold, critical_threshold FROM thresholdconfig WHERE Id = 1";
                using (var command = new MySqlCommand(sql, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int goodThreshold = reader.GetInt32("good_threshold");
                            int midThreshold = reader.GetInt32("mid_threshold");
                            int criticalThreshold = reader.GetInt32("critical_threshold");

                            return (goodThreshold, midThreshold, criticalThreshold);
                        }
                        else
                        {
                            throw new Exception("Threshold values not found in the database.");
                        }
                    }
                }
            }
        }

        [HttpPost("admin/threshold-config/{item_id}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> AddThresholdConfig(int item_id, [FromBody] AddThreshold addThreshold)
        {
            try
            {
                // Ensure that the AddThreshold object is not null
                if (addThreshold == null)
                {
                    return BadRequest("Threshold data must be provided.");
                }

                // Ensure that the threshold values are valid
                if (string.IsNullOrEmpty(addThreshold.good) || string.IsNullOrEmpty(addThreshold.mid) || string.IsNullOrEmpty(addThreshold.bad))
                {
                    return BadRequest("All threshold values (good, mid, bad) must be provided.");
                }

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    // Check if the item with the given ID exists
                    string checkItemSql = "SELECT item_name FROM Items WHERE Id = @item_id";
                    string itemName = null;

                    using (var checkItemCommand = new MySqlCommand(checkItemSql, connection))
                    {
                        checkItemCommand.Parameters.AddWithValue("@item_id", item_id);

                        using (var reader = await checkItemCommand.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                itemName = reader["item_name"].ToString();
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(itemName))
                    {
                        return NotFound("No ingredient with the specified ID was found.");
                    }

                    // Insert the new threshold configuration
                    string insertSql = @"
                INSERT INTO thresholdconfig (item, good_threshold, mid_threshold, bad_threshold)
                VALUES (@itemName, @goodThreshold, @midThreshold, @badThreshold)";

                    using (var insertCommand = new MySqlCommand(insertSql, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@itemName", itemName);
                        insertCommand.Parameters.AddWithValue("@goodThreshold", addThreshold.good);
                        insertCommand.Parameters.AddWithValue("@midThreshold", addThreshold.mid);
                        insertCommand.Parameters.AddWithValue("@badThreshold", addThreshold.bad);

                        await insertCommand.ExecuteNonQueryAsync();
                    }
                }

                return Ok("Threshold configuration added successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while adding the threshold configuration.");
                return StatusCode(500, "An error occurred while processing the request.");
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

                    // Fetch the threshold values
                    var thresholds = await GetThresholdValuesAsync(connection);

                    // Fetch all ingredients
                    string sql = "SELECT * FROM Item"; // Adjust SQL query as per your database schema

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                double quantity = Convert.ToDouble(reader["quantity"]);
                                string status;

                                // Determine the status based on quantity and thresholds
                                if (quantity <= thresholds.CriticalThreshold)
                                {
                                    status = "critical";
                                }
                                else if (quantity >= thresholds.MidThreshold && quantity < thresholds.GoodThreshold)
                                {
                                    status = "mid";
                                }
                                else if (quantity >= thresholds.GoodThreshold)
                                {
                                    status = "good";
                                }
                                else
                                {
                                    status = "normal"; // Default status if none of the conditions are met
                                }

                                IngriDTP ingredientDto = new IngriDTP
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    name = reader["item_name"].ToString(),
                                    quantity = quantity,
                                    price = Convert.ToDecimal(reader["price"]),
                                    status = status, // Use the determined status
                                    type = reader["type"].ToString(),
                                    CreatedAt = Convert.ToDateTime(reader["created_at"]),
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

        // Helper method to fetch threshold values
        private async Task<ThresholdConfig> GetThresholdValuesAsync(MySqlConnection connection)
        {
            string sql = "SELECT * FROM thresholdconfig WHERE Id = 1"; // Assumes there's only one row with Id = 1

            using (var command = new MySqlCommand(sql, connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new ThresholdConfig
                        {
                            GoodThreshold = reader.GetInt32("good_threshold"),
                            MidThreshold = reader.GetInt32("mid_threshold"),
                            CriticalThreshold = reader.GetInt32("critical_threshold")
                        };
                    }
                    else
                    {
                        throw new Exception("Threshold configuration not found.");
                    }
                }
            }
        }




        [HttpGet("active")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult GetActiveIngredients()
        {
            try
            {
                List<Ingri> activeIngredients = GetActiveIngredientsFromDatabase();

                if (activeIngredients == null || !activeIngredients.Any())
                    return NotFound("No active ingredients found");

                // Map Ingri entities to IngriDTP DTOs
                var ingredientsDto = activeIngredients.Select(ingredient => new IngriDTP
                {
                    Id = ingredient.Id,
                    name = ingredient.name,
                    quantity = ingredient.quantity,
                    measurements = ingredient.measurements,
                    price = ingredient.price,
                    status = ingredient.status,
                    type = ingredient.type,
                    CreatedAt = ingredient.CreatedAt,
                    isActive = ingredient.isActive,
                    lastUpdatedBy = ingredient.lastUpdatedBy,
                    lastUpdatedAt = ingredient.lastUpdatedAt
                }).ToList();

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
                                Id = Convert.ToInt32(reader["id"]),
                                name = reader["item_name"].ToString(),
                                quantity = Convert.ToDouble(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["created_at"]),
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
                    CreatedAt = ingredient.CreatedAt,
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
                    CreatedAt = ingredient.CreatedAt,
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
                    CreatedAt = ingredient.CreatedAt,
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
        public async Task<IActionResult> UpdateIngredient(int id, [FromBody] IngriDTO updatedIngredient)
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

                // Map properties from IngriDTO to Ingri
                if (!string.IsNullOrEmpty(updatedIngredient.name))
                {
                    existingIngredient.name = updatedIngredient.name;
                }
                if (updatedIngredient.quantity > 0)
                {
                    existingIngredient.quantity = updatedIngredient.quantity;
                    existingIngredient.isActive = true;
                }
                else
                {
                    existingIngredient.isActive = false;
                }
                if (updatedIngredient.price > 0)
                {
                    existingIngredient.price = updatedIngredient.price;
                }
                if (!string.IsNullOrEmpty(updatedIngredient.type))
                {
                    existingIngredient.type = updatedIngredient.type;
                }
                if (!string.IsNullOrEmpty(updatedIngredient.measurements))
                {
                    existingIngredient.measurements = updatedIngredient.measurements;
                }

                // Set the status based on the quantity
                if (existingIngredient.quantity < 30)
                {
                    existingIngredient.status = "critical";
                }
                else if (existingIngredient.quantity == 50)
                {
                    existingIngredient.status = "mid";
                }
                else if (existingIngredient.quantity > 100)
                {
                    existingIngredient.status = "good";
                }
                else
                {
                    existingIngredient.status = "normal"; // Default status for quantities between 30 and 100
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

        [HttpPatch("threshold/update")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> UpdateThresholdConfig([FromQuery] int goodThreshold, [FromQuery] int midThreshold)
        {
            try
            {
                // Ensure that the thresholds are valid
                if (goodThreshold <= midThreshold || midThreshold <= 0)
                {
                    return BadRequest("Invalid threshold values. Ensure goodThreshold is greater than midThreshold and midThreshold is positive.");
                }

                // Calculate the criticalThreshold
                int criticalThreshold = midThreshold - 1;

                // Update the threshold configuration in the database
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sqlUpdate = "UPDATE thresholdconfig SET good_threshold = @goodThreshold, mid_threshold = @midThreshold, critical_threshold = @criticalThreshold WHERE Id = 1";

                    using (var command = new MySqlCommand(sqlUpdate, connection))
                    {
                        command.Parameters.AddWithValue("@goodThreshold", goodThreshold);
                        command.Parameters.AddWithValue("@midThreshold", midThreshold);
                        command.Parameters.AddWithValue("@criticalThreshold", criticalThreshold);

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



        [HttpDelete("ingredients/{id}")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult DeleteIngredient([FromQuery] int id)
        {
            try
            {
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
                    string sqlUpdate = "UPDATE Item SET is_active = @isActive WHERE Id = @id";
                    using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                    {
                        updateCommand.Parameters.AddWithValue("@id", id);
                        updateCommand.Parameters.AddWithValue("@isActive", false);
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

        [HttpPut]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult ReactivateIngredient([FromQuery] int restore)
        {
            try
            {
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
                    string sqlUpdate = "UPDATE Item SET is_active = @isActive WHERE Id = @id";
                    using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                    {
                        updateCommand.Parameters.AddWithValue("@id", restore);
                        updateCommand.Parameters.AddWithValue("@isActive", true); // Reactivate the ingredient
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
                    CreatedAt = ingredient.CreatedAt,
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
                                    Id = Convert.ToInt32(reader["Id"]),
                                    name = reader["item_name"].ToString(),
                                    quantity = Convert.ToDouble(reader["quantity"]),
                                    price = Convert.ToDecimal(reader["price"]),
                                    status = reader["status"].ToString(),
                                    type = reader["type"].ToString(),
                                    CreatedAt = Convert.ToDateTime(reader["created_at"]),
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
                                Id = Convert.ToInt32(reader["Id"]),
                                name = reader["item_name"].ToString(),
                                quantity = Convert.ToDouble(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["created_at"]),
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
                                Id = Convert.ToInt32(reader["Id"]),
                                name = reader["item_name"].ToString(),
                                quantity = Convert.ToDouble(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["created_at"]),
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
                                Id = Convert.ToInt32(reader["Id"]),
                                name = reader["item_name"].ToString(),
                                quantity = Convert.ToDouble(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["created_at"]),
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
                                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                name = reader["item_name"].ToString(),
                                quantity = Convert.ToDouble(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["created_at"]),
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

                string sql = "UPDATE Item SET item_name = @item_name, quantity = @quantity, price = @price, status = @status, type = @type, measurements = @measurements, last_updated_by = @last_updated_by, last_updated_at = @last_updated_at, is_active = @isActive WHERE Id = @id";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", ingredient.Id);
                    command.Parameters.AddWithValue("@item_name", ingredient.name);
                    command.Parameters.AddWithValue("@quantity", ingredient.quantity);
                    command.Parameters.AddWithValue("@price", ingredient.price);
                    command.Parameters.AddWithValue("@status", ingredient.status);
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
