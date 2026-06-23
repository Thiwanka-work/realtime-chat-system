using ChatApp.API.Data;
using ChatApp.API.DTOs;
using ChatApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.API.Hubs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ChatApp.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ChatsController(AppDbContext db)
        {
            _db = db;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst(JwtRegisteredClaimNames.Sub);
            return int.Parse(claim?.Value ?? "0");
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentChats()
        {
            var userId = GetCurrentUserId();

            var chats = await _db.Chats
                .Include(c => c.User1)
                .Include(c => c.User2)
                .Where(c => c.User1Id == userId || c.User2Id == userId)
                .ToListAsync();

            var chatDtos = new List<ChatDto>();

            foreach (var chat in chats)
            {
                var otherUser = chat.User1Id == userId ? chat.User2! : chat.User1!;
                
                var lastMessage = await _db.Messages
                    .Where(m => m.ChatId == chat.Id)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                var unreadCount = await _db.Messages
                    .Where(m => m.ChatId == chat.Id && m.SenderId != userId && !m.IsRead)
                    .CountAsync();

                chatDtos.Add(new ChatDto
                {
                    Id = chat.Id,
                    OtherUser = new UserDto
                    {
                        Id = otherUser.Id,
                        Email = otherUser.Email,
                        Username = otherUser.Username,
                        IsOnline = ChatHub.IsUserOnline(otherUser.Id)
                    },
                    LastMessage = lastMessage != null ? new MessageDto
                    {
                        Id = lastMessage.Id,
                        ChatId = lastMessage.ChatId,
                        SenderId = lastMessage.SenderId,
                        Content = lastMessage.Content,
                        SentAt = lastMessage.SentAt,
                        IsRead = lastMessage.IsRead
                    } : null,
                    UnreadCount = unreadCount
                });
            }

            // Sort by last message sent date
            chatDtos = chatDtos.OrderByDescending(c => c.LastMessage?.SentAt ?? DateTime.MinValue).ToList();

            return Ok(chatDtos);
        }

        [HttpPost("with/{otherUserId}")]
        public async Task<IActionResult> GetOrCreateChat(int otherUserId)
        {
            var userId = GetCurrentUserId();

            if (userId == otherUserId)
                return BadRequest("Cannot create a chat with yourself.");

            var chat = await _db.Chats
                .FirstOrDefaultAsync(c => 
                    (c.User1Id == userId && c.User2Id == otherUserId) || 
                    (c.User1Id == otherUserId && c.User2Id == userId));

            if (chat == null)
            {
                chat = new Chat
                {
                    User1Id = userId,
                    User2Id = otherUserId,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Chats.Add(chat);
                await _db.SaveChangesAsync();
            }

            return Ok(new { ChatId = chat.Id });
        }

        [HttpGet("{chatId}/messages")]
        public async Task<IActionResult> GetMessages(int chatId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = GetCurrentUserId();

            var chat = await _db.Chats.FirstOrDefaultAsync(c => c.Id == chatId);
            if (chat == null || (chat.User1Id != userId && chat.User2Id != userId))
                return Forbid();

            var messages = await _db.Messages
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    ChatId = m.ChatId,
                    SenderId = m.SenderId,
                    Content = m.Content,
                    SentAt = m.SentAt,
                    IsRead = m.IsRead
                })
                .ToListAsync();

            messages.Reverse(); // Return oldest to newest for UI
            return Ok(messages);
        }
    }
}
