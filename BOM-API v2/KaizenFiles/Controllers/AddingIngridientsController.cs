using CRUDFI.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
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

        public AddingIngridientsController(IConfiguration configuration, ILogger<AddingIngridientsController> logger)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Roles = UserRoles.Admin)]
        public IActionResult CreateIngredient([FromBody] Ingri ingredient)
        {
            try
            {
                ingredient.itemName = ingredient.itemName.ToLower();
                byte[] lastUpdatedBy = FetchUserId(ingredient.username);

                using (var connection = new MySqlConnection(connectionstring))
                {
                    connection.Open();

                    string sqlCheck = "SELECT COUNT(*) FROM Item WHERE LOWER(item_name) = @item_name";
                    using (var checkCommand = new MySqlCommand(sqlCheck, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@item_name", ingredient.itemName);
                        int ingredientCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (ingredientCount > 0)
                        {
                            string sqlUpdate = "UPDATE Item SET quantity = quantity + @quantity, price = @price, last_updated_by = @last_updated_by, last_updated_at = @last_updated_at WHERE LOWER(item_name) = @item_name";
                            using (var updateCommand = new MySqlCommand(sqlUpdate, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@quantity", ingredient.quantity);
                                updateCommand.Parameters.AddWithValue("@price", ingredient.price);
                                updateCommand.Parameters.AddWithValue("@item_name", ingredient.itemName);
                                updateCommand.Parameters.AddWithValue("@last_updated_by", lastUpdatedBy);
                                updateCommand.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow);

                                updateCommand.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            string sqlInsert = "INSERT INTO Item(item_name, quantity, price, status, type, createdAt, last_updated_by, last_updated_at, measurements) VALUES(@item_name, @quantity, @price, @status, @type, @createdAt, @last_updated_by, @last_updated_at, @measurements)";
                            using (var insertCommand = new MySqlCommand(sqlInsert, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@item_name", ingredient.itemName);
                                insertCommand.Parameters.AddWithValue("@quantity", ingredient.quantity);
                                insertCommand.Parameters.AddWithValue("@price", ingredient.price);
                                insertCommand.Parameters.AddWithValue("@status", ingredient.status);
                                insertCommand.Parameters.AddWithValue("@type", ingredient.type);
                                insertCommand.Parameters.AddWithValue("@createdAt", ingredient.CreatedAt);
                                insertCommand.Parameters.AddWithValue("@last_updated_by", lastUpdatedBy);
                                insertCommand.Parameters.AddWithValue("@last_updated_at", DateTime.UtcNow);
                                insertCommand.Parameters.AddWithValue("@measurements", ingredient.measurements);

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


        [HttpGet("all")]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public IActionResult GetAllIngredients()
        {
            try
            {
                IEnumerable<Ingri> ingredients = GetAllIngredientsFromDatabase();

                if (ingredients == null || !ingredients.Any())
                    return NotFound();

                return Ok(ingredients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching all ingredients");
                return StatusCode(500, "An error occurred while processing the request");
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
                List<Ingri> ingredient = GetIngredientsFromDatabaseByName(name);

                if (ingredient == null)
                    return NotFound();

                return Ok(ingredient);
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
                List<Ingri> ingredient = GetIngredientsFromDatabaseByStatus(status);

                if (ingredient == null)
                    return NotFound();

                return Ok(ingredient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching ingredient by status");
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

                if (ingredients.Count == 0)
                    return NotFound();

                return Ok(ingredients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching ingredients by type");
                return StatusCode(500, "An error occurred while processing the request");
            }
        }

        [HttpPatch("{id}")]
        [Authorize(Roles = UserRoles.Admin)]
        public IActionResult UpdateIngredient(int id, [FromBody] Ingri updatedIngredient, string username)
        {
            try
            {
                Ingri existingIngredient = GetIngredientFromDatabase(id);

                if (existingIngredient == null)
                    return NotFound();

                if (!string.IsNullOrEmpty(updatedIngredient.itemName))
                    existingIngredient.itemName = updatedIngredient.itemName;
                if (updatedIngredient.quantity > 0)
                {
                    existingIngredient.quantity = updatedIngredient.quantity;
                    existingIngredient.isActive = true;
                }
                else
                    existingIngredient.isActive = false;
                if (updatedIngredient.price > 0)
                    existingIngredient.price = updatedIngredient.price;
                if (!string.IsNullOrEmpty(updatedIngredient.status))
                    existingIngredient.status = updatedIngredient.status;
                if (!string.IsNullOrEmpty(updatedIngredient.type))
                    existingIngredient.type = updatedIngredient.type;

                existingIngredient.lastUpdatedBy = FetchUserId(username);
                existingIngredient.lastUpdatedAt = DateTime.UtcNow;

                UpdateIngredientInDatabase(existingIngredient);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating the ingredient");
                return StatusCode(500, "An error occurred while processing the request");
            }
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



        private List<Ingri> GetAllIngredientsFromDatabase()
        {
            List<Ingri> ingredients = new List<Ingri>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                connection.Open();

                string sql = "SELECT * FROM Item";
                using (var command = new MySqlCommand(sql, connection))
                {
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
                                isActive = Convert.ToInt32(reader["quantity"]) > 0 // Determine isActive based on quantity
                            };

                            ingredients.Add(ingredient);
                        }
                    }
                }
            }

            return ingredients;
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

