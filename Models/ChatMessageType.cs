namespace YMH219_ChatApp.Models
{
    /// <summary>
    /// Ağ üzerinden gönderilen mesajın türünü tanımlar.
    /// </summary>
    public enum ChatMessageType
    {
        System,           // Sistem / Sunucu bilgilendirme mesajı
        Public,           // Odadaki tüm kullanıcılara açık genel mesaj
        Private,          // Belirli bir kullanıcıya gönderilen özel mesaj (DM)
        ClientListUpdate  // Bağlı kullanıcı listesini senkronize eden kontrol paketi
    }
}
