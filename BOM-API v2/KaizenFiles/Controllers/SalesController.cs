using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using BOM_API_v2.KaizenFiles.Models;
using Microsoft.Extensions.Logging;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace BOM_API_v2.KaizenFiles.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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

                    string sql = "SELECT Id, Name, Contact, Email, Cost, Total, Date FROM sales";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                Sales sale = new Sales
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    Name = reader.GetString(reader.GetOrdinal("Name")),
                                    Number = reader.GetInt32(reader.GetOrdinal("Contact")),
                                    Email = reader.GetString(reader.GetOrdinal("Email")),
                                    Cost = reader.GetDouble(reader.GetOrdinal("Cost")),
                                    Total = reader.GetInt32(reader.GetOrdinal("Total")),
                                    Date = reader.GetDateTime(reader.GetOrdinal("Date"))
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

        [HttpGet("totals")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetSalesTotal()
        {
            try
            {
                Totals totalSum = new Totals();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "SELECT SUM(Total) AS TotalSum FROM sales";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        var result = await command.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            totalSum.Total = Convert.ToInt32(result);
                        }
                    }
                }

                return Ok(totalSum);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching the total sales.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching the total sales.");
            }
        }


        [HttpGet("top-sales")]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager)]
        public async Task<IActionResult> GetTopSales()
        {
            try
            {
                List<SalesSum> topSalesList = new List<SalesSum>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "SELECT Name, Total FROM sales ORDER BY Total DESC LIMIT 5";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                SalesSum salesSummary = new SalesSum
                                {
                                    Name = reader.GetString(reader.GetOrdinal("Name")),
                                    Total = reader.GetInt32(reader.GetOrdinal("Total"))
                                };
                                topSalesList.Add(salesSummary);
                            }
                        }
                    }
                }

                return Ok(topSalesList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching top sales data.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching top sales data.");
            }
        }


    }
}
