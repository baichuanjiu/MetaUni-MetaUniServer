namespace WebSocket.API.ReusableClass
{
    public class CommonMessageDataForClient
    {
        public CommonMessageDataForClient(int id, int chatId, int senderId, int receiverId, DateTime createdTime, bool isCustom, bool isRecalled, bool isDeleted, bool isReply, bool isMediaMessage, bool isVoiceMessage, string? customType, string? minimumSupportVersion, string? textOnError, string? customMessageContent, int? messageReplied, string? messageText, string? messageMedias, string? messageVoice, int sequence)
        {
            Id = id;
            ChatId = chatId;
            SenderId = senderId;
            ReceiverId = receiverId;
            CreatedTime = createdTime;
            IsCustom = isCustom;
            IsRecalled = isRecalled;
            IsDeleted = isDeleted;
            IsReply = isReply;
            IsMediaMessage = isMediaMessage;
            IsVoiceMessage = isVoiceMessage;
            CustomType = customType;
            MinimumSupportVersion = minimumSupportVersion;
            TextOnError = textOnError;
            CustomMessageContent = customMessageContent;
            MessageReplied = messageReplied;
            MessageText = messageText;
            MessageMedias = messageMedias;
            MessageVoice = messageVoice;
            Sequence = sequence;
        }

        public int Id { get; set; }
        public int ChatId { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public DateTime CreatedTime { get; set; }
        public bool IsCustom { get; set; }
        public bool IsRecalled { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsReply { get; set; }
        public bool IsMediaMessage { get; set; }
        public bool IsVoiceMessage { get; set; }
        public string? CustomType { get; set; }
        public string? MinimumSupportVersion { get; set; }
        public string? TextOnError { get; set; }
        public string? CustomMessageContent { get; set; }
        public int? MessageReplied { get; set; }
        public string? MessageText { get; set; }
        public string? MessageMedias { get; set; }
        public string? MessageVoice { get; set; }
        public int Sequence { get; set; }
    }
}
