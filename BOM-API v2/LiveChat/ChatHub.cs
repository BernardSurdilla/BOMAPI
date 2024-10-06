using JWTAuthentication.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;
using System.Diagnostics;

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

            var requestHttpContext = Context.GetHttpContext();
            string? authorizationHeaderBearerToken = null;

            if (requestHttpContext != null)
            {
                StringValues authorizationHeader = requestHttpContext.Request.Query["access_token"]; ;

                string authorizationHeaderToken = authorizationHeader.ToString();

                authorizationHeaderToken = authorizationHeaderToken.Split(',')[0];
                authorizationHeaderToken = authorizationHeaderToken.Replace("Bearer ", "");

                authorizationHeaderBearerToken = authorizationHeaderToken;
            }
            if (authorizationHeaderBearerToken != null && authorizationHeaderBearerToken != "")
            {
                var decrypt = new JwtSecurityTokenHandler().ReadJwtToken(authorizationHeaderBearerToken);
                currentUser = new ClaimsPrincipal(new ClaimsIdentity(decrypt.Claims));

            }

            ConnectionInfo? currentConnectionInfo = _connectionManager.GetAllCustomerConnections().FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);

            MessageFormat formattedMessage;
            if (currentConnectionInfo != null)
            {
                formattedMessage = CreateFormattedMessage(accountId, message, currentUser, currentConnectionInfo.AccountId);
            }
            else
            {
                formattedMessage = CreateFormattedMessage(accountId, message, currentUser);
            }

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



            var requestHttpContext = Context.GetHttpContext();
            string? authorizationHeaderBearerToken = null;

            if (requestHttpContext != null)
            {
                StringValues authorizationHeader = requestHttpContext.Request.Query["access_token"]; ;

                string authorizationHeaderToken = authorizationHeader.ToString();

                authorizationHeaderToken = authorizationHeaderToken.Split(',')[0];
                authorizationHeaderToken = authorizationHeaderToken.Replace("Bearer ", "");

                authorizationHeaderBearerToken = authorizationHeaderToken;
            }
            if (authorizationHeaderBearerToken != null && authorizationHeaderBearerToken != "")
            {
                var decrypt = new JwtSecurityTokenHandler().ReadJwtToken(authorizationHeaderBearerToken);
                currentUser = new ClaimsPrincipal(new ClaimsIdentity(decrypt.Claims));

            }

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



            var requestHttpContext = Context.GetHttpContext();
            string? authorizationHeaderBearerToken = null;

            if (requestHttpContext != null)
            {
                StringValues authorizationHeader = requestHttpContext.Request.Query["access_token"]; ;

                string authorizationHeaderToken = authorizationHeader.ToString();

                authorizationHeaderToken = authorizationHeaderToken.Split(',')[0];
                authorizationHeaderToken = authorizationHeaderToken.Replace("Bearer ", "");

                authorizationHeaderBearerToken = authorizationHeaderToken;
            }
            if (authorizationHeaderBearerToken != null && authorizationHeaderBearerToken != "")
            {
                var decrypt = new JwtSecurityTokenHandler().ReadJwtToken(authorizationHeaderBearerToken);
                currentUser = new ClaimsPrincipal(new ClaimsIdentity(decrypt.Claims));

            }

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


            var requestHttpContext = Context.GetHttpContext();
            string? authorizationHeaderBearerToken = null;

            if (requestHttpContext != null)
            {
                StringValues authorizationHeader = requestHttpContext.Request.Query["access_token"]; ;

                string authorizationHeaderToken = authorizationHeader.ToString();

                authorizationHeaderToken = authorizationHeaderToken.Split(',')[0];
                authorizationHeaderToken = authorizationHeaderToken.Replace("Bearer ", "");

                authorizationHeaderBearerToken = authorizationHeaderToken;
            }
            if (authorizationHeaderBearerToken != null && authorizationHeaderBearerToken != "")
            {
                var decrypt = new JwtSecurityTokenHandler().ReadJwtToken(authorizationHeaderBearerToken);
                currentUser = new ClaimsPrincipal(new ClaimsIdentity(decrypt.Claims));

            }

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

            var requestHttpContext = Context.GetHttpContext();
            string? authorizationHeaderBearerToken = null;

            if (requestHttpContext != null)
            {
                StringValues authorizationHeader = requestHttpContext.Request.Query["access_token"]; ;
                
                string authorizationHeaderToken = authorizationHeader.ToString();

                authorizationHeaderToken = authorizationHeaderToken.Split(',')[0];
                authorizationHeaderToken = authorizationHeaderToken.Replace("Bearer ", "");

                authorizationHeaderBearerToken = authorizationHeaderToken;
            }
            if (authorizationHeaderBearerToken != null && authorizationHeaderBearerToken != "")
            {
                var decrypt = new JwtSecurityTokenHandler().ReadJwtToken(authorizationHeaderBearerToken);
                currentUser = new ClaimsPrincipal(new ClaimsIdentity(decrypt.Claims));
                
            }
            
            if (currentUser != null)
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
            else
            {
                _connectionManager.AddConnection(Context.ConnectionId);
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
                senderConnectionId = connectionId,
                senderMessage = message,
                senderMessageTimeSent = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"))
            };

            if (senderAccount != null)
            {
                Claim? nameClaim = senderAccount.FindFirst(ClaimTypes.Name);
                Claim? idClaim = senderAccount.FindFirst(ClaimTypes.NameIdentifier);

                response.senderName = nameClaim == null ? "Anonymous" : nameClaim.Value;
                response.senderAccountId = idClaim == null ? "N/A" : idClaim.Value;
            }
            else
            {

                response.senderName = "Anonymous";
                response.senderAccountId = "N/A";
            }

            return response;
        }
        public static MessageFormat CreateFormattedMessage(string connectionId, string message, ClaimsPrincipal? senderAccount, string senderAccountId)
        {
            MessageFormat response = new MessageFormat
            {
                senderConnectionId = connectionId,
                senderMessage = message,
                senderMessageTimeSent = TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"))
            };

            if (senderAccount == null)
            {
                response.senderName = "Anonymous";
                response.senderAccountId = senderAccountId;
            }
            else
            {
                Claim? nameClaim = senderAccount.FindFirst(ClaimTypes.Name);
                Claim? idClaim = senderAccount.FindFirst(ClaimTypes.NameIdentifier);

                response.senderName = nameClaim == null ? "N/A" : nameClaim.Value;
                response.senderAccountId = idClaim == null ? "N/A" : idClaim.Value;

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
                date_sent = message.senderMessageTimeSent,
                message = message.senderMessage,
            };

            await _directMessagesDB.AddAsync(newDbEntry);
            await _directMessagesDB.SaveChangesAsync();

            return 1;
        }
    }

    public class MessageFormat
    {
        public string senderConnectionId { get; set; }
        public string? senderAccountId { get; set; }
        public string senderName { get; set; }
        public string senderMessage { get; set; }
        public DateTime senderMessageTimeSent { get; set; }
    }
}
