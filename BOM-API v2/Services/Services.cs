using BillOfMaterialsAPI.Models;
using System.Security.Claims;
using API_TEST.Controllers;
using BillOfMaterialsAPI.Schemas;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace BillOfMaterialsAPI.Services
{
    public class AccountManager : IActionLogger
    {
        private readonly LoggingDatabaseContext _logs;
        private readonly UserManager<Users> _users;

        public AccountManager(LoggingDatabaseContext logs, UserManager<Users> users) {  _logs = logs; _users = users; }

        public async Task<int> LogAction(ClaimsPrincipal user, string action)
        {
            if (user == null) { return 0; }
            if (user.Identity == null) { return 0; }
            if (user.Identity.Name == null) { return 0; }

            Users? currentUserData = await _users.GetUserAsync(user);
            if (currentUserData == null) { return 0; }
            if (currentUserData.UserName == null) { return 0; }
            if (currentUserData.Email == null) { return 0; }

            string userName = user.Identity.Name;

            
            string lastLogId;

            try { List<TransactionLogs> logs = await _logs.TransactionLogs.ToListAsync(); lastLogId = logs.Last().logId; }
            catch
            {
                string newLogId = IdFormat.logsIdFormat;
                for (int i = 1; i <= IdFormat.idNumLength; i++) { newLogId += "0"; }
                lastLogId = newLogId;
            }
            TransactionLogs newLog = new TransactionLogs(
                    IdFormat.IncrementId(IdFormat.logsIdFormat, IdFormat.idNumLength, lastLogId),
                    currentUserData.Id,
                    currentUserData.UserName,
                    currentUserData.Email,
                    action,
                    DateTime.Now
                    );

            _logs.TransactionLogs.Add(newLog);
            return await _logs.SaveChangesAsync();
        }
    }
}
