using GeoSlayer.Domain.DTOs.Auth.Requests;
using GeoSlayer.Domain.Interfaces.Api;
using Microsoft.AspNetCore.Mvc;

namespace GeoSlayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        await authService.RegisterUser(request);
        return Ok();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginUserRequest request)
    {
        var response = await authService.LoginUser(request);
        return Ok(response);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var response = await authService.RefreshToken(request.RefreshToken);
        return Ok(response);
    }
}
