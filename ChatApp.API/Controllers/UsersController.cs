using ChatApp.API.Data;
using ChatApp.API.DTOs;
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
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;

        public UsersController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchUsers([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email query is required.");

            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst(JwtRegisteredClaimNames.Sub);
            var currentUserId = int.Parse(claim?.Value ?? "0");

            var users = await _db.Users
                .Where(u => u.Id != currentUserId && u.Email.Contains(email))
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Username = u.Username,
                    IsOnline = ChatHub.IsUserOnline(u.Id)
                })
                .Take(10)
                .ToListAsync();

            return Ok(users);
        }
    }
}
