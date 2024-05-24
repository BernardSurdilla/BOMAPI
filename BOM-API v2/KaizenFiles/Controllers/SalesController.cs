using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using BOM_API_v2.KaizenFiles.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using JWTAuthentication.Authentication;

namespace BOM_API_v2.KaizenFiles.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SalesController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<SalesController> _logger;

        public SalesController(IConfiguration configuration, ILogger<SalesController> logger)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;
        }

        [HttpGet]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetAllSales()
        {
            try
            {
                List<Sales> salesList = new List<Sales>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "SELECT Name, Contact, Email, Cost, Total, Date FROM sales";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Sales sale = new Sales
                                {
                                    Name = reader["Name"].ToString(),
                                    Number = Convert.ToInt32(reader["Contact"]),
                                    Email = reader["Email"].ToString(),
                                    Cost = Convert.ToDouble(reader["Cost"]),
                                    Total = Convert.ToInt32(reader["Total"]),
                                    Date = Convert.ToDateTime(reader["Date"])
                                };
                                salesList.Add(sale);
                            }
                        }
                    }
                }

                return Ok(salesList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching sales data.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching sales data.");
            }
        }
    }
}
