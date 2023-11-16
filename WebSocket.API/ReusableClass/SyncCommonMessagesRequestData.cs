namespace WebSocket.API.ReusableClass
{
    public class SyncMessagesRequestData
    {
        public SyncMessagesRequestData(int sequence)
        {
            Sequence = sequence;
        }

        public int Sequence { get; set; }
    }
}
