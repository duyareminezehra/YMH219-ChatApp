using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YMH219_ChatApp.Models;

namespace YMH219_ChatApp.Services
{
    /// <summary>
    /// TcpListener tabanlı çoklu istemci destekli asenkron sohbet sunucusu sınıfı.
    /// Genel oda mesajlarını ve kullanıcılar arası özel mesajları (DM) yönlendirir.
    /// </summary>
    public class ChatServer
    {
        private sealed class ClientSession
        {
            public ConnectedClientInfo Info { get; }
            public TcpClient TcpClient { get; }
            public StreamWriter Writer { get; }

            public ClientSession(ConnectedClientInfo info, TcpClient tcpClient, StreamWriter writer)
            {
                Info = info;
                TcpClient = tcpClient;
                Writer = writer;
            }
        }

        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly ConcurrentDictionary<string, ClientSession> _clients =
            new ConcurrentDictionary<string, ClientSession>(StringComparer.OrdinalIgnoreCase);

        public bool IsRunning { get; private set; }
        public int Port { get; private set; }

        public event EventHandler<ChatMessage>? ServerLogGenerated;
        public event EventHandler<IReadOnlyList<ConnectedClientInfo>>? ClientListChanged;

        /// <summary>
        /// Belirtilen port üzerinde TCP sunucusunu asenkron olarak başlatır.
        /// </summary>
        public void Start(int port)
        {
            if (IsRunning) return;

            Port = port;
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            IsRunning = true;

            EmitServerLog($"Sunucu {port} portunda dinlemeye başladı.");
            _ = AcceptClientsLoopAsync(_cts.Token);
        }

        /// <summary>
        /// Sunucuyu ve tüm aktif istemci bağlantılarını güvenli şekilde kapatır.
        /// </summary>
        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }

            foreach (var session in _clients.Values)
            {
                try { session.TcpClient.Close(); } catch { }
            }

