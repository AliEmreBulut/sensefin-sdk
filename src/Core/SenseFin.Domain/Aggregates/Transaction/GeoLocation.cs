using SenseFin.Domain.Common;

namespace SenseFin.Domain.Aggregates.Transaction;

// Coğrafi konum verilerini temsil eden Value Object.
public sealed class GeoLocation : ValueObject
{
    public double Latitude { get; }
    public double Longitude { get; }
    public string? Country { get; }
    public string? City { get; }

    private GeoLocation(double latitude, double longitude, string? country, string? city)
    {
        Latitude = latitude;
        Longitude = longitude;
        Country = country;
        City = city;
    }

    public static GeoLocation Create(double latitude, double longitude, string? country = null, string? city = null)
    {
        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude must be between -90 and 90.");

        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude must be between -180 and 180.");

        return new GeoLocation(latitude, longitude, country, city);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
        yield return Country;
        yield return City;
    }

    public override string ToString() => $"({Latitude:F4}, {Longitude:F4}) {Country}/{City}";
}
