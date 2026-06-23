using ChatApp.API.Data;
using ChatApp.API.Models;
using ChatApp.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace ChatApp.API.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly AppDbContext _db;
        private readonly ILogger<ChatHub> _logger;

        // Map UserId to a HashSet of ConnectionIds
        private static readonly ConcurrentDictionary<int, HashSet<string>> _userConnections = new();

        public ChatHub(AppDbContext db, ILogger<ChatHub> logger)
        {
            _db     = db;
            _logger = logger;
        }

        private int GetUserId()
        {
            var subClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                        ?? Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (int.TryParse(subClaim, out int userId))
            {
                return userId;
            }
            throw new HubException("Invalid User ID in token.");
        }

        public static bool IsUserOnline(int userId)
        {
            return _userConnections.TryGetValue(userId, out var connections) && connections.Count > 0;
        }

        public override async Task OnConnectedAsync()
        {
            int userId = GetUserId();
            var connectionId = Context.ConnectionId;

            _userConnections.AddOrUpdate(userId,
                _ => new HashSet<string> { connectionId },
                (_, connections) =>
                {
                    lock (connections) { connections.Add(connectionId); }
                    return connections;
                });

            _logger.LogInformation("User {UserId} connected with ConnectionId {ConnectionId}", userId, connectionId);

            // Notify all clients that this user is online
            await Clients.All.SendAsync("UserOnlineStatus", userId, true);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            int userId = GetUserId();
            var connectionId = Context.ConnectionId;

            if (_userConnections.TryGetValue(userId, out var connections))
            {
                bool isEmpty = false;
                lock (connections)
                {
                    connections.Remove(connectionId);
                    isEmpty = connections.Count == 0;
                }

                if (isEmpty)
                {
                    _userConnections.TryRemove(userId, out _);
                    // Notify all clients that this user is offline
                    await Clients.All.SendAsync("UserOnlineStatus", userId, false);
                }
            }

            _logger.LogInformation("User {UserId} disconnected (ConnectionId {ConnectionId})", userId, connectionId);

            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendMessage(int chatId, string content)
        {
            int senderId = GetUserId();

            if (string.IsNullOrWhiteSpace(content))
                throw new HubException("Message content cannot be empty.");

            var chat = await _db.Chats.FirstOrDefaultAsync(c => c.Id == chatId);
            if (chat == null || (chat.User1Id != senderId && chat.User2Id != senderId))
                throw new HubException("Chat not found or unauthorized.");

            int receiverId = chat.User1Id == senderId ? chat.User2Id : chat.User1Id;

            var message = new Message
            {
                ChatId = chatId,
                SenderId = senderId,
                Content = content.Trim(),
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            var messageDto = new MessageDto
            {
                Id = message.Id,
                ChatId = message.ChatId,
                SenderId = message.SenderId,
                Content = message.Content,
                SentAt = message.SentAt,
                IsRead = message.IsRead
            };

            // Send to sender's other connections
            if (_userConnections.TryGetValue(senderId, out var senderConnections))
            {
                lock (senderConnections)
                {
                    Clients.Clients(senderConnections.ToList()).SendAsync("ReceiveMessage", messageDto);
                }
            }

            // Send to receiver's connections
            if (_userConnections.TryGetValue(receiverId, out var receiverConnections))
            {
                lock (receiverConnections)
                {
                    Clients.Clients(receiverConnections.ToList()).SendAsync("ReceiveMessage", messageDto);
                }
            }
        }

        public async Task Typing(int chatId)
        {
            int senderId = GetUserId();

            var chat = await _db.Chats.FirstOrDefaultAsync(c => c.Id == chatId);
            if (chat == null || (chat.User1Id != senderId && chat.User2Id != senderId))
                return;

            int receiverId = chat.User1Id == senderId ? chat.User2Id : chat.User1Id;

            if (_userConnections.TryGetValue(receiverId, out var receiverConnections))
            {
                lock (receiverConnections)
                {
                    Clients.Clients(receiverConnections.ToList()).SendAsync("UserTyping", chatId, senderId);
                }
            }
        }

        public async Task MarkAsRead(int chatId)
        {
            int currentUserId = GetUserId();

            var chat = await _db.Chats.FirstOrDefaultAsync(c => c.Id == chatId);
            if (chat == null || (chat.User1Id != currentUserId && chat.User2Id != currentUserId))
                return;

            var unreadMessages = await _db.Messages
                .Where(m => m.ChatId == chatId && m.SenderId != currentUserId && !m.IsRead)
                .ToListAsync();

            if (unreadMessages.Any())
            {
                foreach (var msg in unreadMessages)
                {
                    msg.IsRead = true;
                }
                await _db.SaveChangesAsync();

                int otherUserId = chat.User1Id == currentUserId ? chat.User2Id : chat.User1Id;

                // Notify sender that their messages were read
                if (_userConnections.TryGetValue(otherUserId, out var otherUserConns))
                {
                    lock (otherUserConns)
                    {
                        Clients.Clients(otherUserConns.ToList()).SendAsync("MessagesRead", chatId, currentUserId);
                    }
                }
            }
        }
    }
}
