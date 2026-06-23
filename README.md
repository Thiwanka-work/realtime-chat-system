# 💬 SecureChat — Real-Time Chat System
### ASP.NET Core .NET 8 + SignalR + JWT Authentication

A full-stack real-time chat application with secure JWT-based authentication,
SignalR WebSocket messaging, SQL Server persistence, and a vanilla HTML/CSS/JS frontend.

---

## 📁 Project Structure

```
realtime-chat-system/
└── ChatApp.API/
    ├── Controllers/        AuthController.cs
    ├── Data/               AppDbContext.cs
    ├── DTOs/               RegisterDto, LoginDto, AuthResponseDto
    ├── Hubs/               ChatHub.cs  (SignalR, [Authorize])
    ├── Migrations/         EF Core migrations
    ├── Models/             User.cs, Message.cs
    ├── Services/           IAuthService, AuthService (BCrypt + JWT)
    ├── wwwroot/
    │   ├── index.html      Login page
    │   ├── register.html   Register page
    │   ├── chat.html       Chat room
    │   ├── css/style.css
    │   └── js/             auth.js, chat.js
    ├── appsettings.json
    └── Program.cs
```

---

## ✅ Prerequisites

| Tool | Version | Download |
|------|---------|----------|
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download/dotnet/8.0 |
| SQL Server | Any (LocalDB / Express / Full) | https://www.microsoft.com/en-us/sql-server/sql-server-downloads |

> **Tip:** SQL Server LocalDB is bundled with Visual Studio and requires zero setup.
> The default connection string in `appsettings.json` already uses LocalDB.

---

## 🚀 Step-by-Step Run Instructions

### Step 1 — Install .NET 8 SDK

Download and install from: https://dotnet.microsoft.com/download/dotnet/8.0

Verify:
```powershell
dotnet --version
# Should print: 8.x.x
```

---

### Step 2 — Restore NuGet packages

```powershell
cd ChatApp.API
dotnet restore
```

---

### Step 3 — Configure the Database

Open `ChatApp.API/appsettings.json` and check the connection string:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ChatAppDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

**If using SQL Server Express**, change to:
```json
"DefaultConnection": "Server=.\\SQLEXPRESS;Database=ChatAppDb;Trusted_Connection=True;TrustServerCertificate=True"
```

**If using full SQL Server**, change to:
```json
"DefaultConnection": "Server=YOUR_SERVER_NAME;Database=ChatAppDb;Trusted_Connection=True;TrustServerCertificate=True"
```

---

### Step 4 — Apply Database Migrations

The app **auto-applies migrations on startup**, so this step is optional.
But you can also run manually:

```powershell
cd ChatApp.API
dotnet ef database update
```

---

### Step 5 — Run the Application

```powershell
cd ChatApp.API
dotnet run
```

You will see output like:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

---

### Step 6 — Open in Browser

Navigate to: **http://localhost:5000**

You'll land on the **Login** page.

---

## 🧪 Testing the App

### 1. Register a new user
- Click **"Create one"** link on the login page
- Fill in username, email, password → Submit
- Auto-redirects to chat room

### 2. Send messages
- Type in the chat box → press **Enter** or click **Send**
- Messages appear in real time with sender name + timestamp

### 3. Test multi-user real-time
- Open a **second browser tab** (or incognito window)
- Register/login as a different user
- Messages from either window appear in both — instantly

### 4. Test JWT protection
- Clear localStorage (`localStorage.clear()` in browser console)
- Refresh chat page → should redirect to login automatically

---

## 🔐 Security Notes

| Feature | Implementation |
|---------|---------------|
| Password Storage | BCrypt hashing (BCrypt.Net-Next) |
| Authentication | JWT Bearer tokens (HS256) |
| Token Expiry | 24 hours (configurable in `appsettings.json`) |
| Hub Protection | `[Authorize]` attribute on ChatHub |
| SignalR Token | Passed via query string (`?access_token=...`) — required for WebSocket |
| SQL Injection | EF Core parameterized queries |
| XSS | All user content HTML-escaped in JS |

---

## ⚙️ Configuration Reference (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."           // SQL Server connection string
  },
  "Jwt": {
    "Key": "...",                        // Secret signing key (change in production!)
    "Issuer": "ChatApp",
    "Audience": "ChatAppUsers",
    "ExpiryMinutes": 1440                // Token lifetime (default: 24 hours)
  }
}
```

---

## 🗄️ Database Schema

### Users
| Column | Type | Notes |
|--------|------|-------|
| Id | int | PK, auto-increment |
| Username | nvarchar(50) | UNIQUE |
| Email | nvarchar(100) | UNIQUE |
| PasswordHash | nvarchar(max) | BCrypt hash |
| CreatedAt | datetime2 | UTC |

### Messages
| Column | Type | Notes |
|--------|------|-------|
| Id | int | PK, auto-increment |
| Sender | nvarchar(50) | Username |
| Content | nvarchar(max) | Message text |
| Timestamp | datetime2 | UTC |

---

## 🌐 API Endpoints

| Method | URL | Auth | Description |
|--------|-----|------|-------------|
| POST | `/api/auth/register` | ❌ | Register new user |
| POST | `/api/auth/login` | ❌ | Login, returns JWT |
| WS | `/chatHub` | ✅ JWT | SignalR hub |

### Register Request Body
```json
{ "username": "alice", "email": "alice@example.com", "password": "secret123" }
```

### Login Request Body
```json
{ "username": "alice", "password": "secret123" }
```

### Auth Response
```json
{
  "token": "eyJhbGci...",
  "username": "alice",
  "email": "alice@example.com",
  "expiresAt": "2024-01-02T00:00:00Z"
}
```

---

## 🐛 Troubleshooting

| Problem | Solution |
|---------|----------|
| `dotnet` not found | Install .NET 8 SDK and restart terminal |
| DB connection failed | Check SQL Server is running; verify connection string |
| Port 5000 in use | Change port: `dotnet run --urls http://localhost:5050` |
| CORS error | Ensure you're accessing via `http://localhost:5000`, not `file://` |
| SignalR 401 Unauthorized | JWT may be expired — logout and login again |

---

## 🏗️ Architecture

```
Browser (HTML/CSS/JS)
    │
    ├── POST /api/auth/login  ──→  AuthController → AuthService (BCrypt + JWT)
    ├── POST /api/auth/register ─→ AuthController → AuthService
    │
    └── WebSocket /chatHub?access_token=JWT
             │
             ▼
         ChatHub [Authorize]
             │
             ├── OnConnectedAsync  → Load history, notify users
             ├── SendMessage()     → Save to DB, broadcast to all
             └── OnDisconnectedAsync → Notify users
                      │
                      ▼
               SQL Server (EF Core)
               ├── Users table
               └── Messages table
```
