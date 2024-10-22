using BillOfMaterialsAPI.Helpers;// Adjust the namespace according to your project structure
using BillOfMaterialsAPI.Models;
using BillOfMaterialsAPI.Schemas;
using BOM_API_v2.KaizenFiles.Models;
using CRUDFI.Models;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using static BOM_API_v2.KaizenFiles.Models.Transactions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace BOM_API_v2.KaizenFiles.Controllers
{
    [Route("transactions")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly string connectionstring;
        private readonly ILogger<TransactionsController> _logger;

        public TransactionsController(IConfiguration configuration, ILogger<TransactionsController> logger, DatabaseContext context, KaizenTables kaizenTables)
        {
            connectionstring = configuration["ConnectionStrings:connection"] ?? throw new ArgumentNullException("connectionStrings is missing in the configuration.");
            _logger = logger;
        }

        [HttpGet("/culo-api/v1/current-user/transaction/history")]
        [ProducesResponseType(typeof(GetTransactions), StatusCodes.Status200OK)]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Manager + "," + UserRoles.Customer)]
        public async Task<IActionResult> GetAllTransactions()
        {
            try
            {
                // Fetch customer username from claims
                var customerUsername = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(customerUsername))
                {
                    return Unauthorized("User is not authorized");
                }

                // Retrieve customerId from username
                string customerId = await GetUserIdByAllUsername(customerUsername);
                if (customerId == null || customerId.Length == 0)
                {
                    return BadRequest("Customer not found.");
                }

                List<GetTransactions> transactionList = new List<GetTransactions>();

                using (var connection = new MySqlConnection(connectionstring))
                {
                    await connection.OpenAsync();

                    string sql = "SELECT Id, order_id, status, total_amount, total_paid, date FROM transactions WHERE user_id = @userId AND status != 'unpaid'";

                    using (var command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@userId", customerId);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                GetTransactions transact = new GetTransactions
                                {
                                    id = reader.GetString(reader.GetOrdinal("id")),
                                    orderId = reader.GetString(reader.GetOrdinal("order_id")),
                                    status = reader.GetString(reader.GetOrdinal("status")),
                                    totalPrice = reader.GetDouble(reader.GetOrdinal("total_amount")),
                                    totalPaid = reader.GetInt32(reader.GetOrdinal("total_paid")),
                                    date = reader.GetDateTime(reader.GetOrdinal("date"))
                                };
                                transactionList.Add(transact);
                            }
                        }
                    }
                }

                return Ok(transactionList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching transaction data.");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while fetching transaction data.");
            }
        }

        private async Task<string> GetUserIdByAllUsername(string username)
        {
            using (var connection = new MySqlConnection(connectionstring))
            {
                await connection.OpenAsync();

                string sql = "SELECT Id FROM aspnetusers WHERE Username = @username AND EmailConfirmed = 1";

                using (var command = new MySqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@username", username);
                    var result = await command.ExecuteScalarAsync();

                    if (result != null && result != DBNull.Value)
                    {
                        // Return the string value directly
                        string userId = (string)result;

                        // Debug.WriteLine to display the value of userId
                        Debug.WriteLine($"UserId for username '{username}': {userId}");

                        return userId;
                    }
                    else
                    {
                        return null; // User not found
                    }
                }
            }
        }


    }
}
