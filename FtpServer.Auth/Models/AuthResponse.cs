namespace FtpServer.Auth.Models;

public class AuthResponse
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = "";
    public User? User { get; set; }
}
