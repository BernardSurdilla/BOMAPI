﻿using BOM_API_v2.KaizenFiles.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Data;

namespace BOM_API_v2.KaizenFiles.Controllers
{
    [Route("sales")]
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
        [ProducesResponseType(typeof(Sales), StatusCodes.Status200OK)]
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
                                    id = reader.GetInt32(reader.GetOrdinal("Id")),
                                    name = reader.GetString(reader.GetOrdinal("Name")),
                                    number = reader.GetString(reader.GetOrdinal("Contact")),
                                    email = reader.GetString(reader.GetOrdinal("Email")),
                                    price = reader.GetDouble(reader.GetOrdinal("Cost")),
                                    total = reader.GetInt32(reader.GetOrdinal("Total")),
                                    date = reader.GetDateTime(reader.GetOrdinal("Date"))
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
        [ProducesResponseType(typeof(Totals), StatusCodes.Status200OK)]
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
                            totalSum.total = Convert.ToInt32(result);
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
        [ProducesResponseType(typeof(SalesSum), StatusCodes.Status200OK)]
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
                                    name = reader.GetString(reader.GetOrdinal("Name")),
                                    total = reader.GetInt32(reader.GetOrdinal("Total"))
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

        [HttpGet("total/day")]
        [ProducesResponseType(typeof(SalesResponse), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetTotalSalesForDay([FromQuery] int year, [FromQuery] int month, [FromQuery] int day)
        {
            try
            {
                // Create a DateTime object from the provided query parameters
                DateTime specificDay = new DateTime(year, month, day);

                // Call the method to get total sales for the specific day
                decimal totalSales = await GetTotalSalesForSpecificDay(specificDay);

                // Create the response object
                var response = new SalesResponse
                {
                    day = specificDay.ToString("dddd"), // Get the full name of the day (e.g., Monday)
                    totalSales = totalSales
                };

                // If total is 0, still return success with 0 total
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total sales for the day.");
                return StatusCode(500, "Internal server error.");
            }
        }


        // Private method for fetching total sales on a specific day
        private async Task<decimal> GetTotalSalesForSpecificDay(DateTime specificDay)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT SUM(Total) 
            FROM sales 
            WHERE DAY(Date) = @day AND MONTH(Date) = @month AND YEAR(Date) = @year";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Use the DateTime properties for the day, month, and year
                    command.Parameters.AddWithValue("@day", specificDay.Day);
                    command.Parameters.AddWithValue("@month", specificDay.Month);
                    command.Parameters.AddWithValue("@year", specificDay.Year);

                    object result = await command.ExecuteScalarAsync();
                    return result != DBNull.Value ? Convert.ToDecimal(result) : 0m; // Return 0 if result is DBNull
                }
            }
        }

        [HttpGet("total/week")]
        [ProducesResponseType(typeof(SalesResponse), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetTotalSalesForWeek([FromQuery] int year, [FromQuery] int month, [FromQuery] int day)
        {
            try
            {
                // Create a DateTime object from the provided query parameters for the start of the week
                DateTime startOfWeek = new DateTime(year, month, day).StartOfWeek(DayOfWeek.Monday);

                // Fetch total sales for the specific week
                var weekSales = await GetTotalSalesForSpecificWeek(startOfWeek);

                // If no data found, return an empty array
                if (weekSales.Count == 0)
                {
                    return Ok(new List<SalesResponse>()); // Return an empty array of TotalSalesResponse
                }

                // Map the weekSales data to the TotalSalesResponse class
                var response = weekSales.Select(s => new SalesResponse
                {
                    day = s.Key, // Assuming s.Key is a string representing the day
                    totalSales = s.Value // Assuming s.Value is a decimal representing total sales
                }).ToList();

                // Return result in the desired format
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total sales for the week.");
                return StatusCode(500, "Internal server error.");
            }
        }


        // Private method for fetching sales totals for a specific week
        private async Task<Dictionary<string, decimal>> GetTotalSalesForSpecificWeek(DateTime startOfWeek)
        {
            var sales = new Dictionary<string, decimal>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT DAYNAME(Date) AS DayName, SUM(Total) AS TotalSales
            FROM sales 
            WHERE Date >= @startOfWeek AND Date < @endOfWeek
            GROUP BY DAYNAME(Date)
            ORDER BY FIELD(DAYNAME(Date), 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday')";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Set the start and end of the week for the SQL query
                    command.Parameters.AddWithValue("@startOfWeek", startOfWeek);
                    command.Parameters.AddWithValue("@endOfWeek", startOfWeek.AddDays(7));

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Add day names and corresponding total sales to the dictionary
                            sales[reader.GetString("DayName")] = reader.GetDecimal("TotalSales");
                        }
                    }
                }
            }

            return sales;
        }

        [HttpGet("total/month")]
        [ProducesResponseType(typeof(MonthSalesResponse), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
public async Task<IActionResult> GetTotalSalesForMonth([FromQuery] int year, [FromQuery] int month)
{
    try
    {
        // Fetch total sales for the specific month
        var dailySales = await GetTotalSalesForSpecificMonth(year, month);

        // If no data found, return an empty array
        if (dailySales.Count == 0)
        {
            return Ok(new List<MonthSalesResponse>()); // Return an empty array of TotalSalesResponse
        }

        // Map the dailySales data to the TotalSalesResponse class
        var response = dailySales.Select(s => new MonthSalesResponse
        {
            day = s.Key, // Assuming s.Key is a string representing the day number
            totalSales = s.Value // Assuming s.Value is a decimal representing total sales
        }).ToList();

        // Return result in the desired format
        return Ok(response);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error retrieving total sales for the month.");
        return StatusCode(500, "Internal server error.");
    }
}


        // Private method to get total sales for a specific month
        private async Task<Dictionary<int, decimal>> GetTotalSalesForSpecificMonth(int year, int month)
        {
            var dailySales = new Dictionary<int, decimal>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT DAY(Date) AS Day, SUM(Total) AS TotalSales
            FROM sales 
            WHERE MONTH(Date) = @month AND YEAR(Date) = @year
            GROUP BY DAY(Date)
            ORDER BY DAY(Date)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Set the month and year parameters for the SQL query
                    command.Parameters.AddWithValue("@month", month);
                    command.Parameters.AddWithValue("@year", year);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Add day and total sales to the dictionary
                            dailySales[reader.GetInt32("Day")] = reader.GetDecimal("TotalSales");
                        }
                    }
                }
            }

            return dailySales;
        }

        [HttpGet("total/year")]
        [ProducesResponseType(typeof(YearSalesResponse), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Manager + "," + UserRoles.Admin)]
        public async Task<IActionResult> GetTotalSalesForYear([FromQuery] int year)
        {
            try
            {
                // Fetch total sales for the specific year
                var yearlySales = await GetTotalSalesForSpecificYear(year);

                // If no data found, return an empty array
                if (yearlySales.Count == 0)
                {
                    return Ok(new List<YearSalesResponse>()); // Return an empty array of TotalSalesResponse
                }

                // Map the yearlySales data to the TotalSalesResponse class
                var response = yearlySales.Select(s => new YearSalesResponse
                {
                    month = s.Key, // Assuming s.Key is a string representing the month name
                    totalSales = s.Value // Assuming s.Value is a decimal representing total sales
                }).ToList();

                // Return result in the desired format
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total sales for the year.");
                return StatusCode(500, "Internal server error.");
            }
        }


        // Private method to get total sales for a specific year
        private async Task<Dictionary<string, decimal>> GetTotalSalesForSpecificYear(int year)
        {
            var sales = new Dictionary<string, decimal>();

            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = @"
            SELECT MONTHNAME(Date) AS MonthName, SUM(Total) AS TotalSales
            FROM sales 
            WHERE YEAR(Date) = @year
            GROUP BY MONTH(Date)
            ORDER BY MONTH(Date)";

                using (var command = new MySqlCommand(sql, connection))
                {
                    // Set the year parameter for the SQL query
                    command.Parameters.AddWithValue("@year", year);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // Add month name and total sales to the dictionary
                            sales[reader.GetString("MonthName")] = reader.GetDecimal("TotalSales");
                        }
                    }
                }
            }

            return sales;
        }



    }
}
