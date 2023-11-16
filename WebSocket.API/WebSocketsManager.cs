namespace WebSocket.API
{
    public class WebSocketsManager
    {
        public Dictionary<int, Dictionary<string, System.Net.WebSockets.WebSocket>> webSockets = new();
    }
}
