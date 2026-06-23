using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp.API.Models
{
    public class Chat
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int User1Id { get; set; }
        
        [ForeignKey("User1Id")]
        public User? User1 { get; set; }

        [Required]
        public int User2Id { get; set; }

        [ForeignKey("User2Id")]
        public User? User2 { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
