using System;

namespace YMH219_ChatApp.Models
{
    /// <summary>
    /// İstemci ve sunucu arasında iletilen sohbet mesajlarını temsil eden OOP sınıfı.
    /// </summary>
    public class ChatMessage
    {
        public DateTime Timestamp { get; set; }
        public ChatMessageType MessageType { get; set; }
        public string Sender { get; set; }
        public string Recipient { get; set; }
        public string Content { get; set; }

        public ChatMessage(
            ChatMessageType messageType,
            string sender,
            string recipient,
            string content,
            DateTime? timestamp = null)
        {
            MessageType = messageType;
            Sender = sender;
            Recipient = string.IsNullOrWhiteSpace(recipient) ? "Herkes" : recipient;
            Content = content;
            Timestamp = timestamp ?? DateTime.Now;
        }

        /// <summary>
        /// Mesajı TCP StreamWriter üzerinden tek satırda göndermek için serileştirir.
        /// </summary>
        public string ToWireString()
        {
            string safeContent = Content.Replace("\r", " ").Replace("\n", " ").Replace("|", "¦");
            return $"{MessageType}|{Timestamp:O}|{Sender}|{Recipient}|{safeContent}";
        }

        /// <summary>
        /// TCP StreamReader üzerinden okunan satırı ChatMessage nesnesine dönüştürür.
        /// </summary>
        public static ChatMessage FromWireString(string wireLine)
        {
            if (string.IsNullOrWhiteSpace(wireLine))
            {
                return new ChatMessage(ChatMessageType.System, "Sistem", "Herkes", string.Empty);
            }

            string[] parts = wireLine.Split('|', 5);
            if (parts.Length < 5)
            {
                return new ChatMessage(ChatMessageType.Public, "Bilinmeyen", "Herkes", wireLine);
            }

            if (!Enum.TryParse(parts[0], out ChatMessageType msgType))
            {
                msgType = ChatMessageType.Public;
            }

            if (!DateTime.TryParse(parts[1], out DateTime parsedTime))
            {
                parsedTime = DateTime.Now;
            }

            return new ChatMessage(
                msgType,
                parts[2],
                parts[3],
                parts[4].Replace("¦", "|"),
                parsedTime);
        }

        /// <summary>
        /// Kullanıcı arayüzünde zaman bilgisiyle gösterilecek metni üretir.
        /// </summary>
        public string ToDisplayString()
        {
            string timeText = Timestamp.ToString("HH:mm:ss");
            return MessageType switch
            {
                ChatMessageType.System => $"[{timeText}] [SİSTEM] {Content}",
                ChatMessageType.Private => $"[{timeText}] [ÖZEL: {Sender} -> {Recipient}] {Content}",
                _ => $"[{timeText}] {Sender}: {Content}"
            };
        }

        public override string ToString() => ToDisplayString();
    }
}
