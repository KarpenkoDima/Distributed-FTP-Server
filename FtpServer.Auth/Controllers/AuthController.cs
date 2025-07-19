using FtpServer.Auth.Models;
using FtpServer.Auth.Services;
using Microsoft.AspNetCore.Mvc;

namespace FtpServer.Auth.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController: ControllerBase
{

    private IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

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
    public async Task<IActionResult> Authenticate([FromBody] AuthRequest authRequest)
    {
       var result = await _userService.AuthenticateAsync(authRequest);
        return Ok(result);
    }

    [HttpGet("user/{username}")]
    public async Task<IActionResult> GetUser(string username)
    {
        var user = await _userService.GetUserByUsernameAsync(username);

        if (user == null)
            return NotFound(new {Message="User not found"});

        return Ok(user);
    }
}
