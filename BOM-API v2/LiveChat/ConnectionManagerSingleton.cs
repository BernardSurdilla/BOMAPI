using JWTAuthentication.Authentication;
namespace LiveChat
{
    public interface ILiveChatConnectionManager
    {
        void AddConnection(ConnectionInfo entry);
        void AddConnection(string connectionString);
        void RemoveConnection(ConnectionInfo entry);
        void RemoveConnection(string connectionId);
        List<ConnectionInfo> GetAllConnections();
        List<ConnectionInfo> GetAllAdminConnections();
        List<ConnectionInfo> GetAllManagerConnections();
    }
    public class ConnectionInfo
    {
        public string ConnectionId { get; set; }
        public string? AccountId { get; set; }
        public string? Name { get; set; }
        public List<string>? Claims { get; set; }

        public ConnectionInfo() { }
        public ConnectionInfo(string connectionId)
        {
            ConnectionId = connectionId;
            AccountId = null;
            Name = null;
            Claims = null;
        }
        public ConnectionInfo(string connectionId, string? accountId, string? name, List<string>? claims)
        {
            ConnectionId = connectionId;
            AccountId = accountId;
            Name = name;
            Claims = claims;
        }
    }
    public class LiveChatConnectionManager : ILiveChatConnectionManager
    {
        private List<ConnectionInfo> ConnectionInfos = new List<ConnectionInfo>();

        public void AddConnection(ConnectionInfo entry)
        {
            ConnectionInfos.Add(entry);
        }
        public void AddConnection(string connectionId)
        {
            ConnectionInfos.Add(new ConnectionInfo(connectionId));
        }
        public void RemoveConnection(ConnectionInfo entry)
        {
            ConnectionInfos.Remove(entry);
        }
        public void RemoveConnection(string connectionId)
        {
            ConnectionInfo? currentConnectionInfo = ConnectionInfos.Where(x => x != null && x.ConnectionId == connectionId).FirstOrDefault();
            if (currentConnectionInfo != null)
            {
                try { ConnectionInfos.Remove(currentConnectionInfo); }
                catch { Console.WriteLine("Error removing a connection. Id = " + currentConnectionInfo.ConnectionId); }

            }
        }
        public List<ConnectionInfo> GetAllConnections()
        {
            return ConnectionInfos; 
        }
        public List<ConnectionInfo> GetAllAdminConnections()
        {
            return ConnectionInfos.Where(x => x.Claims != null && x.Claims.Contains(UserRoles.Admin)).ToList();
        }
        public List<ConnectionInfo> GetAllManagerConnections()
        {
            return ConnectionInfos.Where(x => x.Claims != null && x.Claims.Contains(UserRoles.Manager)).ToList();
        }

    }
}
