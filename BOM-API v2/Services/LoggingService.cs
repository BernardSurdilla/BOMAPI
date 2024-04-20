﻿using BillOfMaterialsAPI.Helpers;

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

            try { TransactionLogs logs = await _logs.TransactionLogs.OrderByDescending(x => x.log_id).FirstAsync(); lastLogId = logs.log_id; }
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

            try { TransactionLogs logs = await _logs.TransactionLogs.OrderByDescending(x => x.log_id).FirstAsync(); lastLogId = logs.log_id; }
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
    [PrimaryKey("log_id")]
    public class TransactionLogs
    {
        [Required][Key][MaxLength(25)] public string log_id { get; set; }
        [Required] public string account_id { get; set; }
        [Required] public string account_name { get; set; }
        [EmailAddress][Required] public string account_email { get; set; }
        [Required][MaxLength(100)] public string transaction_type { get; set; }
        [Required] public DateTime date { get; set; }

        public TransactionLogs(string log_id, string account_id, string account_name, string account_email, string transaction_type, DateTime date)
        {
            this.log_id = log_id;
            this.account_id = account_id;
            this.account_name = account_name;
            this.account_email = account_email;
            this.transaction_type = transaction_type;
            this.date = date;
        }
    }
}
