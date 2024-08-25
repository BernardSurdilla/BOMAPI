using Google.Protobuf;
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
            List<string?> uniqueIds = allMessages.SelectMany(x => new[] {x.sender_account_id, x.receiver_account_id}).Distinct().ToList();
            uniqueIds.Remove(currentUser.Id.ToString());
            uniqueIds.RemoveAll(x => x == null);

            foreach (string? id in uniqueIds)
            {
                APIUsers? currentIdUser = await _userManager.FindByIdAsync(id);
                if (currentUser == null) { continue; }

                List<DirectMessages> messagesForCurrentUser = allMessages.Where(x => x.sender_account_id == id || x.receiver_account_id == id).OrderBy(x => x.date_sent).ToList();
                GetMessageResponseFormat newResponseEntry = new GetMessageResponseFormat
                {
                    account_id = currentIdUser.Id,
                    account_name = currentIdUser.UserName,
                    messages = new List<GetMessageFormat>()
                };

                foreach (DirectMessages currentMessage in messagesForCurrentUser)
                {
                    newResponseEntry.messages.Add(new GetMessageFormat
                    {
                        sender_account_id = currentMessage.sender_account_id,
                        sender_account_name = currentMessage.sender_account_id == currentUser.Id ? currentUser.UserName : currentIdUser.UserName,
                        receiver_account_id = currentMessage.receiver_account_id,
                        reciever_account_name = currentMessage.receiver_account_id == currentUser.Id ? currentUser.UserName : currentIdUser.UserName,
                        message = currentMessage.message,
                        time_sent = currentMessage.date_sent,
                    });
                }

                response.Add(newResponseEntry);
            }

            return response;
        }

    }
    public class GetMessageResponseFormat
    {
        public string? account_id {  get; set; }
        public string? account_name { get; set; }
        public List<GetMessageFormat> messages { get; set; }
    }
    public class GetMessageFormat
    {
        public string? sender_account_id { get; set; }
        public string? sender_account_name { get; set; }
        public string? receiver_account_id { get; set; }
        public string? reciever_account_name { get; set; }
        public string message { get; set; }
        public DateTime time_sent { get; set; }
    }
}
