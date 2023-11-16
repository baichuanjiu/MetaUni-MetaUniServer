namespace WebSocket.API.ReusableClass
{
    public class ReadMessagesResponseData
    {
        public ReadMessagesResponseData(int chatId)
        {
            ChatId = chatId;
        }

        public int ChatId { get; set; }
    }
}
