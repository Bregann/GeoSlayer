using GeoSlayer.Domain.DTOs.Auth.Requests;
using GeoSlayer.Domain.DTOs.Auth.Responses;

namespace GeoSlayer.Domain.Interfaces.Api.Auth
{
    public interface IAuthService
    {
        Task RegisterUser(RegisterUserRequest request);
        Task<LoginUserResponse> LoginUser(LoginUserRequest request);
        Task<LoginUserResponse> RefreshToken(string refreshToken);
    }
}
