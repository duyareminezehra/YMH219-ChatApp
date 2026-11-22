using System;

namespace YMH219_ChatApp.Models
{
    /// <summary>
    /// Sunucuya bağlanan bir istemcinin temel kimlik ve ağ bilgilerini tutan OOP modeli.
    /// </summary>
    public class ConnectedClientInfo
    {
        public string Username { get; }
        public string RemoteEndPoint { get; }
        public DateTime ConnectedAt { get; }

        public ConnectedClientInfo(string username, string remoteEndPoint, DateTime connectedAt)
        {
            Username = username;
            RemoteEndPoint = remoteEndPoint;
            ConnectedAt = connectedAt;
        }

        public string ToServerListDisplay()
        {
            return $"👤 {Username}  |  IP: {RemoteEndPoint}  |  Giriş: {ConnectedAt:HH:mm:ss}";
        }

        public override string ToString() => ToServerListDisplay();
    }
}
