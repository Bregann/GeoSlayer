namespace GeoSlayer.Domain.Interfaces.Api;

public interface IPoiImportService
{
    Task ImportCellPois(double cellLat, double cellLng);
}
