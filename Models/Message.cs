namespace Alejandria.Server.Models
{
    public class Message
    {
        public string Role { get; set; }
        public string Content { get; set; }

        // Campos opcionales, manejados en el backend
        public long TimeStamp { get; private set; }
        public bool IsRequest { get; private set; }

        // Constructor modificado para manejar 'Role' y 'Content'
        public Message(string role, string content)
        {
            Role = role;
            Content = content;
            TimeStamp = ((DateTimeOffset)DateTime.Now).ToUnixTimeMilliseconds();
            IsRequest = role.Equals("user", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class ChatRequest
    {
        public required string Query { get; set; }
        public required List<Message> History { get; set; }
    }
}
