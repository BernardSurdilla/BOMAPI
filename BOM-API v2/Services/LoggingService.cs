using API_TEST.Controllers;
using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Identity;
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

        public async Task<int> LogAction(ClaimsPrincipal user, string action)
        {

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

            try { TransactionLogs logs = await _logs.TransactionLogs.OrderByDescending(x => x.logId).FirstAsync(); lastLogId = logs.logId; }
            catch
            {
                string newLogId = IdFormat.logsIdFormat;
                for (int i = 1; i <= IdFormat.idNumLength; i++) { newLogId += "0"; }
                lastLogId = newLogId;
            }
            TransactionLogs newLog = new TransactionLogs(
                    IdFormat.IncrementId(IdFormat.logsIdFormat, IdFormat.idNumLength, lastLogId),
                    currentUserData.Id,
                    currentUserData.UserName == null ? "N/A" : currentUserData.UserName,
                    currentUserData.Email == null ? "N/A" : currentUserData.Email,
                    action,
                    DateTime.Now
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

            try { TransactionLogs logs = await _logs.TransactionLogs.OrderByDescending(x => x.logId).FirstAsync(); lastLogId = logs.logId; }
            catch
            {
                string newLogId = IdFormat.logsIdFormat;
                for (int i = 1; i <= IdFormat.idNumLength; i++) { newLogId += "0"; }
                lastLogId = newLogId;
            }
            TransactionLogs newLog = new TransactionLogs(
                    IdFormat.IncrementId(IdFormat.logsIdFormat, IdFormat.idNumLength, lastLogId),
                    userId,
                    userName == null ? "N/A" : userName,
                    email == null ? "N/A" : email,
                    "LOGIN",
                    DateTime.Now
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
    [PrimaryKey("logId")]
    public class TransactionLogs
    {
        [Required][Key][MaxLength(25)] public string logId { get; set; }
        [Required] public string accountId { get; set; }
        [Required] public string accountName { get; set; }
        [Required] public string accountEmail { get; set; }
        [Required][MaxLength(100)] public string transactionType { get; set; }
        [Required] public DateTime date { get; set; }

        public TransactionLogs(string logId, string accountId, string accountName, string accountEmail, string transactionType, DateTime date)
        {
            this.logId = logId;
            this.accountId = accountId;
            this.accountName = accountName;
            this.accountEmail = accountEmail;
            this.transactionType = transactionType;
            this.date = date;
        }
    }
}
