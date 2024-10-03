using BillOfMaterialsAPI.Helpers;
using BOM_API_v2.Services;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace BillOfMaterialsAPI.Services
{
    public class AccountManager : IActionLogger
    {
        private readonly LoggingDatabaseContext _logs;
        private readonly UserManager<APIUsers> _users;

        public AccountManager(LoggingDatabaseContext logs, UserManager<APIUsers> users) { _logs = logs; _users = users; }

        public async Task<int> LogAction(ClaimsPrincipal user, string transaction_type, string transaction_description)
        {
            return 1;
            //
            // NOTE: The UserManager.GetUserAsync() finds the claim with the type 
            // ClaimTypes.NameIdentifier, gets the value in it, and uses it to find the user.
            // E.G. new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            //
            // While issuing tokens, the token needs to have this type of claim, and that claim
            // must contain the user id of the user in order for the
            // UserManager.GetUserAsync() to find the user
            //

            if (!user.HasClaim(x => x.Type == ClaimTypes.NameIdentifier)) { return 0; }

            APIUsers? currentUserData = await _users.GetUserAsync(user);
            if (currentUserData == null) { return 0; }


            string lastLogId;

            try { TransactionLogs logs = await _logs.TransactionLogs.OrderByDescending(x => x.log_id).FirstAsync(); lastLogId = logs.log_id; }
            catch
            {
                string newLogId = IdPrefix.Logs;
                for (int i = 1; i <= IdFormat.IdNumbersLength; i++) { newLogId += "0"; }
                lastLogId = newLogId;
            }
            TransactionLogs newLog = new TransactionLogs(
                    IdFormat.IncrementId(IdPrefix.Logs, IdFormat.IdNumbersLength, lastLogId),
                    currentUserData.Id,
                    currentUserData.UserName == null ? "N/A" : currentUserData.UserName,
                    currentUserData.Email == null ? "N/A" : currentUserData.Email,
                    transaction_type,
                    transaction_description,
                    TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"))
                    );

            _logs.TransactionLogs.Add(newLog);
            return await _logs.SaveChangesAsync();
        }
        public async Task<int> LogUserLogin(IdentityUser user)
        {
            if (user == null) { return 0; }

            string userId = user.Id;
            string? userName = user.UserName;
            string? email = user.Email;

            string lastLogId;

            try { TransactionLogs logs = await _logs.TransactionLogs.OrderByDescending(x => x.log_id).FirstAsync(); lastLogId = logs.log_id; }
            catch
            {
                string newLogId = IdPrefix.Logs;
                for (int i = 1; i <= IdFormat.IdNumbersLength; i++) { newLogId += "0"; }
                lastLogId = newLogId;
            }
            TransactionLogs newLog = new TransactionLogs(
                    IdFormat.IncrementId(IdPrefix.Logs, IdFormat.IdNumbersLength, lastLogId),
                    userId,
                    userName == null ? "N/A" : userName,
                    email == null ? "N/A" : email,
                    "LOGIN",
                    "User " + userName + " logged in.",
                    TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"))
                    );

            _logs.TransactionLogs.Add(newLog);
            return await _logs.SaveChangesAsync();
        }
    }

    //Database Context
    public class LoggingDatabaseContext : DbContext
    {
        public LoggingDatabaseContext(DbContextOptions<LoggingDatabaseContext> options) : base(options) { }
        public DbSet<TransactionLogs> TransactionLogs { get; set; }

    }
    [PrimaryKey("log_id")]
    public class TransactionLogs
    {
        [Required][Key][MaxLength(25)] public string log_id { get; set; }
        [Required] public string account_id { get; set; }
        [Required] public string account_name { get; set; }
        [EmailAddress][Required] public string account_email { get; set; }
        [Required][MaxLength(100)] public string transaction_type { get; set; }
        [Required][MaxLength(100)] public string transaction_description { get; set; }
        [Required] public DateTime date { get; set; }

        public TransactionLogs(string log_id, string account_id, string account_name, string account_email, string transaction_type, string transaction_description, DateTime date)
        {
            this.log_id = log_id;
            this.account_id = account_id;
            this.account_name = account_name;
            this.account_email = account_email;
            this.transaction_type = transaction_type;
            this.transaction_description = transaction_description;
            this.date = date;
        }
    }

    [ApiController]
    [Route("logs/")]
    [Authorize(Roles = UserRoles.Admin)]
    public class LoggingController : ControllerBase
    {
        private readonly LoggingDatabaseContext _logs;
        public LoggingController(LoggingDatabaseContext context) { _logs = context; }

        [HttpGet]
        public async Task<List<TransactionLogs>> GetLogs(int? page, int? record_per_page, string? log_type, string? account_id)
        {
            string[] possibleTypes = ["GET", "POST", "PATCH", "DELETE", "LOGIN"];
            List<TransactionLogs> transactionLogs;

            IQueryable<TransactionLogs> transactionLogsQuery = _logs.TransactionLogs.AsQueryable();

            if (log_type != null)
            {
                if (possibleTypes.Contains(log_type.ToUpper())) { transactionLogsQuery = _logs.TransactionLogs.Where(x => x.transaction_type == log_type); }
                else { return new List<TransactionLogs>(); }
            }

            //Paging algorithm
            if (page == null) { transactionLogsQuery = transactionLogsQuery.OrderByDescending(x => x.date); }
            else
            {
                int record_limit = record_per_page == null || record_per_page.Value < Page.DefaultNumberOfEntriesPerPage ? Page.DefaultNumberOfEntriesPerPage : record_per_page.Value;
                int current_page = page.Value < Page.DefaultStartingPageNumber ? Page.DefaultStartingPageNumber : page.Value;

                int num_of_record_to_skip = (current_page * record_limit) - record_limit;

                transactionLogsQuery = transactionLogsQuery.OrderByDescending(x => x.date).Skip(num_of_record_to_skip).Take(record_limit);
            }

            transactionLogs = await transactionLogsQuery.ToListAsync();
            return transactionLogs;
        }
    }
}
