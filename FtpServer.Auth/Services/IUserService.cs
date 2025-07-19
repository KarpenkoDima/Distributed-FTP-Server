using FtpServer.Auth.Models;

namespace FtpServer.Auth.Services;

public interface IUserService
{
    Task<AuthResponse> AuthenticateAsync(AuthRequest request);
    Task<User?> GetUserByUsernameAsync(string username);
}
