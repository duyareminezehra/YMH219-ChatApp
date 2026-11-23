using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YMH219_ChatApp.Models;

namespace YMH219_ChatApp.Services
{
    /// <summary>
    /// TcpClient ve StreamReader/StreamWriter kullanarak sohbet sunucusuna asenkron bağlanan istemci sınıfı.
    /// </summary>
    public class ChatClient
    {
        private TcpClient? _tcpClient;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _cts;
        private readonly List<ChatMessage> _messageHistory = new List<ChatMessage>();

        public bool IsConnected => _tcpClient != null && _tcpClient.Connected;
        public string Username { get; private set; } = string.Empty;
        public IReadOnlyList<ChatMessage> MessageHistory => _messageHistory;

        public event EventHandler<ChatMessage>? MessageReceived;
        public event EventHandler<IReadOnlyList<string>>? OnlineUsersUpdated;
        public event EventHandler<string>? ConnectionStateChanged;
        public event EventHandler<string>? ConnectionErrorOccurred;

        /// <summary>
        /// Belirtilen sunucu IP ve portuna kullanıcı adı ile asenkron olarak bağlanır.
        /// </summary>
        public async Task<bool> ConnectAsync(string host, int port, string username)
        {
            if (IsConnected)
            {
                return true;
            }

            try
            {
                Username = string.IsNullOrWhiteSpace(username) ? "Kullanici" : username.Trim();
                _cts = new CancellationTokenSource();
                _tcpClient = new TcpClient();

                await _tcpClient.ConnectAsync(host, port);
                NetworkStream stream = _tcpClient.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                // İlk paket: Kullanıcı adını sunucuya bildir
                var handshake = new ChatMessage(ChatMessageType.System, Username, "Sunucu", "MERHABA");
                await _writer.WriteLineAsync(handshake.ToWireString());

                ConnectionStateChanged?.Invoke(this, $"Bağlandı ({host}:{port} - {Username})");
                _ = ReceiveLoopAsync(_cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                CleanupResources();
                ConnectionErrorOccurred?.Invoke(this, $"Sunucuya bağlanılamadı ({host}:{port}): {ex.Message}");
                ConnectionStateChanged?.Invoke(this, "Bağlantı Yok");
                return false;
            }
        }

        /// <summary>
        /// Sunucuya genel veya özel (DM) mesaj gönderir.
        /// </summary>
        public async Task SendMessageAsync(string recipient, string content)
        {
            if (!IsConnected || _writer == null || string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            try
            {
                bool isPrivate = !string.IsNullOrWhiteSpace(recipient) &&
                                 !string.Equals(recipient, "Herkes (Genel Oda)", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(recipient, "Herkes", StringComparison.OrdinalIgnoreCase);

                var msg = new ChatMessage(
                    isPrivate ? ChatMessageType.Private : ChatMessageType.Public,
                    Username,
                    isPrivate ? recipient : "Herkes",
                    content.Trim());

                await _writer.WriteLineAsync(msg.ToWireString());
            }
            catch (Exception ex)
            {
                ConnectionErrorOccurred?.Invoke(this, $"Mesaj gönderilemedi: {ex.Message}");
                Disconnect();
            }
        }

        /// <summary>
        /// Sunucu bağlantısını güvenli şekilde sonlandırır.
        /// </summary>
        public void Disconnect()
        {
            if (_tcpClient == null) return;

            CleanupResources();
            ConnectionStateChanged?.Invoke(this, "Bağlantı Kesildi");
        }

        /// <summary>
        /// Sohbet geçmişini .txt dosyasına kaydeder (İsteğe bağlı geliştirme).
        /// </summary>
        public void SaveHistoryToFile(string filePath)
        {
            using var writer = new StreamWriter(filePath, append: false, encoding: Encoding.UTF8);
            writer.WriteLine("======================================================================");
            writer.WriteLine($"YMH219 - SOHBET GEÇMİŞİ KAYDI ({Username})");
            writer.WriteLine($"Kayıt Tarihi: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine("======================================================================");
            writer.WriteLine();

            foreach (ChatMessage msg in _messageHistory)
            {
                writer.WriteLine(msg.ToDisplayString());
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _reader != null && IsConnected)
                {
                    string? line = await _reader.ReadLineAsync(token);
                    if (line == null)
                    {
                        break;
                    }

                    ChatMessage incoming = ChatMessage.FromWireString(line);
                    if (incoming.MessageType == ChatMessageType.ClientListUpdate)
                    {
                        List<string> users = incoming.Content
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .ToList();
                        OnlineUsersUpdated?.Invoke(this, users);
                        continue;
                    }

                    _messageHistory.Add(incoming);
                    MessageReceived?.Invoke(this, incoming);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal çıkış
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    ConnectionErrorOccurred?.Invoke(this, $"Sunucu ile bağlantı koptu: {ex.Message}");
                }
            }
            finally
            {
                CleanupResources();
                ConnectionStateChanged?.Invoke(this, "Bağlantı Kesildi");
            }
        }

        private void CleanupResources()
        {
            try { _cts?.Cancel(); } catch { }
            try { _reader?.Dispose(); } catch { }
            try { _writer?.Dispose(); } catch { }
            try { _tcpClient?.Close(); } catch { }

            _reader = null;
            _writer = null;
            _tcpClient = null;
        }
    }
}
