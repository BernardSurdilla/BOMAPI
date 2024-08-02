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
    public class Add_onsController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<Add_onsController> _logger;

        public Add_onsController(IConfiguration configuration, ILogger<Add_onsController> logger)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;
        }

        [HttpPost("add-ons-table")]
        public async Task<IActionResult> AddAddOn([FromBody] Models.Adds.AddOnDetails addOnDetails)
        {
            try
            {
                // Create AddOns object for database insertion
                var addOns = new AddOns
                {
                    name = addOnDetails.name,
                    pricePerUnit = addOnDetails.pricePerUnit,
                    quantity = addOnDetails.quantity,
                    size = addOnDetails.size,
                    DateAdded = DateTime.UtcNow,  // Current UTC time as DateAdded
                    LastModifiedDate = null,      // Initial value for LastModifiedDate
                    IsActive = true               // Set IsActive to true initially
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
                string sql = @"INSERT INTO AddOns (name, price, quantity, size, measurement, ingredient_type, date_added, last_modified_date, IsActive)
                VALUES (@Name, @PricePerUnit, @Quantity, @Size, @Measure, @IngredientType, @DateAdded, @LastModifiedDate, @IsActive);
                SELECT LAST_INSERT_ID();";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@Name", addOns.name);
                    command.Parameters.AddWithValue("@PricePerUnit", addOns.pricePerUnit);
                    command.Parameters.AddWithValue("@Quantity", addOns.quantity);
                    command.Parameters.AddWithValue("@Size", addOns.size);
                    command.Parameters.AddWithValue("@Measure", "piece");
                    command.Parameters.AddWithValue("@IngredientType", "element");
                    command.Parameters.AddWithValue("@DateAdded", addOns.DateAdded);
                    command.Parameters.AddWithValue("@LastModifiedDate", addOns.LastModifiedDate ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue("@IsActive", addOns.IsActive);

                    // Execute scalar to get the inserted ID
                    int newAddOnsId = Convert.ToInt32(await command.ExecuteScalarAsync());
                    return newAddOnsId;
                }
            }
        }

        [HttpGet("add-ons-table")]
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

                string sql = "SELECT name, price, addOnsId, measurement, size, quantity, date_added, last_modified_date, isActive FROM addons";

                using (var command = new MySqlCommand(sql, connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var addOnDSOS = new AddOnDS2
                            {
                                AddOnName = reader.GetString("name"),
                                PricePerUnit = reader.GetDouble("price"),
                                addOnsId = reader.GetInt32("addOnsId"),
                                Measurement = reader.IsDBNull(reader.GetOrdinal("measurement")) ? null : reader.GetString("measurement"),
                                Quantity = reader.GetInt32("quantity"),
                                // DateAdded is assumed to be non-nullable and should be directly read
                                DateAdded = reader.GetDateTime(reader.GetOrdinal("date_added")),
                                // Handle LastModifiedDate as nullable
                                LastModifiedDate = reader.IsDBNull(reader.GetOrdinal("last_modified_date"))
                                    ? (DateTime?)null
                                    : reader.GetDateTime(reader.GetOrdinal("last_modified_date")),
                                IsActive = reader.GetBoolean("isActive")
                            };

                            addOnDSOSList.Add(addOnDSOS);
                        }
                    }
                }
            }

            return addOnDSOSList;
        }

    }
}
