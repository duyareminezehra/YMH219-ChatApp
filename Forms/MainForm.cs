using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using YMH219_ChatApp.Models;
using YMH219_ChatApp.Services;

namespace YMH219_ChatApp.Forms
{
    /// <summary>
    /// İstemci-Sunucu (TCP/IP) Sohbet Uygulamasının ana Windows Forms arayüzü.
    /// Aynı pencere üzerinden hem Sunucu (TcpListener) yönetimi hem de İstemci (TcpClient)
    /// sohbet odası ve özel mesajlaşma işlevlerini sunar; ayrıca ek istemci penceresi açabilir.
    /// </summary>
    public class MainForm : Form
    {
        private readonly ChatServer _chatServer = new ChatServer();
        private readonly ChatClient _chatClient = new ChatClient();
        private static int _spawnedClientCounter = 2;

        // Sunucu Kontrolleri (Sol Panel)
        private GroupBox _grpServer = null!;
        private TextBox _txtServerPort = null!;
        private Button _btnStartServer = null!;
        private Button _btnStopServer = null!;
        private Button _btnOpenNewClientWindow = null!;
        private Label _lblServerStatus = null!;
        private ListBox _lstConnectedClients = null!;
        private ListBox _lstServerLogs = null!;

        // İstemci Kontrolleri (Sağ Panel)
        private GroupBox _grpClient = null!;
        private TextBox _txtHostIp = null!;
        private TextBox _txtClientPort = null!;
        private TextBox _txtUsername = null!;
        private Button _btnConnect = null!;
        private Button _btnDisconnect = null!;
        private Panel _clientStatusBadge = null!;
        private Label _lblClientStatus = null!;
        private ListBox _lstChatMessages = null!;
        private ComboBox _cmbRecipient = null!;
        private TextBox _txtMessageInput = null!;
        private Button _btnSendMessage = null!;
        private Button _btnSaveHistory = null!;

        public MainForm(string defaultUsername = "Zehra", bool clientOnlyMode = false)
        {
            Text = clientOnlyMode
                ? $"YMH219 - Chat İstemcisi ({defaultUsername})"
                : "YMH219 - İstemci-Sunucu (TCP/IP) Chat Uygulaması";
            Size = new Size(1150, 680);
            MinimumSize = new Size(1080, 620);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(241, 245, 249);
            Font = new Font("Segoe UI", 9.2f);

            InitializeComponents(defaultUsername, clientOnlyMode);
            BindEvents();
            UpdateServerUiState();
            UpdateClientUiState("Bağlantı Yok");
        }

        private void InitializeComponents(string defaultUsername, bool clientOnlyMode)
        {
            // 1. Sol Panel: Sunucu Yönetimi
            _grpServer = new GroupBox
            {
                Text = "Sunucu (Server - TcpListener) Yönetimi",
                Dock = DockStyle.Left,
                Width = 365,
                Padding = new Padding(12),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42),
                Visible = !clientOnlyMode
            };

