using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace LiveChat
{
    public class ChatHub : Hub
    {
        private readonly ILiveChatConnectionManager _connectionManager;
        private readonly DirectMessagesDB _directMessagesDB;
        private readonly UserManager<APIUsers> _userManager;

        private const string CLIENT_RECIEVE_MESSAGE_FUNCTION_NAME = "RecieveMessage";

        public ChatHub(ILiveChatConnectionManager connectionManager, DirectMessagesDB directMessagesDB, UserManager<APIUsers> userManager)
        {
            _connectionManager = connectionManager; _directMessagesDB = directMessagesDB; _userManager = userManager;
        }

        [HubMethodName("customer-send-message")]
        public async Task CustomerSendMessage(string message, string accountId)
        {
            ClaimsPrincipal? currentUser = Context.User;

            MessageFormat formattedMessage = CreateFormattedMessage(accountId, message, currentUser);

            if (currentUser != null)
            {
                List<Claim> roles = currentUser.FindAll(ClaimTypes.Role).ToList();

                if (roles.Where(x => x.Value == UserRoles.Customer).FirstOrDefault() == null) return;
                //if (currentUser.HasClaim(x => x.ValueType == ClaimTypes.NameIdentifier) == false) return;

                APIUsers? currentUserAccount = await _userManager.FindByIdAsync(currentUser.FindFirstValue(ClaimTypes.NameIdentifier));
                APIUsers? recieverUserAccount = await _userManager.FindByIdAsync(accountId);

                IList<string> recieverUserAccountRoles = await _userManager.GetRolesAsync(recieverUserAccount);

                if (recieverUserAccountRoles.Contains(UserRoles.Admin)
                    || recieverUserAccountRoles.Contains(UserRoles.Manager))
                {
                    await LogMessageToDB(formattedMessage, currentUserAccount, recieverUserAccount);
                }
            }

            ConnectionInfo? messageRecipientConnectionInfo = _connectionManager.GetAllAdminConnections().Where(x => x.AccountId == accountId).FirstOrDefault();
            if (messageRecipientConnectionInfo == null)
            {
                messageRecipientConnectionInfo = _connectionManager.GetAllManagerConnections().Where(x => x.AccountId == accountId).FirstOrDefault();
            }
            if (messageRecipientConnectionInfo == null) { return; }

            ISingleClientProxy callerClientProxy = Clients.Caller;
            ISingleClientProxy recepientClientProxy = Clients.Client(messageRecipientConnectionInfo.ConnectionId);

            await SendMessageToSenderAndRecepientClients(callerClientProxy, recepientClientProxy, formattedMessage);
        }

        [HubMethodName("artist-send-message")]
        public async Task ArtistSendMessage(string message, string accountId)
        {
            ClaimsPrincipal? currentUser = Context.User;
            if (currentUser == null) return;

            List<Claim> roles = currentUser.FindAll(ClaimTypes.Role).ToList();
            if (roles.Count == 0) return;
            if (roles.Where(x => x.Value == UserRoles.Artist).FirstOrDefault() == null) return;
            //if (currentUser.HasClaim(x => x.ValueType == ClaimTypes.NameIdentifier) == false) return;

            MessageFormat formattedMessage = CreateFormattedMessage(Context.ConnectionId, message, currentUser);

            APIUsers? currentUserAccount = await _userManager.FindByIdAsync(currentUser.FindFirstValue(ClaimTypes.NameIdentifier));
            APIUsers? recieverUserAccount = await _userManager.FindByIdAsync(accountId);

            if (currentUserAccount != null && recieverUserAccount != null)
            {
                IList<string> recieverUserAccountRoles = await _userManager.GetRolesAsync(recieverUserAccount);

                if (recieverUserAccountRoles.Contains(UserRoles.Artist)
                    || recieverUserAccountRoles.Contains(UserRoles.Admin)
                    || recieverUserAccountRoles.Contains(UserRoles.Manager))
                {
                    await LogMessageToDB(formattedMessage, currentUserAccount, recieverUserAccount);
                }
            }

            ConnectionInfo? messageRecipientConnectionInfo = _connectionManager.GetAllAdminConnections().Where(x => x.AccountId == accountId).FirstOrDefault();
            if (messageRecipientConnectionInfo == null) messageRecipientConnectionInfo = _connectionManager.GetAllManagerConnections().Where(x => x.AccountId == accountId).FirstOrDefault();
            if (messageRecipientConnectionInfo == null) { return; }

            ISingleClientProxy callerClientProxy = Clients.Caller;
            ISingleClientProxy recepientClientProxy = Clients.Client(messageRecipientConnectionInfo.ConnectionId);

            await SendMessageToSenderAndRecepientClients(callerClientProxy, recepientClientProxy, formattedMessage);
        }
        [HubMethodName("manager-send-message")]
        public async Task ManagerSendMessage(string message, string accountId)
        {
            ClaimsPrincipal? currentUser = Context.User;
            if (currentUser == null) return;

            List<Claim> roles = currentUser.FindAll(ClaimTypes.Role).ToList();
            if (roles.Count == 0) return;
            if (roles.Where(x => x.Value == UserRoles.Artist).FirstOrDefault() == null) return;
            //if (currentUser.HasClaim(x => x.ValueType == ClaimTypes.NameIdentifier) == false) return;

            MessageFormat formattedMessage = CreateFormattedMessage(Context.ConnectionId, message, currentUser);

            APIUsers? currentUserAccount = await _userManager.FindByIdAsync(currentUser.FindFirstValue(ClaimTypes.NameIdentifier));
            APIUsers? recieverUserAccount = await _userManager.FindByIdAsync(accountId);

            if (currentUserAccount != null && recieverUserAccount != null)
            {
                IList<string> recieverUserAccountRoles = await _userManager.GetRolesAsync(recieverUserAccount);

                if (recieverUserAccountRoles.Contains(UserRoles.Artist)
                    || recieverUserAccountRoles.Contains(UserRoles.Admin)
                    || recieverUserAccountRoles.Contains(UserRoles.Manager))
                {
                    await LogMessageToDB(formattedMessage, currentUserAccount, recieverUserAccount);
                }
            }

            ConnectionInfo? messageRecipientConnectionInfo = _connectionManager.GetAllAdminConnections().Where(x => x.AccountId == accountId).FirstOrDefault();
            if (messageRecipientConnectionInfo == null) messageRecipientConnectionInfo = _connectionManager.GetAllArtistConnections().Where(x => x.AccountId == accountId).FirstOrDefault();
            if (messageRecipientConnectionInfo == null) { return; }


            ISingleClientProxy callerClientProxy = Clients.Caller;
            ISingleClientProxy recepientClientProxy = Clients.Client(messageRecipientConnectionInfo.ConnectionId);

            await SendMessageToSenderAndRecepientClients(callerClientProxy, recepientClientProxy, formattedMessage);
        }

        [HubMethodName("admin-send-message")]
        public async Task AdminSendMessage(string message, string accountId)
        {
            ClaimsPrincipal? currentUser = Context.User;
            if (currentUser == null) return;

            List<Claim> roles = currentUser.FindAll(ClaimTypes.Role).ToList();
            if (roles.Count == 0) return;
            if (roles.Where(x => x.Value == UserRoles.Admin).FirstOrDefault() == null) return;
            //if (currentUser.HasClaim(x => x.ValueType == ClaimTypes.NameIdentifier) == false) return;

            MessageFormat formattedMessage = CreateFormattedMessage(Context.ConnectionId, message, currentUser);

            APIUsers? currentUserAccount = await _userManager.FindByIdAsync(currentUser.FindFirstValue(ClaimTypes.NameIdentifier));
            APIUsers? recieverUserAccount = await _userManager.FindByIdAsync(accountId);

            if (currentUserAccount != null && recieverUserAccount != null)
            {
                await LogMessageToDB(formattedMessage, currentUserAccount, recieverUserAccount);
            }

            ConnectionInfo? messageRecipientConnectionInfo = _connectionManager.GetAllConnections().Where(x => x.AccountId == accountId).FirstOrDefault();
            if (messageRecipientConnectionInfo == null)
            {
                return;
            }

            ISingleClientProxy callerClientProxy = Clients.Caller;
            ISingleClientProxy recepientClientProxy = Clients.Client(messageRecipientConnectionInfo.ConnectionId);

            await SendMessageToSenderAndRecepientClients(callerClientProxy, recepientClientProxy, formattedMessage);
        }

        public override Task OnConnectedAsync()
        {
            ClaimsPrincipal? currentUser = Context.User;

            if (currentUser == null) { _connectionManager.AddConnection(Context.ConnectionId); }
            else
            {
                List<Claim> allRoles = currentUser.FindAll(ClaimTypes.Role).ToList();
                List<string> allRolesParsed = new List<string>();
                foreach (Claim claim in allRoles) { allRolesParsed.Add(claim.Value); }

                _connectionManager.AddConnection(new ConnectionInfo(
                Context.ConnectionId,
                currentUser.FindFirstValue(ClaimTypes.NameIdentifier),
                currentUser.FindFirstValue(ClaimTypes.Name),
                allRolesParsed));
            }

            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            _connectionManager.RemoveConnection(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        public static MessageFormat CreateFormattedMessage(string connectionId, string message, ClaimsPrincipal? senderAccount)
        {
            MessageFormat response = new MessageFormat
            {
                sender_connection_id = connectionId,
                sender_message = message,
                sender_message_time_sent = DateTime.Now
            };

            if (senderAccount == null)
            {
                response.sender_name = "Anonymous";
                response.sender_account_id = "N/A";
            }
            else
            {
                Claim? nameClaim = senderAccount.FindFirst(ClaimTypes.Name);
                Claim? idClaim = senderAccount.FindFirst(ClaimTypes.NameIdentifier);

                response.sender_name = nameClaim == null ? "N/A" : nameClaim.Value;
                response.sender_account_id = idClaim == null ? "N/A" : idClaim.Value;

            }

            return response;
        }
        public async Task<int> SendMessageToSenderAndRecepientClients(ISingleClientProxy sender, ISingleClientProxy recepient, MessageFormat message)
        {
            await recepient.SendAsync(CLIENT_RECIEVE_MESSAGE_FUNCTION_NAME, message);
            await sender.SendAsync(CLIENT_RECIEVE_MESSAGE_FUNCTION_NAME, message);

            return 1;
        }
        public async Task<int> LogMessageToDB(MessageFormat message, APIUsers sender, APIUsers recepient)
        {
            DirectMessages newDbEntry = new DirectMessages
            {
                direct_message_id = Guid.NewGuid(),
                sender_account_id = sender.Id,
                receiver_account_id = recepient.Id,
                date_sent = message.sender_message_time_sent,
                message = message.sender_message,
            };

            await _directMessagesDB.AddAsync(newDbEntry);
            await _directMessagesDB.SaveChangesAsync();

            return 1;
        }
    }

    public class MessageFormat
    {
        public string sender_connection_id { get; set; }
        public string? sender_account_id { get; set; }
        public string sender_name { get; set; }
        public string sender_message { get; set; }
        public DateTime sender_message_time_sent { get; set; }
    }
}
