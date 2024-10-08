using BOM_API_v2.KaizenFiles.Models;
using CRUDFI.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;
using static BOM_API_v2.KaizenFiles.Models.Adds;

namespace BOM_API_v2.KaizenFiles.Controllers
{
    [Route("add-ons")]
    [ApiController]
    public class Add_onsController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<Add_onsController> _logger;

        public Add_onsController(IConfiguration configuration, ILogger<Add_onsController> logger)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> AddAddOn([FromBody] Models.Adds.AddOnDetails addOnDetails)
        {
            try
            {
                // Create AddOns object for database insertion
                var addOns = new AddOns
                {
                    name = addOnDetails.name,
                    pricePerUnit = addOnDetails.price,
                    size = addOnDetails.size,
                    dateAdded = DateTime.UtcNow,  // Current UTC time as DateAdded
                    lastModifiedDate = null,      // Initial value for LastModifiedDate
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
                string sql = @"INSERT INTO addons (name, price, size, measurement, ingredient_type, date_added, last_modified_date)
                VALUES (@Name, @PricePerUnit, @Size, @Measure, @IngredientType, @DateAdded, @LastModifiedDate);
                SELECT LAST_INSERT_ID();";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Name", addOns.name);
                    command.Parameters.AddWithValue("@PricePerUnit", addOns.pricePerUnit);
                    command.Parameters.AddWithValue("@Size", addOns.size);
                    command.Parameters.AddWithValue("@Measure", "piece");
                    command.Parameters.AddWithValue("@IngredientType", "element");
                    command.Parameters.AddWithValue("@DateAdded", addOns.dateAdded);
                    command.Parameters.AddWithValue("@LastModifiedDate", addOns.dateAdded);

                    // Execute scalar to get the inserted ID
                    int newAddOnsId = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return newAddOnsId;
                }
            }
        }

        [HttpGet]
        [ProducesResponseType(typeof(AddOnDS2), StatusCodes.Status200OK)]
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

                string sql = "SELECT name, price, add_ons_id, measurement, size, date_added, last_modified_date FROM addons WHERE is_active = 1";

                using (var command = new MySqlCommand(sql, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var addOnDSOS = new AddOnDS2
                            {
                                addOnName = reader.GetString("name"),
                                price = reader.GetDouble("price"),
                                id = reader.GetInt32("add_ons_id"),
                                measurement = reader.IsDBNull(reader.GetOrdinal("measurement")) ? null : reader.GetString("measurement"),
                                size = reader.IsDBNull(reader.GetOrdinal("size")) ? null : reader.GetDouble("size"),
                                // DateAdded is assumed to be non-nullable and should be directly read
                                dateAdded = reader.GetDateTime(reader.GetOrdinal("date_added")),
                                // Handle LastModifiedDate as nullable
                                lastModifiedDate = reader.IsDBNull(reader.GetOrdinal("last_modified_date"))
                                    ? (DateTime?)null
                                    : reader.GetDateTime(reader.GetOrdinal("last_modified_date")),
                            };

                            addOnDSOSList.Add(addOnDSOS);
                        }
                    }
                }
            }

            return addOnDSOSList;
        }

        [HttpPatch("{addOnsId}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> UpdateAddOn(int addOnsId, [FromBody] UpdateAddOnRequest? updateRequest)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    // Prepare to build the SQL statement and parameters
                    var setClauses = new List<string>();
                    var parameters = new List<MySqlParameter>();

                    if (!string.IsNullOrEmpty(updateRequest.name))
                    {
                        setClauses.Add("name = @AddOnName");
                        parameters.Add(new MySqlParameter("@AddOnName", updateRequest.name));
                    }

                    if (updateRequest.price.HasValue)
                    {
                        setClauses.Add("price = @PricePerUnit");
                        parameters.Add(new MySqlParameter("@PricePerUnit", updateRequest.price.Value));
                    }

                    if (!setClauses.Any())
                    {
                        return BadRequest("No fields to update.");
                    }

                    // Create the SQL statement
                    string sql = $@"
                UPDATE addons 
                SET {string.Join(", ", setClauses)}, 
                    last_modified_date = @LastModifiedDate 
                WHERE add_ons_id = @AddOnsId";

                    parameters.Add(new MySqlParameter("@AddOnsId", addOnsId));
                    parameters.Add(new MySqlParameter("@LastModifiedDate", DateTime.UtcNow));

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddRange(parameters.ToArray());

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                        {
                            return NotFound($"Add-On with ID '{addOnsId}' not found.");
                        }

                        return Ok($"Add-On '{addOnsId}' updated successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Add-On in the database.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> DeactivateAddOn(int id)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
                UPDATE addons 
                SET 
                    is_active = 0,
                    last_modified_date = @LastModifiedDate 
                WHERE add_ons_id = @AddOnsId";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@AddOnsId", id);
                        command.Parameters.AddWithValue("@LastModifiedDate", DateTime.UtcNow);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                        {
                            return NotFound($"Add-On with ID '{id}' not found.");
                        }

                        return Ok($"Add-On '{id}' deactivated successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating Add-On in the database.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        [HttpPost("{id}/restore")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> ActivateAddOn(int id)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
                UPDATE addons 
                SET 
                    is_active = 1,
                    last_modified_date = @LastModifiedDate 
                WHERE add_ons_id = @AddOnsId";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@AddOnsId", id);
                        command.Parameters.AddWithValue("@LastModifiedDate", DateTime.UtcNow);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                        {
                            return NotFound($"Add-On with ID '{id}' not found.");
                        }

                        return Ok($"Add-On '{id}' activated successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating Add-On in the database.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        [HttpDelete("deactivate-all")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> DeactivateAllAddOns()
        {
            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
                UPDATE addons 
                SET 
                    is_active = 0,
                    last_modified_date = @LastModifiedDate";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@LastModifiedDate", DateTime.UtcNow);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        return Ok($"{rowsAffected} Add-Ons deactivated successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating all Add-Ons in the database.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }

        [HttpPut("activate-all")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> ActivateAllAddOns()
        {
            try
            {
                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = @"
                UPDATE addons 
                SET 
                    is_active = 1,
                    last_modified_date = @LastModifiedDate";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@LastModifiedDate", DateTime.UtcNow);

                        int rowsAffected = await command.ExecuteNonQueryAsync();

                        return Ok($"{rowsAffected} Add-Ons activated successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating all Add-Ons in the database.");
                return StatusCode(500, "An error occurred while processing the request.");
            }
        }


    }
}
