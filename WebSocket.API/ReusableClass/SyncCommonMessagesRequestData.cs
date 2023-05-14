namespace WebSocket.API.ReusableClass
{
    public class SyncCommonMessagesRequestData
    {
        public SyncCommonMessagesRequestData(int sequence)
        {
            Sequence = sequence;
        }

        public int Sequence { get; set; }
    }
}
