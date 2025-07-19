using FtpServer.Auth.Models;
using Microsoft.AspNetCore.Mvc;

namespace FtpServer.Auth.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController: ControllerBase
{
    [HttpGet("info")]
    public IActionResult GetInfo()
    {
        return Ok(new
        {
            Service = "FTP Authentication Service",
            Version = "1.0.0",
            Status = "Running",
            Time = DateTime.UtcNow
        });
    }

    [HttpPost("authenticate")]
    public IActionResult Authenticate([FromBody] AuthRequest authRequest)
    {
        // While simple verify
        if (authRequest.Username == "demo" || authRequest.Username == "test")
        {
            var user = new User
            {
                Id = 1,
                Username = "demo",
                Email = "demo@example.com",
                IsActive = true
            };

            return Ok(new AuthResponse
            {
                IsSuccess = true,
                User = user,
                Message = "Authentication successful"
            });
        }

        return BadRequest(new AuthResponse
       {
            IsSuccess=false,
            Message = "Invalid credentials"
        });
    }
}
