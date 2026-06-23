namespace ChatApp.API.DTOs
{
    public class ChatDto
    {
        public int Id { get; set; }
        public UserDto OtherUser { get; set; } = null!;
        public MessageDto? LastMessage { get; set; }
        public int UnreadCount { get; set; }
    }
}
