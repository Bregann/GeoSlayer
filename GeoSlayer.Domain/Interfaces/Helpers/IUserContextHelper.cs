using GeoSlayer.Domain.Database.Models;

namespace GeoSlayer.Domain.Interfaces.Helpers
{
    public interface IUserContextHelper
    {
        string GetUserId();
        string GetUserFirstName();
        User GetUser();
    }
}
