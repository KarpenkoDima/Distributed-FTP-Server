namespace FtpServer.Commons.Models;

public class AuthRequest
{
    public bool IsSuccess { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Message { get; set; } = "";
}

