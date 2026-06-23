using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ChatApp.API.Data;
using ChatApp.API.DTOs;
using ChatApp.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ChatApp.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public AuthService(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            // Check if username already exists
            bool usernameExists = await _db.Users.AnyAsync(u => u.Username == dto.Username);
            if (usernameExists)
                throw new InvalidOperationException("Username is already taken.");

            // Check if email already exists
            bool emailExists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
            if (emailExists)
                throw new InvalidOperationException("Email is already registered.");

            // Hash password using BCrypt
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var user = new User
            {
                Username    = dto.Username,
                Email       = dto.Email,
                PasswordHash = passwordHash,
                CreatedAt   = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return GenerateAuthResponse(user);
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Username == dto.Username);

            if (user == null)
                throw new UnauthorizedAccessException("Invalid username or password.");

            bool passwordValid = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
            if (!passwordValid)
                throw new UnauthorizedAccessException("Invalid username or password.");

            return GenerateAuthResponse(user);
        }

        // ─── JWT Generation ───────────────────────────────────────────────────────

        private AuthResponseDto GenerateAuthResponse(User user)
        {
            var expiryMinutes = _config.GetValue<int>("Jwt:ExpiryMinutes", 1440);
            var expiresAt     = DateTime.UtcNow.AddMinutes(expiryMinutes);
            var token         = GenerateJwtToken(user, expiresAt);

            return new AuthResponseDto
            {
                Token     = token,
                Username  = user.Username,
                Email     = user.Email,
                ExpiresAt = expiresAt
            };
        }

        private string GenerateJwtToken(User user, DateTime expiresAt)
        {
            var jwtKey   = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured.");
            var issuer   = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];

            var keyBytes       = Encoding.UTF8.GetBytes(jwtKey);
            var securityKey    = new SymmetricSecurityKey(keyBytes);
            var credentials    = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Name,               user.Username),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer:             issuer,
                audience:           audience,
                claims:             claims,
                notBefore:          DateTime.UtcNow,
                expires:            expiresAt,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
