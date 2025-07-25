﻿using FtpServer.Auth.Services;
using FtpServer.Commons.Models;
using Microsoft.AspNetCore.Mvc;


namespace FtpServer.Auth.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
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

    [HttpPost("user")]
    public async Task<IActionResult> GetUser([FromBody] AuthRequest authRequest)
    {       
        var user = await _userService.GetUserByUsernameAsync(authRequest.Username);
       
        if (user == null)
            return NotFound(new {Message="User not found"});

        return Ok(user);
    }
}
