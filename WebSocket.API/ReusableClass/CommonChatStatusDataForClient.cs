namespace WebSocket.API.ReusableClass
{
    public class CommonChatStatusDataForClient
    {
        public CommonChatStatusDataForClient(int chatId, int? lastMessageSendByMe, bool? isRead, DateTime? readTime, DateTime updatedTime)
        {
            ChatId = chatId;
            LastMessageSendByMe = lastMessageSendByMe;
            IsRead = isRead;
            ReadTime = readTime;
            UpdatedTime = updatedTime;
        }

        public int ChatId { get; set; }
        public int? LastMessageSendByMe { get; set; }
        public bool? IsRead { get; set; }
        public DateTime? ReadTime { get; set; }
        public DateTime UpdatedTime { get; set; }
    }
}
