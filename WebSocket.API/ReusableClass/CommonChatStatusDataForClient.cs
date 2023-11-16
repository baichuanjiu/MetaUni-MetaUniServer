namespace WebSocket.API.ReusableClass
{
    public class CommonChatStatusDataForClient
    {
        public CommonChatStatusDataForClient(int chatId, int? lastMessageBeReadSendByMe, DateTime? readTime, DateTime updatedTime)
        {
            ChatId = chatId;
            LastMessageBeReadSendByMe = lastMessageBeReadSendByMe;
            ReadTime = readTime;
            UpdatedTime = updatedTime;
        }

        public int ChatId { get; set; }
        public int? LastMessageBeReadSendByMe { get; set; }
        public DateTime? ReadTime { get; set; }
        public DateTime UpdatedTime { get; set; }
    }
}
