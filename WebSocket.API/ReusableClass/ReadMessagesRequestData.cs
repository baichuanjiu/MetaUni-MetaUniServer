namespace WebSocket.API.ReusableClass
{
    public class ReadMessagesRequestData
    {
        public ReadMessagesRequestData(int chatId)
        {
            ChatId = chatId;
        }

        public int ChatId { get; set; }
    }
}
