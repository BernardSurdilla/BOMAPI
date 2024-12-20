﻿using JWTAuthentication.Authentication;
namespace LiveChat
{
    public interface ILiveChatConnectionManager
    {
        void AddConnection(ConnectionInfo entry);
        string AddConnection(string connectionString);
        void RemoveConnection(ConnectionInfo entry);
        void RemoveConnection(string connectionId);
        List<ConnectionInfo> GetAllConnections();
        List<ConnectionInfo> GetAllCustomerConnections();
        List<ConnectionInfo> GetAllManagerConnections();
        List<ConnectionInfo> GetAllArtistConnections();
        List<ConnectionInfo> GetAllAdminConnections();
    }
    public class ConnectionInfo
    {
        public string ConnectionId { get; set; }
        public string? AccountId { get; set; }
        public string? Name { get; set; }
        public List<string>? Claims { get; set; }

        public ConnectionInfo() { }
        public ConnectionInfo(string connectionId, string accountId)
        {
            ConnectionId = connectionId;
            AccountId = accountId;
            Name = "Anonymous";
            Claims = new List<string>{"Customer"};
        }
        public ConnectionInfo(string connectionId, string? accountId, string? name, List<string>? claims)
        {
            ConnectionId = connectionId;
            AccountId = accountId == null ? Guid.NewGuid().ToString() : accountId;
            Name = name == null ? "Anonymous" : name;
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
        public string AddConnection(string connectionId)
        {
            string newConnectionId = Guid.NewGuid().ToString();
            ConnectionInfos.Add(new ConnectionInfo(connectionId, newConnectionId));
            return newConnectionId;
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
        public List<ConnectionInfo> GetAllCustomerConnections()
        {
            return ConnectionInfos.Where(x => x.Claims != null && x.Claims.Contains(UserRoles.Customer)).ToList();
        }
        public List<ConnectionInfo> GetAllArtistConnections()
        {
            return ConnectionInfos.Where(x => x.Claims != null && x.Claims.Contains(UserRoles.Artist)).ToList();
        }
        public List<ConnectionInfo> GetAllManagerConnections()
        {
            return ConnectionInfos.Where(x => x.Claims != null && x.Claims.Contains(UserRoles.Manager)).ToList();
        }
        public List<ConnectionInfo> GetAllAdminConnections()
        {
            return ConnectionInfos.Where(x => x.Claims != null && x.Claims.Contains(UserRoles.Admin)).ToList();
        }

    }
}
