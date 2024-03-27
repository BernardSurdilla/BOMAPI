using BillOfMaterialsAPI.Models;
using System.Security.Claims;
using API_TEST.Controllers;
using BillOfMaterialsAPI.Schemas;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using JWTAuthentication.Authentication;
using System.Diagnostics;

namespace BillOfMaterialsAPI.Services
{
    public class AccountManager : IActionLogger
    {
        private readonly LoggingDatabaseContext _logs;
        private readonly UserManager<APIUsers> _users;

        public AccountManager(LoggingDatabaseContext logs, UserManager<APIUsers> users) {  _logs = logs; _users = users; }

        public async Task<int> LogAction(ClaimsPrincipal user, string action)
        {
            if (!user.HasClaim(x => x.Type == ClaimTypes.Name)) { return 0; }
            if (!user.HasClaim(x => x.Type == ClaimTypes.Email)) { return 0; }

            //
            // NOTE: The UserManager.GetUserAsync() finds the claim with the type 
            // ClaimTypes.NameIdentifier, gets the value in it, and uses it to find the user.
            // E.G. Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            //
            // While issuing tokens, the token needs to have this type of claim, and
            // must contain the user id of the user in order for the
            // UserManager.GetUserAsync() to find the user
            //

            if (!user.HasClaim(x => x.Type == ClaimTypes.NameIdentifier)) { return 0; }

            string userName = user.Claims.First(x => x.Type == ClaimTypes.Name).Value;
            string email = user.Claims.First(x => x.Type == ClaimTypes.Name).Value;

            APIUsers? currentUserData = await _users.GetUserAsync(user);
            Debug.WriteLine(currentUserData);

            if (currentUserData == null) { return 0; }
            if (currentUserData.UserName == null) { return 0; }
            if (currentUserData.Email == null) { return 0; }

            /*
            if (user == null) { return 0; }
            if (user.Identity == null) { return 0; }
            if (user.Identity.Name == null) { return 0; }
            string userName = user.Identity.Name;
            */

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
