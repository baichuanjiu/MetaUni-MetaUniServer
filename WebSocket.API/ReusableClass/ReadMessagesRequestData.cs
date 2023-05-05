namespace WebSocket.API.ReusableClass
{
    public class ReadMessagesRequestData
    {
        public ReadMessagesRequestData(int UUID, string JWT, int chatId)
        {
            this.UUID = UUID;
            this.JWT = JWT;
            ChatId = chatId;
        }

        public int UUID { get; set; }
        public string JWT { get; set; }
        public int ChatId { get; set; }
    }
}
