using GeoSlayer.Domain.DTOs.Auth.Requests;
using GeoSlayer.Domain.Interfaces.Api;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace GeoSlayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
    {
        try
        {
            await authService.RegisterUser(request);
            return Ok();
        }
        catch (System.Data.DuplicateNameException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Registration failed");
            return StatusCode(500, new { message = "Registration failed" });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginUserRequest request)
    {
        try
        {
            var response = await authService.LoginUser(request);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return Unauthorized(new { message = "Invalid username or password" });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Invalid username or password" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Login failed");
            return StatusCode(500, new { message = "Login failed" });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var response = await authService.RefreshToken(request.RefreshToken);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return Unauthorized(new { message = "Invalid refresh token" });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Refresh token expired" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Token refresh failed");
            return StatusCode(500, new { message = "Token refresh failed" });
        }
    }
}
