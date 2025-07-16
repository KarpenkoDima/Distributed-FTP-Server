# MVP FTP Server - План реализации

## 🎯 Scope MVP

### Что делаем сами:
- ✅ **FTPS Server** (TcpListener + SslStream)
- ✅ **Authentication Service** (ASP.NET Core Web API)  
- ✅ **File Upload/Download**
- ✅ **Basic Logging** (Serilog)
- ✅ **Rate Limiting** (AspNetCoreRateLimit)

### Что используем готовое:
- ✅ **nginx** (load balancer)
- ✅ **Redis** (session storage)
- ✅ **Distributed FS** (GlusterFS или обычная shared folder для начала)

## 🏗️ Архитектура MVP

```
[FTP Client]
    ↓ FTPS (port 2121)
[nginx] → [FTP Server .NET]
              ↓
    [Auth API] ← HTTP
              ↓  
         [Redis] ← Sessions
              ↓
    [Shared Storage] ← Files
```

## 📂 Структура проекта

```
FtpServerMVP/
├── FtpServer.Core/           # Основной FTP сервер
├── FtpServer.Auth/           # Authentication API  
├── FtpServer.Contracts/      # Shared models/interfaces
├── FtpServer.Tests/          # Unit tests
└── docker-compose.yml       # Infrastructure
```

## 🔧 Компоненты для реализации

### 1. FTP Server Core (.NET 8)

**Основные классы:**
```csharp
// Program.cs
public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        
        builder.Services.AddSingleton<IFtpServer, FtpServer>();
        builder.Services.AddSingleton<ISessionManager, RedisSessionManager>();
        builder.Services.AddHttpClient<IAuthService, AuthService>();
        builder.Services.AddSingleton<IRateLimiter, RedisRateLimiter>();
        
        var host = builder.Build();
        
        var ftpServer = host.Services.GetRequiredService<IFtpServer>();
        await ftpServer.StartAsync(2121); // Non-standard port для разработки
        
        await host.RunAsync();
    }
}

// Core FTP Server
public class FtpServer : IFtpServer
{
    private TcpListener _listener;
    private readonly ISessionManager _sessionManager;
    private readonly IAuthService _authService;
    private readonly IRateLimiter _rateLimiter;
    private readonly ILogger<FtpServer> _logger;
    
    public async Task StartAsync(int port)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        
        _logger.LogInformation("FTP Server started on port {Port}", port);
        
        while (true)
        {
            var tcpClient = await _listener.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleClientAsync(tcpClient));
        }
    }
    
    private async Task HandleClientAsync(TcpClient client)
    {
        var clientEndpoint = client.Client.RemoteEndPoint?.ToString();
        _logger.LogInformation("New client connected: {ClientEndpoint}", clientEndpoint);
        
        try
        {
            // Rate limiting check
            var clientIP = ((IPEndPoint)client.Client.RemoteEndPoint!).Address;
            if (!await _rateLimiter.IsAllowedAsync(clientIP))
            {
                _logger.LogWarning("Rate limit exceeded for {ClientIP}", clientIP);
                client.Close();
                return;
            }
            
            using var sslStream = new SslStream(client.GetStream());
            
            // TLS handshake
            await sslStream.AuthenticateAsServerAsync(_serverCertificate);
            
            var session = new FtpSession
            {
                SessionId = Guid.NewGuid().ToString(),
                ClientIP = clientIP,
                SslStream = sslStream
            };
            
            await _sessionManager.SaveSessionAsync(session);
            
            // Send welcome message
            await SendResponseAsync(sslStream, "220 Welcome to MVP FTP Server");
            
            // Command processing loop
            await ProcessCommandsAsync(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client {ClientEndpoint}", clientEndpoint);
        }
        finally
        {
            client.Close();
        }
    }
}

// FTP Command Handler
public class FtpCommandHandler
{
    public async Task<FtpResponse> HandleAsync(string command, FtpSession session)
    {
        var parts = command.Split(' ', 2);
        var cmd = parts[0].ToUpper();
        var args = parts.Length > 1 ? parts[1] : "";
        
        return cmd switch
        {
            "USER" => await HandleUserAsync(args, session),
            "PASS" => await HandlePassAsync(args, session),
            "PWD" => HandlePwd(session),
            "CWD" => await HandleCwdAsync(args, session),
            "LIST" => await HandleListAsync(session),
            "STOR" => await HandleStorAsync(args, session),
            "RETR" => await HandleRetrAsync(args, session),
            "QUIT" => new FtpResponse(221, "Goodbye"),
            _ => new FtpResponse(502, "Command not implemented")
        };
    }
    
    private async Task<FtpResponse> HandleUserAsync(string username, FtpSession session)
    {
        session.Username = username;
        await _sessionManager.SaveSessionAsync(session);
        return new FtpResponse(331, "Password required");
    }
    
    private async Task<FtpResponse> HandlePassAsync(string password, FtpSession session)
    {
        var authResult = await _authService.AuthenticateAsync(session.Username, password);
        
        if (authResult.IsSuccess)
        {
            session.IsAuthenticated = true;
            session.User = authResult.User;
            await _sessionManager.SaveSessionAsync(session);
            
            _logger.LogInformation("User {Username} authenticated successfully", session.Username);
            return new FtpResponse(230, "Login successful");
        }
        
        _logger.LogWarning("Authentication failed for user {Username}", session.Username);
        return new FtpResponse(530, "Login incorrect");
    }
}
```

