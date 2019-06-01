namespace betten.WebsocketHandler.Messages
{
    public class CommandMessage
    {
        public string Command { get; set; }
        public object[] Parameters { get; set; }
    }
}