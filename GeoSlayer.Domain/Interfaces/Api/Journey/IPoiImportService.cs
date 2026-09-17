namespace GeoSlayer.Domain.Interfaces.Api.Journey
{
    public interface IPoiImportService
    {
        Task ImportCellPois(double cellLat, double cellLng);
    }
}