### 2. Authentication Service (ASP.NET Core)

```csharp
// Controllers/AuthController.cs
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<AuthController> _logger;
    
    [HttpPost("authenticate")]
    public async Task<IActionResult> Authenticate([FromBody] LoginRequest request)
    {
        try
        {
            var user = await _userRepository.GetByUsernameAsync(request.Username);
            
            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning("Authentication failed for user {Username}", request.Username);
                return Unauthorized(new { IsSuccess = false, Message = "Invalid credentials" });
            }
            
            _logger.LogInformation("User {Username} authenticated successfully", request.Username);
            
            return Ok(new AuthResult
            {
                IsSuccess = true,
                User = new UserDto
                {
                    Username = user.Username,
                    HomeDirectory = user.HomeDirectory,
                    Permissions = user.Permissions
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication for user {Username}", request.Username);
            return StatusCode(500, new { IsSuccess = false, Message = "Internal server error" });
        }
    }
    
    [HttpGet("user/{username}")]
    public async Task<IActionResult> GetUser(string username)
    {
        var user = await _userRepository.GetByUsernameAsync(username);
        return user == null ? NotFound() : Ok(user);
    }
    
    private bool VerifyPassword(string password, string hash)
    {
        // Используем BCrypt для хеширования паролей
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}

// Program.cs для Auth Service
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Rate limiting
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

// Services
builder.Services.AddScoped<IUserRepository, InMemoryUserRepository>(); // Потом заменим на реальную БД

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseIpRateLimiting(); // Rate limiting middleware

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### 3. Infrastructure (docker-compose.yml)

```yaml
version: '3.8'

services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data

  nginx:
    image: nginx:alpine
    ports:
      - "21:21"
      - "2121:2121"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf
    depends_on:
      - ftp-server

  auth-service:
    build:
      context: .
      dockerfile: FtpServer.Auth/Dockerfile
    ports:
      - "5001:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
    depends_on:
      - redis

  ftp-server:
    build:
      context: .
      dockerfile: FtpServer.Core/Dockerfile
    ports:
      - "2121:2121"
    environment:
      - AuthService__BaseUrl=http://auth-service
      - Redis__ConnectionString=redis:6379
    volumes:
      - ftp_storage:/app/storage
    depends_on:
      - redis
      - auth-service

volumes:
  redis_data:
  ftp_storage:
```

## 🚀 План разработки по неделям

### Неделя 1: Core FTP Server
- [ ] TcpListener + basic command parsing
- [ ] USER/PASS commands
- [ ] PWD/CWD commands  
- [ ] Basic logging setup

### Неделя 2: File Operations
- [ ] STOR command (file upload)
- [ ] RETR command (file download)
- [ ] LIST command (directory listing)
- [ ] Data connection management

### Неделя 3: FTPS + Auth Integration
- [ ] SslStream integration
- [ ] Certificate management
- [ ] HTTP client для Auth Service
- [ ] Session management с Redis

### Неделя 4: Infrastructure + Testing
- [ ] Rate limiting
- [ ] nginx configuration
- [ ] docker-compose setup
- [ ] Integration testing

## 🧪 Тестирование

```bash
# Тестируем с FileZilla или командной строкой
ftp -s -A ftps://localhost:2121

# Проверяем rate limiting
for i in {1..100}; do curl http://localhost:5001/api/auth/authenticate; done
```

---

Готов начинать? С какого компонента хотите стартовать? 🚀