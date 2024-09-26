using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LiveChat
{
    public class DirectMessagesController : ControllerBase

    {
        private readonly UserManager<APIUsers> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private readonly DirectMessagesDB _directMessagesDB;
        public DirectMessagesController(DirectMessagesDB directMessagesDB, UserManager<APIUsers> userManager, RoleManager<IdentityRole> roleManager)
        {
            _directMessagesDB = directMessagesDB;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [Authorize]
        [HttpGet("user/direct-messages")]
        public async Task<List<GetMessageResponseFormat>> GetMessagesForCurrentUser()
        {
            List<GetMessageResponseFormat> response = new List<GetMessageResponseFormat>();
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) { return response; }

            APIUsers? currentUser = await _userManager.FindByIdAsync(userId);
            if (currentUser == null) { return response; }

            List<DirectMessages> allMessages = await _directMessagesDB.DirectMessages.Where(x => x.sender_account_id == currentUser.Id.ToString() || x.receiver_account_id == currentUser.Id.ToString()).ToListAsync();

            //Select all unique ids
            List<string?> uniqueIds = allMessages.SelectMany(x => new[] { x.sender_account_id, x.receiver_account_id }).Distinct().ToList();
            uniqueIds.Remove(currentUser.Id.ToString());
            uniqueIds.RemoveAll(x => x == null);

            foreach (string? id in uniqueIds)
            {
                APIUsers? currentIdUser = await _userManager.FindByIdAsync(id);
                if (currentUser == null) { continue; }

                List<DirectMessages> messagesForCurrentUser = allMessages.Where(x => x.sender_account_id == id || x.receiver_account_id == id).OrderBy(x => x.date_sent).ToList();
                GetMessageResponseFormat newResponseEntry = new GetMessageResponseFormat
                {
                    accountId = currentIdUser.Id,
                    accountName = currentIdUser.UserName,
                    messages = new List<GetMessageFormat>()
                };

                foreach (DirectMessages currentMessage in messagesForCurrentUser)
                {
                    newResponseEntry.messages.Add(new GetMessageFormat
                    {
                        senderAccountId = currentMessage.sender_account_id,
                        senderAccountName = currentMessage.sender_account_id == currentUser.Id ? currentUser.UserName : currentIdUser.UserName,
                        receiverAccountId = currentMessage.receiver_account_id,
                        recieverAccountName = currentMessage.receiver_account_id == currentUser.Id ? currentUser.UserName : currentIdUser.UserName,
                        message = currentMessage.message,
                        timeSent = currentMessage.date_sent,
                    });
                }

                response.Add(newResponseEntry);
            }

            return response;
        }

    }
    public class GetMessageResponseFormat
    {
        public string? accountId { get; set; }
        public string? accountName { get; set; }
        public List<GetMessageFormat> messages { get; set; }
    }
    public class GetMessageFormat
    {
        public string? senderAccountId { get; set; }
        public string? senderAccountName { get; set; }
        public string? receiverAccountId { get; set; }
        public string? recieverAccountName { get; set; }
        public string message { get; set; }
        public DateTime timeSent { get; set; }
    }
}
