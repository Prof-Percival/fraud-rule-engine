using System.Globalization;

namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// A point on the earth's surface, used to work out how fast somebody would have had to travel.
/// </summary>
public readonly record struct GeoCoordinate
{
    // Mean earth radius per the IUGG. Any single radius is an approximation, off by up to half a percent
    // at the extremes, which is irrelevant when the question is whether a speed is physically impossible.
    private const double MeanEarthRadiusKm = 6371.0088;

    public GeoCoordinate(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90 || double.IsNaN(latitude))
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                latitude,
                "Latitude must be between -90 and 90 degrees.");
        }

        if (longitude is < -180 or > 180 || double.IsNaN(longitude))
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                longitude,
                "Longitude must be between -180 and 180 degrees.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    /// <summary>
    /// Great circle distance in kilometres, by haversine.
    /// </summary>
    /// <remarks>
    /// Haversine rather than the spherical law of cosines, which loses precision badly at short
    /// distances. Two transactions in the same shop are exactly that case, and a spurious few kilometres
    /// there would read as an impossible speed when nobody moved.
    /// </remarks>
    public double DistanceInKilometresTo(GeoCoordinate other)
    {
        var fromLatitude = ToRadians(Latitude);
        var toLatitude = ToRadians(other.Latitude);
        var latitudeDelta = toLatitude - fromLatitude;
        var longitudeDelta = ToRadians(other.Longitude - Longitude);

        var haversine =
            (Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2))
            + (Math.Cos(fromLatitude) * Math.Cos(toLatitude)
                * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2));

        return 2 * MeanEarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(haversine)));
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Latitude:F4}, {Longitude:F4}");

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
