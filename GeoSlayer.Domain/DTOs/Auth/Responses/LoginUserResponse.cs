namespace GeoSlayer.Domain.DTOs.Auth.Responses
{
    public class LoginUserResponse
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
        public int PlayerId { get; set; }
        public string Username { get; set; } = "";
        public int Level { get; set; }
        public int Xp { get; set; }
    }
}
