using FtpServer.Auth.Models;

namespace FtpServer.Auth.Services;

public class UserService : IUserService
{
    // simple In-Memory database users
    private readonly List<User> users = new()
    {
        new User { Id=1, Username="demo", PasswordHash="1", Email="demo@example.com", IsActive=true},
        new User { Id=2, Username="test", PasswordHash="1", Email="test@example.com", IsActive=true},
        new User { Id=3, Username="admin", PasswordHash="123", Email="admin@example.com", IsActive=true}
    };
    public Task<AuthResponse> AuthenticateAsync(AuthRequest request)
    {
        var user = users.FirstOrDefault(u => u.Username == request.Username);

        if (user == null)
        {
            return Task.FromResult(new AuthResponse
            {
                IsSuccess = false,
                Message = "User not found"
            });
        }

        // simple verify password (without hash)
        if (user.PasswordHash == request.Password)
        {
            return Task.FromResult(new AuthResponse
            {
                IsSuccess = true,
                Message = "Authentication successful"
            });
        }

        return Task.FromResult(new AuthResponse
        {
            IsSuccess = false,
            Message = "Invalid password"
        });
    }

    public Task<User?> GetUserByUsernameAsync(string username)
    {        
        var user = users.FirstOrDefault(u => u.Username == username);        
        return Task.FromResult(user);
    }
}