            _clients.Clear();
            NotifyClientsChanged();
            EmitServerLog("Sunucu durduruldu.");
        }

        public IReadOnlyList<ConnectedClientInfo> GetConnectedClients()
        {
            return _clients.Values
                .Select(c => c.Info)
                .OrderBy(c => c.ConnectedAt)
                .ToList();
        }

        private async Task AcceptClientsLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null)
            {
                try
                {
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync(token);
                    _ = HandleClientSessionAsync(tcpClient, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (IsRunning)
                    {
                        EmitServerLog($"Bağlantı kabul hatası: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleClientSessionAsync(TcpClient tcpClient, CancellationToken token)
        {
            string? registeredUsername = null;
            string remoteEndpoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "127.0.0.1";

            try
            {
                using (tcpClient)
                {
                    NetworkStream stream = tcpClient.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                    // İlk satır: İstemcinin kullanıcı adı tanıtımı
                    string? firstLine = await reader.ReadLineAsync(token);
                    if (string.IsNullOrWhiteSpace(firstLine))
                    {
                        return;
                    }

                    ChatMessage helloMsg = ChatMessage.FromWireString(firstLine);
                    string desiredName = string.IsNullOrWhiteSpace(helloMsg.Sender) ? "Misafir" : helloMsg.Sender.Trim();

                    // Aynı kullanıcı adı varsa sonuna numara ekle
                    string finalName = desiredName;
                    int suffix = 2;
                    while (_clients.ContainsKey(finalName))
                    {
                        finalName = $"{desiredName}_{suffix++}";
                    }

                    registeredUsername = finalName;
                    var info = new ConnectedClientInfo(registeredUsername, remoteEndpoint, DateTime.Now);
                    var session = new ClientSession(info, tcpClient, writer);
                    _clients[registeredUsername] = session;

                    NotifyClientsChanged();
                    await BroadcastClientListAsync();

                    var joinMessage = new ChatMessage(
                        ChatMessageType.System,
                        "Sunucu",
                        "Herkes",
                        $"'{registeredUsername}' sohbet odasına katıldı ({remoteEndpoint}).");
                    EmitServerLog(joinMessage.Content);
                    await BroadcastMessageAsync(joinMessage);

                    // Mesaj okuma döngüsü
                    while (!token.IsCancellationRequested && tcpClient.Connected)
                    {
                        string? line = await reader.ReadLineAsync(token);
                        if (line == null) break;

                        ChatMessage incoming = ChatMessage.FromWireString(line);
                        incoming.Sender = registeredUsername;
                        incoming.Timestamp = DateTime.Now;

                        // @kullanici şeklinde yazılan mesajları da özel mesaja dönüştür
                        if (incoming.Content.StartsWith("@") && incoming.Content.Contains(' '))
                        {
                            int spaceIdx = incoming.Content.IndexOf(' ');
                            string targetUser = incoming.Content.Substring(1, spaceIdx - 1).Trim();
                            string body = incoming.Content.Substring(spaceIdx + 1).Trim();
                            if (!string.IsNullOrEmpty(targetUser) && !string.IsNullOrEmpty(body))
                            {
                                incoming.MessageType = ChatMessageType.Private;
                                incoming.Recipient = targetUser;
                                incoming.Content = body;
                            }
                        }

                        if (incoming.MessageType == ChatMessageType.Private &&
                            !string.Equals(incoming.Recipient, "Herkes", StringComparison.OrdinalIgnoreCase))
                        {
                            EmitServerLog($"[ÖZEL] {incoming.Sender} -> {incoming.Recipient}: {incoming.Content}");
                            await RoutePrivateMessageAsync(incoming);
                        }
                        else
                        {
                            incoming.MessageType = ChatMessageType.Public;
                            incoming.Recipient = "Herkes";
                            EmitServerLog($"{incoming.Sender}: {incoming.Content}");
                            await BroadcastMessageAsync(incoming);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // İstemci beklenmedik şekilde koptuğunda sunucu çalışmaya devam eder
            }
            finally
            {
                if (registeredUsername != null && _clients.TryRemove(registeredUsername, out _))
                {
                    NotifyClientsChanged();
                    await BroadcastClientListAsync();

                    var leaveMsg = new ChatMessage(
                        ChatMessageType.System,
                        "Sunucu",
                        "Herkes",
                        $"'{registeredUsername}' sohbetten ayrıldı.");
                    EmitServerLog(leaveMsg.Content);
                    await BroadcastMessageAsync(leaveMsg);
                }
            }
        }

        private async Task RoutePrivateMessageAsync(ChatMessage message)
        {
            string wire = message.ToWireString();

            if (_clients.TryGetValue(message.Recipient, out ClientSession? targetSession))
            {
                try { await targetSession.Writer.WriteLineAsync(wire); } catch { }

                if (!string.Equals(message.Sender, message.Recipient, StringComparison.OrdinalIgnoreCase) &&
                    _clients.TryGetValue(message.Sender, out ClientSession? senderSession))
                {
                    try { await senderSession.Writer.WriteLineAsync(wire); } catch { }
                }
            }
            else if (_clients.TryGetValue(message.Sender, out ClientSession? senderSession))
            {
                var notFound = new ChatMessage(
                    ChatMessageType.System,
                    "Sunucu",
                    message.Sender,
                    $"Uyarı: '{message.Recipient}' adlı kullanıcı bağlı değil.");
                try { await senderSession.Writer.WriteLineAsync(notFound.ToWireString()); } catch { }
            }
        }

        private async Task BroadcastMessageAsync(ChatMessage message)
        {
            string wire = message.ToWireString();
            foreach (var session in _clients.Values)
            {
                try
                {
                    await session.Writer.WriteLineAsync(wire);
                }
                catch
                {
                    // Yazma hatası alan soket okuma döngüsünde temizlenir
                }
            }
        }

        private async Task BroadcastClientListAsync()
        {
            string userListCsv = string.Join(",", _clients.Keys.OrderBy(x => x));
            var updateMsg = new ChatMessage(
                ChatMessageType.ClientListUpdate,
                "Sunucu",
                "Herkes",
                userListCsv);
            await BroadcastMessageAsync(updateMsg);
        }

        private void NotifyClientsChanged()
        {
            ClientListChanged?.Invoke(this, GetConnectedClients());
        }

        private void EmitServerLog(string text)
        {
            ServerLogGenerated?.Invoke(this, new ChatMessage(ChatMessageType.System, "Sunucu", "Herkes", text));
        }
    }
}
