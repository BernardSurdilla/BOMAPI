using BOM_API_v2.Services;
using CRUDFI.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;

namespace CRUDFI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AddingIngridientsController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<AddingIngridientsController> _logger;
        private readonly IActionLogger dbLogger;

        public AddingIngridientsController(IConfiguration configuration, ILogger<AddingIngridientsController> logger, IActionLogger dbLogger)
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
                byte[] lastUpdatedBy = FetchUserId(username);

                if (lastUpdatedBy == null)
                {
                    return Unauthorized("User ID not found");
                }

                // Determine the status based on quantity
                string status;
                if (ingredientDto.quantity < 50)
                {
                    status = "critical";
                }
                else if (ingredientDto.quantity >= 50)
                {
                    status = "mid";
                }
                else if (ingredientDto.quantity >= 100)
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
                        checkCommand.Parameters.AddWithValue("@item_name", ingredientDto.itemName);
                        int ingredientCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (ingredientCount > 0)
                        {
                            // If the ingredient exists, update the existing record
                            string sqlUpdate = "UPDATE Item SET quantity = quantity + @quantity, price = @price, last_updated_by = @last_updated_by, last_updated_at = @last_updated_at WHERE item_name = @item_name";
                            using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@quantity", ingredientDto.quantity);
                                updateCommand.Parameters.AddWithValue("@price", ingredientDto.price);
                                updateCommand.Parameters.AddWithValue("@item_name", ingredientDto.itemName);
                                updateCommand.Parameters.AddWithValue("@last_updated_by", lastUpdatedBy);
                                updateCommand.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow);

                                updateCommand.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // If the ingredient does not exist, insert a new record
                            string sqlInsert = "INSERT INTO Item(item_name, quantity, price, status, type, createdAt, last_updated_by, last_updated_at, measurements) VALUES(@item_name, @quantity, @price, @status, @type, @createdAt, @last_updated_by, @last_updated_at, @measurements)";
                            using (var insertCommand = new MySqlCommand(sqlInsert, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@item_name", ingredientDto.itemName);
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

                    string sql = "SELECT * FROM Item"; // Adjust SQL query as per your database schema

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                IngriDTP ingredientDto = new IngriDTP
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    itemName = reader["item_name"].ToString(),
                                    quantity = Convert.ToInt32(reader["quantity"]),
                                    price = Convert.ToDecimal(reader["price"]),
                                    status = reader["status"].ToString(),
                                    type = reader["type"].ToString(),
                                    CreatedAt = Convert.ToDateTime(reader["createdAt"]),
                                    lastUpdatedBy = reader["last_updated_by"] != DBNull.Value ? (byte[])reader["last_updated_by"] : null,
                                    lastUpdatedAt = reader["last_updated_at"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_at"]) : DateTime.MinValue,
                                    measurements = reader["measurements"].ToString(),
                                    isActive = Convert.ToInt32(reader["quantity"]) > 0 // Determine isActive based on quantity
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
                    itemName = ingredient.itemName,
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

                string sql = "SELECT * FROM Item WHERE isActive = @isActive";
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
                                itemName = reader["item_name"].ToString(),
                                quantity = Convert.ToInt32(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["createdAt"]),
                                lastUpdatedBy = reader["last_updated_by"] != DBNull.Value ? (byte[])reader["last_updated_by"] : null,
                                lastUpdatedAt = reader["last_updated_at"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_at"]) : DateTime.MinValue,
                                measurements = reader["measurements"].ToString(),
                                isActive = Convert.ToBoolean(reader["isActive"])
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

                    string sql = "SELECT * FROM Item WHERE id = @id";
                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@id", id);

                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var ingredient = new
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    itemName = reader["item_name"].ToString(),
                                    Quantity = Convert.ToInt32(reader["quantity"]),
                                    Price = Convert.ToDecimal(reader["price"]),
                                    Status = reader["status"].ToString(),
                                    CreatedAt = Convert.ToDateTime(reader["createdAt"]),
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

        [HttpGet("byname/{name}")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult GetIngredientByName(string name)
        {
            try
            {
                List<Ingri> ingredients = GetIngredientsFromDatabaseByName(name);

                if (ingredients == null || !ingredients.Any())
                    return NotFound();

                // Map Ingri entities to IngriDTP DTOs
                var ingredientsDto = ingredients.Select(ingredient => new IngriDTP
                {
                    itemName = ingredient.itemName,
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


        [HttpGet("bystatus/{status}")]
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
                    itemName = ingredient.itemName,
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


        [HttpGet("bytype/{type}")]
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
                    itemName = ingredient.itemName,
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
        public IActionResult UpdateIngredient(int id, [FromBody] IngriDTO updatedIngredient)
        {
            try
            {
                // Retrieve the existing ingredient from the database
                Ingri existingIngredient = GetIngredientFromDatabase(id);

                // Extract the username from the token
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("User is not authorized");
                }

                if (existingIngredient == null)
                {
                    return NotFound();
                }

                // Map properties from IngriDTO to Ingri
                if (!string.IsNullOrEmpty(updatedIngredient.itemName))
                {
                    existingIngredient.itemName = updatedIngredient.itemName;
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
                existingIngredient.lastUpdatedBy = FetchUserId(username);
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

        [HttpDelete]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult DeleteIngredient([FromQuery] int id)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    connection.Open();

                    // Check if the ingredient exists
                    string sqlCheck = "SELECT COUNT(*) FROM Item WHERE id = @id";
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
                    string sqlUpdate = "UPDATE Item SET isActive = @isActive WHERE id = @id";
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

        [HttpPut("restore{id}")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult ReactivateIngredient(int id)
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

                    // Reactivate the ingredient by setting isActive to true
                    string sqlUpdate = "UPDATE Item SET isActive = @isActive WHERE Id = @id";
                    using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                    {
                        updateCommand.Parameters.AddWithValue("@id", id);
                        updateCommand.Parameters.AddWithValue("@isActive", true); // Reactivate the ingredient
                        updateCommand.ExecuteNonQuery();
                    }
                }

                return Ok($"Ingredient with ID {id} has been successfully reactivated.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while reactivating ingredient with ID {id}.");
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
                    itemName = ingredient.itemName,
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

                    string sql = "SELECT * FROM Item WHERE isActive = @isActive";
                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@isActive", false);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Ingri ingredient = new Ingri
                                {
                                    Id = Convert.ToInt32(reader["id"]),
                                    itemName = reader["item_name"].ToString(),
                                    quantity = Convert.ToInt32(reader["quantity"]),
                                    price = Convert.ToDecimal(reader["price"]),
                                    status = reader["status"].ToString(),
                                    type = reader["type"].ToString(),
                                    CreatedAt = Convert.ToDateTime(reader["createdAt"]),
                                    lastUpdatedBy = reader["last_updated_by"] != DBNull.Value ? (byte[])reader["last_updated_by"] : null,
                                    lastUpdatedAt = reader["last_updated_at"] != DBNull.Value ? Convert.ToDateTime(reader["last_updated_at"]) : DateTime.MinValue,
                                    measurements = reader["measurements"].ToString(),
                                    isActive = Convert.ToBoolean(reader["isActive"]) // Ensure this matches your database type
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


        private byte[] FetchUserId(string username)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    connection.Open();

                    string sql = "SELECT UserId FROM users WHERE Username = @username";
                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@username", username);

                        var result = command.ExecuteScalar();
                        if (result != null && result is byte[])
                        {
                            return (byte[])result;
                        }
                        else
                        {
                            throw new Exception("User not found or invalid user ID.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching UserId for username: {username}");
                throw; // Re-throw the exception for the caller to handle
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
                                Id = Convert.ToInt32(reader["id"]),
                                itemName = reader["item_name"].ToString(),
                                quantity = Convert.ToInt32(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["createdAt"]),
                                lastUpdatedBy = reader["last_updated_by"] != DBNull.Value ? (byte[])reader["last_updated_by"] : null,
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
                                Id = Convert.ToInt32(reader["id"]),
                                itemName = reader["item_name"].ToString(),
                                quantity = Convert.ToInt32(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["createdAt"]),
                                lastUpdatedBy = reader["last_updated_by"] != DBNull.Value ? (byte[])reader["last_updated_by"] : null,
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
                                Id = Convert.ToInt32(reader["id"]),
                                itemName = reader["item_name"].ToString(),
                                quantity = Convert.ToInt32(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["createdAt"]),
                                lastUpdatedBy = reader["last_updated_by"] != DBNull.Value ? (byte[])reader["last_updated_by"] : null,
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
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                itemName = reader["item_name"].ToString(),
                                quantity = Convert.ToInt32(reader["quantity"]),
                                price = Convert.ToDecimal(reader["price"]),
                                status = reader["status"].ToString(),
                                type = reader["type"].ToString(),
                                CreatedAt = Convert.ToDateTime(reader["createdAt"]),
                                lastUpdatedBy = reader["last_updated_by"] != DBNull.Value ? (byte[])reader["last_updated_by"] : null,
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

                string sql = "UPDATE Item SET item_name = @item_name, quantity = @quantity, price = @price, status = @status, type = @type, measurements = @measurements, last_updated_by = @last_updated_by, last_updated_at = @last_updated_at, isActive = @isActive WHERE id = @id";
                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@id", ingredient.Id);
                    command.Parameters.AddWithValue("@item_name", ingredient.itemName);
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