            var lblPort = new Label
            {
                Text = "Port:",
                Location = new Point(14, 32),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _txtServerPort = new TextBox
            {
                Text = "5000",
                Location = new Point(54, 29),
                Size = new Size(65, 25),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
            };

            _btnStartServer = CreateButton("Sunucuyu Başlat", new Point(128, 26), new Size(115, 32), Color.FromArgb(22, 163, 74));
            _btnStopServer = CreateButton("Durdur", new Point(250, 26), new Size(98, 32), Color.FromArgb(220, 38, 38));

            _lblServerStatus = new Label
            {
                Text = "● Sunucu Durumu: Kapalı",
                Location = new Point(14, 68),
                Size = new Size(334, 24),
                Font = new Font("Segoe UI", 9.2f, FontStyle.Bold),
                ForeColor = Color.FromArgb(185, 28, 28)
            };

            _btnOpenNewClientWindow = CreateButton(
                "+ Yeni İstemci Penceresi Aç (Çoklu Sohbet Testi)",
                new Point(14, 96),
                new Size(334, 34),
                Color.FromArgb(79, 70, 229));

            var lblClientsHeader = new Label
            {
                Text = "Bağlanan İstemciler (Kullanıcı | IP:Port | Saat):",
                Location = new Point(14, 142),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            _lstConnectedClients = new ListBox
            {
                Location = new Point(14, 165),
                Size = new Size(334, 175),
                Font = new Font("Consolas", 9f),
                BackColor = Color.FromArgb(248, 250, 252),
                HorizontalScrollbar = true
            };

            var lblServerLogHeader = new Label
            {
                Text = "Sunucu Olay Günlüğü:",
                Location = new Point(14, 350),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            _lstServerLogs = new ListBox
            {
                Location = new Point(14, 373),
                Size = new Size(334, 235),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Consolas", 8.8f),
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(148, 163, 184),
                HorizontalScrollbar = true
            };

            _grpServer.Controls.AddRange(new Control[]
            {
                lblPort,
                _txtServerPort,
                _btnStartServer,
                _btnStopServer,
                _lblServerStatus,
                _btnOpenNewClientWindow,
                lblClientsHeader,
                _lstConnectedClients,
                lblServerLogHeader,
                _lstServerLogs
            });

            // 2. Sağ Panel: İstemci ve Sohbet Odası
            _grpClient = new GroupBox
            {
                Text = "İstemci (Client - TcpClient) ve Sohbet Odası",
                Dock = DockStyle.Fill,
                Padding = new Padding(14),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(15, 23, 42)
            };

            var topConnectPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 82,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            var lblHost = new Label
            {
                Text = "Sunucu IP:",
                Location = new Point(10, 14),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _txtHostIp = new TextBox
            {
                Text = "127.0.0.1",
                Location = new Point(78, 11),
                Size = new Size(95, 25),
                Font = new Font("Segoe UI", 9.2f, FontStyle.Regular)
            };

            var lblClientPort = new Label
            {
                Text = "Port:",
                Location = new Point(182, 14),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _txtClientPort = new TextBox
            {
                Text = "5000",
                Location = new Point(218, 11),
                Size = new Size(56, 25),
                Font = new Font("Segoe UI", 9.2f, FontStyle.Regular)
            };

            var lblUser = new Label
            {
                Text = "Kullanıcı Adı:",
                Location = new Point(284, 14),
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            _txtUsername = new TextBox
            {
                Text = defaultUsername,
                Location = new Point(368, 11),
                Size = new Size(105, 25),
                Font = new Font("Segoe UI", 9.2f, FontStyle.Bold)
            };

            _btnConnect = CreateButton("Bağlan", new Point(482, 8), new Size(82, 30), Color.FromArgb(37, 99, 235));
            _btnDisconnect = CreateButton("Bağlantıyı Kes", new Point(570, 8), new Size(105, 30), Color.FromArgb(100, 116, 139));

            _clientStatusBadge = new Panel
            {
                Location = new Point(10, 44),
                Size = new Size(463, 28),
                BackColor = Color.FromArgb(254, 226, 226)
            };

            _lblClientStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Text = "● İstemci Durumu: Bağlantı Yok",
                ForeColor = Color.FromArgb(153, 27, 27),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _clientStatusBadge.Controls.Add(_lblClientStatus);

            _btnSaveHistory = CreateButton("Sohbeti .txt Kaydet", new Point(482, 43), new Size(193, 30), Color.FromArgb(14, 116, 144));

            topConnectPanel.Controls.AddRange(new Control[]
            {
                lblHost,
                _txtHostIp,
                lblClientPort,
                _txtClientPort,
                lblUser,
                _txtUsername,
                _btnConnect,
                _btnDisconnect,
                _clientStatusBadge,
                _btnSaveHistory
            });

            var bottomSendPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                BackColor = Color.White,
                Padding = new Padding(10)
            };

            var lblRecipient = new Label
            {
                Text = "Alıcı (Genel / Özel Mesaj):",
                Location = new Point(10, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            _cmbRecipient = new ComboBox
            {
                Location = new Point(172, 6),
                Size = new Size(195, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };
            _cmbRecipient.Items.Add("Herkes (Genel Oda)");
            _cmbRecipient.SelectedIndex = 0;

            var lblDmHint = new Label
            {
                Text = "İpucu: Özel mesaj için alıcı seçebilir veya '@kullanici mesaj' yazabilirsiniz.",
                Location = new Point(378, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.2f, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 116, 139)
            };

            _txtMessageInput = new TextBox
            {
                Location = new Point(10, 38),
                Size = new Size(545, 28),
                Font = new Font("Segoe UI", 10f, FontStyle.Regular),
                PlaceholderText = "Mesajınızı buraya yazın ve Enter'a veya 'Mesaj Gönder' butonuna basın..."
            };

            _btnSendMessage = CreateButton("Mesaj Gönder", new Point(565, 36), new Size(140, 32), Color.FromArgb(22, 163, 74));

            bottomSendPanel.Controls.AddRange(new Control[]
            {
                lblRecipient,
                _cmbRecipient,
                lblDmHint,
                _txtMessageInput,
                _btnSendMessage
            });

            var chatListContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 8)
            };

            _lstChatMessages = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9.8f),
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.FromArgb(241, 245, 249),
                HorizontalScrollbar = true
            };
            chatListContainer.Controls.Add(_lstChatMessages);

            _grpClient.Controls.Add(chatListContainer);
            _grpClient.Controls.Add(bottomSendPanel);
            _grpClient.Controls.Add(topConnectPanel);

            var rootContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };
            rootContainer.Controls.Add(_grpClient);
            rootContainer.Controls.Add(_grpServer);

            Controls.Add(rootContainer);
        }

        private static Button CreateButton(string text, Point location, Size size, Color backColor)
        {
            var btn = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void BindEvents()
        {
            _btnStartServer.Click += (_, _) => HandleStartServer();
            _btnStopServer.Click += (_, _) => HandleStopServer();
            _btnOpenNewClientWindow.Click += (_, _) =>
            {
                string nextUser = $"Kullanici{_spawnedClientCounter++}";
                var secondForm = new MainForm(nextUser, clientOnlyMode: true);
                secondForm.Show();
            };

            _btnConnect.Click += async (_, _) => await HandleConnectClientAsync();
            _btnDisconnect.Click += (_, _) => _chatClient.Disconnect();
            _btnSendMessage.Click += async (_, _) => await HandleSendMessageAsync();
            _btnSaveHistory.Click += (_, _) => HandleSaveChatHistory();

            _txtMessageInput.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await HandleSendMessageAsync();
                }
            };

            _chatServer.ServerLogGenerated += (_, logMsg) =>
            {
                InvokeIfRequired(() =>
                {
                    _lstServerLogs.Items.Add(logMsg.ToDisplayString());
                    _lstServerLogs.TopIndex = Math.Max(0, _lstServerLogs.Items.Count - 1);
                });
            };

            _chatServer.ClientListChanged += (_, clients) =>
            {
                InvokeIfRequired(() =>
                {
                    _lstConnectedClients.BeginUpdate();
                    _lstConnectedClients.Items.Clear();
                    foreach (ConnectedClientInfo client in clients)
                    {
                        _lstConnectedClients.Items.Add(client.ToServerListDisplay());
                    }
                    _lstConnectedClients.EndUpdate();
                });
            };

            _chatClient.MessageReceived += (_, msg) =>
            {
                InvokeIfRequired(() =>
                {
                    _lstChatMessages.Items.Add(msg.ToDisplayString());
                    _lstChatMessages.TopIndex = Math.Max(0, _lstChatMessages.Items.Count - 1);
                });
            };

            _chatClient.OnlineUsersUpdated += (_, users) =>
            {
                InvokeIfRequired(() =>
                {
                    string previousSelection = _cmbRecipient.SelectedItem?.ToString() ?? "Herkes (Genel Oda)";
                    _cmbRecipient.BeginUpdate();
                    _cmbRecipient.Items.Clear();
                    _cmbRecipient.Items.Add("Herkes (Genel Oda)");
                    foreach (string user in users)
                    {
                        if (!string.Equals(user, _chatClient.Username, StringComparison.OrdinalIgnoreCase))
                        {
                            _cmbRecipient.Items.Add(user);
                        }
                    }

                    int idx = _cmbRecipient.Items.IndexOf(previousSelection);
                    _cmbRecipient.SelectedIndex = idx >= 0 ? idx : 0;
                    _cmbRecipient.EndUpdate();
                });
            };

            _chatClient.ConnectionStateChanged += (_, statusText) =>
            {
                InvokeIfRequired(() => UpdateClientUiState(statusText));
            };

            _chatClient.ConnectionErrorOccurred += (_, errorMsg) =>
            {
                InvokeIfRequired(() =>
                {
                    _lstChatMessages.Items.Add($"[{DateTime.Now:HH:mm:ss}] [BAĞLANTI HATASI] {errorMsg}");
                    _lstChatMessages.TopIndex = Math.Max(0, _lstChatMessages.Items.Count - 1);
                    MessageBox.Show(this, errorMsg, "Ağ / Bağlantı Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                });
            };

            FormClosing += (_, _) =>
            {
                _chatClient.Disconnect();
                _chatServer.Stop();
            };
        }

        private void HandleStartServer()
        {
            if (!int.TryParse(_txtServerPort.Text.Trim(), out int port) || port < 1024 || port > 65535)
            {
                MessageBox.Show(this, "Lütfen 1024 ile 65535 arasında geçerli bir port numarası girin.", "Geçersiz Port", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                _chatServer.Start(port);
                _txtClientPort.Text = port.ToString();
                UpdateServerUiState();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Sunucu başlatılamadı:\n{ex.Message}", "Sunucu Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void HandleStopServer()
        {
            _chatServer.Stop();
            UpdateServerUiState();
        }

        private async Task HandleConnectClientAsync()
        {
            string host = _txtHostIp.Text.Trim();
            string username = _txtUsername.Text.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show(this, "Lütfen bağlanmadan önce bir kullanıcı adı girin.", "Kullanıcı Adı Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!int.TryParse(_txtClientPort.Text.Trim(), out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show(this, "Geçerli bir port numarası girin.", "Geçersiz Port", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Eğer yerel sunucu henüz başlatılmamışsa ve 127.0.0.1'e bağlanılıyorsa kullanıcıya kolaylık olarak sunucuyu otomatik başlat
            if (!_chatServer.IsRunning && _grpServer.Visible && (host == "127.0.0.1" || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    _chatServer.Start(port);
                    UpdateServerUiState();
                }
                catch
                {
                    // Başka bir pencerede sunucu zaten çalışıyor olabilir
                }
            }

            _btnConnect.Enabled = false;
            await _chatClient.ConnectAsync(host, port, username);
            UpdateClientUiState(_chatClient.IsConnected ? $"Bağlandı ({host}:{port} - {_chatClient.Username})" : "Bağlantı Yok");
        }

        private async Task HandleSendMessageAsync()
        {
            if (!_chatClient.IsConnected)
            {
                MessageBox.Show(this, "Mesaj göndermek için önce bir sunucuya bağlanmalısınız.", "Bağlı Değil", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string text = _txtMessageInput.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            string recipient = _cmbRecipient.SelectedItem?.ToString() ?? "Herkes (Genel Oda)";
            _txtMessageInput.Clear();
            await _chatClient.SendMessageAsync(recipient, text);
            _txtMessageInput.Focus();
        }

        private void HandleSaveChatHistory()
        {
            if (_lstChatMessages.Items.Count == 0)
            {
                MessageBox.Show(this, "Kaydedilecek sohbet mesajı bulunmuyor.", "Sohbet Boş", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Sohbet Geçmişini .txt Dosyasına Kaydet",
                Filter = "Metin Dosyası (*.txt)|*.txt",
                FileName = $"ChatHistory_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    if (_chatClient.MessageHistory.Count > 0)
                    {
                        _chatClient.SaveHistoryToFile(sfd.FileName);
                    }
                    else
                    {
                        using var writer = new StreamWriter(sfd.FileName);
                        foreach (var item in _lstChatMessages.Items)
                        {
                            writer.WriteLine(item?.ToString());
                        }
                    }

                    MessageBox.Show(this, $"Sohbet geçmişi başarıyla kaydedildi:\n{sfd.FileName}", "Kayıt Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Dosya kaydedilirken hata oluştu:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void UpdateServerUiState()
        {
            bool running = _chatServer.IsRunning;
            _btnStartServer.Enabled = !running;
            _btnStopServer.Enabled = running;
            _txtServerPort.Enabled = !running;

            if (running)
            {
                _lblServerStatus.Text = $"● Sunucu Durumu: Aktif (Port: {_chatServer.Port})";
                _lblServerStatus.ForeColor = Color.FromArgb(21, 128, 61);
            }
            else
            {
                _lblServerStatus.Text = "● Sunucu Durumu: Kapalı";
                _lblServerStatus.ForeColor = Color.FromArgb(185, 28, 28);
            }
        }

        private void UpdateClientUiState(string statusDescription)
        {
            bool connected = _chatClient.IsConnected;
            _btnConnect.Enabled = !connected;
            _btnDisconnect.Enabled = connected;
            _txtHostIp.Enabled = !connected;
            _txtClientPort.Enabled = !connected;
            _txtUsername.Enabled = !connected;
            _btnSendMessage.Enabled = connected;

            _lblClientStatus.Text = $"● İstemci Durumu: {statusDescription}";
            if (connected)
            {
                _clientStatusBadge.BackColor = Color.FromArgb(220, 252, 231);
                _lblClientStatus.ForeColor = Color.FromArgb(21, 128, 61);
            }
            else
            {
                _clientStatusBadge.BackColor = Color.FromArgb(254, 226, 226);
                _lblClientStatus.ForeColor = Color.FromArgb(153, 27, 27);
            }
        }

        private void InvokeIfRequired(Action action)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// README.md için çoklu istemci bağlantısını ve özel mesajlaşma görünümünü oluşturup kaydeder.
        /// </summary>
        public void SaveDocumentationScreenshots(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            Show();
            PerformLayout();

            // 1. Ekran Görüntüsü: Sunucu Aktif + Çoklu İstemci Bağlı + Genel Oda Sohbeti
            _lblServerStatus.Text = "● Sunucu Durumu: Aktif (Port: 5000)";
            _lblServerStatus.ForeColor = Color.FromArgb(21, 128, 61);
            _btnStartServer.Enabled = false;
            _btnStopServer.Enabled = true;

            _clientStatusBadge.BackColor = Color.FromArgb(220, 252, 231);
            _lblClientStatus.ForeColor = Color.FromArgb(21, 128, 61);
            _lblClientStatus.Text = "● İstemci Durumu: Bağlandı (127.0.0.1:5000 - Zehra)";
            _btnConnect.Enabled = false;
            _btnDisconnect.Enabled = true;

            _lstConnectedClients.Items.Clear();
            _lstConnectedClients.Items.Add("👤 Zehra  |  IP: 127.0.0.1:52140  |  Giriş: 21:40:12");
            _lstConnectedClients.Items.Add("👤 Ahmet  |  IP: 127.0.0.1:52144  |  Giriş: 21:40:18");
            _lstConnectedClients.Items.Add("👤 Elif   |  IP: 127.0.0.1:52149  |  Giriş: 21:40:25");

            _lstServerLogs.Items.Clear();
            _lstServerLogs.Items.Add("[21:40:10] [SİSTEM] Sunucu 5000 portunda dinlemeye başladı.");
            _lstServerLogs.Items.Add("[21:40:12] [SİSTEM] 'Zehra' sohbet odasına katıldı (127.0.0.1:52140).");
            _lstServerLogs.Items.Add("[21:40:18] [SİSTEM] 'Ahmet' sohbet odasına katıldı (127.0.0.1:52144).");
            _lstServerLogs.Items.Add("[21:40:25] [SİSTEM] 'Elif' sohbet odasına katıldı (127.0.0.1:52149).");
            _lstServerLogs.Items.Add("[21:40:32] [SİSTEM] Zehra: Merhaba arkadaşlar, NTP sohbet odası aktif!");
            _lstServerLogs.Items.Add("[21:40:39] [SİSTEM] Ahmet: Selam Zehra, bağlantı sorunsuz çalışıyor.");
            _lstServerLogs.Items.Add("[21:40:45] [SİSTEM] Elif: Herkese iyi çalışmalar!");

            _lstChatMessages.Items.Clear();
            _lstChatMessages.Items.Add("[21:40:12] [SİSTEM] 'Zehra' sohbet odasına katıldı (127.0.0.1:52140).");
            _lstChatMessages.Items.Add("[21:40:18] [SİSTEM] 'Ahmet' sohbet odasına katıldı (127.0.0.1:52144).");
            _lstChatMessages.Items.Add("[21:40:25] [SİSTEM] 'Elif' sohbet odasına katıldı (127.0.0.1:52149).");
            _lstChatMessages.Items.Add("[21:40:32] Zehra: Merhaba arkadaşlar, NTP sohbet odası aktif!");
            _lstChatMessages.Items.Add("[21:40:39] Ahmet: Selam Zehra, bağlantı sorunsuz çalışıyor.");
            _lstChatMessages.Items.Add("[21:40:45] Elif: Herkese iyi çalışmalar!");

            _cmbRecipient.Items.Clear();
            _cmbRecipient.Items.AddRange(new object[] { "Herkes (Genel Oda)", "Ahmet", "Elif" });
            _cmbRecipient.SelectedIndex = 0;
            _txtMessageInput.Text = "Teşekkürler, proje testlerini tamamlıyoruz.";

            Refresh();
            SaveSnapshot(Path.Combine(outputDirectory, "chatapp_active_room.png"));

            // 2. Ekran Görüntüsü: Özel Mesajlaşma (DM) ve Sunucu Yönlendirme Günlüğü
            _cmbRecipient.SelectedIndex = 1; // Ahmet seçili
            _lstServerLogs.Items.Add("[21:41:05] [SİSTEM] [ÖZEL] Zehra -> Ahmet: Ahmet, sunucu rapor dosyasını kontrol ettin mi?");
            _lstServerLogs.Items.Add("[21:41:14] [SİSTEM] [ÖZEL] Ahmet -> Zehra: Evet Zehra, StreamWriter çıktısı gayet düzenli.");

            _lstChatMessages.Items.Add("[21:41:05] [ÖZEL: Zehra -> Ahmet] Ahmet, sunucu rapor dosyasını kontrol ettin mi?");
            _lstChatMessages.Items.Add("[21:41:14] [ÖZEL: Ahmet -> Zehra] Evet Zehra, StreamWriter çıktısı gayet düzenli.");
            _txtMessageInput.Text = "@Elif Toplantı saat 22:30'da başlayacak.";

            Refresh();
            SaveSnapshot(Path.Combine(outputDirectory, "chatapp_private_dm.png"));
        }

        private void SaveSnapshot(string filePath)
        {
            using var bmp = new Bitmap(Width, Height);
            DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
            bmp.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}
