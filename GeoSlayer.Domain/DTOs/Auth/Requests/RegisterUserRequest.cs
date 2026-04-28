namespace GeoSlayer.Domain.DTOs.Auth.Requests
{
    public class RegisterUserRequest
    {
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string FirstName { get; set; }
        public required string Email { get; set; }
    }
}
