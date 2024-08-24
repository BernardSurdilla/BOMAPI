using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace LiveChat
{
    public class ChatHub : Hub
    {
        private readonly ILiveChatConnectionManager _connectionManager;
        private const string CLIENT_RECIEVE_MESSAGE_FUNCTION_NAME = "RecieveMessage";

        public ChatHub(ILiveChatConnectionManager connectionManager) { _connectionManager = connectionManager; }

        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
        [HubMethodName("customer-send-message")]
        public async Task CustomerSendMessage(string message, string connectionId)
        {
            ClaimsPrincipal? currentUser = Context.User;
            ConnectionInfo? messageRecipientConnectionInfo = null;

            messageRecipientConnectionInfo = _connectionManager.GetAllAdminConnections().Where(x => x.ConnectionId == connectionId).FirstOrDefault();
            if (messageRecipientConnectionInfo == null) 
            { 
                messageRecipientConnectionInfo = _connectionManager.GetAllManagerConnections().Where(x => x.ConnectionId == connectionId).FirstOrDefault(); 
            }
            if (messageRecipientConnectionInfo == null) { return; }

            MessageFormat formattedMessage = new MessageFormat
            {
                sender_connection_id = connectionId,
                sender_message = message,
                sender_message_time_sent = DateTime.Now
            };

            if (currentUser == null) formattedMessage.sender_name = "Anonymous";
            else 
            {
                Claim? nameClaim = currentUser.FindFirst(ClaimTypes.Name);

                formattedMessage.sender_name = nameClaim == null ? "N/A" : nameClaim.Value;
            }

            await Clients.Caller.SendAsync(CLIENT_RECIEVE_MESSAGE_FUNCTION_NAME, formattedMessage);
            await Clients.Client(messageRecipientConnectionInfo.ConnectionId).SendAsync(CLIENT_RECIEVE_MESSAGE_FUNCTION_NAME, formattedMessage);
        }
        [HubMethodName("admin-send-message")]
        public async Task AdminSendMessage(string message, string connectionId)
        {
            ClaimsPrincipal? currentUser = Context.User;
            if (currentUser == null) return;

            List<Claim> roles = currentUser.FindAll(ClaimTypes.Role).ToList();
            if (roles.Count == 0) return;
            if (roles.Where(x => x.Value == UserRoles.Admin).FirstOrDefault() == null) return;

            ConnectionInfo? messageRecipientConnectionInfo = null;

            messageRecipientConnectionInfo = _connectionManager.GetAllConnections().Where(x => x.ConnectionId == connectionId).FirstOrDefault();

            if (messageRecipientConnectionInfo == null) { return; }

            MessageFormat formattedMessage = new MessageFormat
            {
                sender_connection_id = connectionId,
                sender_message = message,
                sender_message_time_sent = DateTime.Now
            };

            if (currentUser == null) formattedMessage.sender_name = "Anonymous";
            else
            {
                Claim? nameClaim = currentUser.FindFirst(ClaimTypes.Name);

                formattedMessage.sender_name = nameClaim == null ? "N/A" : nameClaim.Value;
            }

            await Clients.Caller.SendAsync(CLIENT_RECIEVE_MESSAGE_FUNCTION_NAME, formattedMessage);
            await Clients.Client(messageRecipientConnectionInfo.ConnectionId).SendAsync(CLIENT_RECIEVE_MESSAGE_FUNCTION_NAME, formattedMessage);
        }
        public override Task OnConnectedAsync()
        {
            ClaimsPrincipal? currentUser = Context.User;

            if (currentUser == null) { _connectionManager.AddConnection(Context.ConnectionId); }
            else {
                List<Claim> allRoles = currentUser.FindAll(ClaimTypes.Role).ToList();
                List<string> allRolesParsed = new List<string>();
                foreach (Claim claim in allRoles) { allRolesParsed.Add(claim.Value); }

                _connectionManager.AddConnection(new ConnectionInfo(
                Context.ConnectionId, 
                currentUser.FindFirstValue(ClaimTypes.NameIdentifier),
                currentUser.FindFirstValue(ClaimTypes.Name), 
                allRolesParsed )); }
            
            return base.OnConnectedAsync();
        }
        public override Task OnDisconnectedAsync(Exception exception)
        {
            _connectionManager.RemoveConnection(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }
    }

    public class MessageFormat
    {
        public string sender_connection_id { get; set; }
        public string sender_name { get; set; }
        public string sender_message { get; set; }
        public DateTime sender_message_time_sent { get; set; }

    }
}
