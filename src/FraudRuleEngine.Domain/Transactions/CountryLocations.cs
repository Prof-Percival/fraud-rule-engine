using System.Collections.Frozen;

namespace FraudRuleEngine.Domain.Transactions;

/// <summary>
/// Approximate centre points for the countries this service has coordinates for.
/// </summary>
/// <remarks>
/// Enough to answer whether two transactions are implausibly far apart, and no more. A country not in
/// this table has no coordinates, and a rule relying on it must decline rather than guess, because
/// <see cref="CountryCode"/> validates format but not membership of the real ISO register.
///
/// <para>
/// Centroids are the limitation. Johannesburg and Cape Town resolve to the same point, so a journey of
/// over a thousand kilometres reads as zero; a card used either side of the Beitbridge border reads as
/// nine hundred. City level geolocation on the transaction is the fix, not a better country table.
/// </para>
/// </remarks>
public static class CountryLocations
{
    private static readonly FrozenDictionary<CountryCode, GeoCoordinate> Centroids =
        new Dictionary<CountryCode, GeoCoordinate>
        {
            // Southern Africa
            [CountryCode.From("ZA")] = new GeoCoordinate(-30.5595, 22.9375),
            [CountryCode.From("BW")] = new GeoCoordinate(-22.3285, 24.6849),
            [CountryCode.From("NA")] = new GeoCoordinate(-22.9576, 18.4904),
            [CountryCode.From("ZW")] = new GeoCoordinate(-19.0154, 29.1549),
            [CountryCode.From("MZ")] = new GeoCoordinate(-18.6657, 35.5296),
            [CountryCode.From("LS")] = new GeoCoordinate(-29.6100, 28.2336),
            [CountryCode.From("SZ")] = new GeoCoordinate(-26.5225, 31.4659),
            [CountryCode.From("ZM")] = new GeoCoordinate(-13.1339, 27.8493),

            // Rest of Africa
            [CountryCode.From("KE")] = new GeoCoordinate(-0.0236, 37.9062),
            [CountryCode.From("NG")] = new GeoCoordinate(9.0820, 8.6753),
            [CountryCode.From("EG")] = new GeoCoordinate(26.8206, 30.8025),
            [CountryCode.From("GH")] = new GeoCoordinate(7.9465, -1.0232),
            [CountryCode.From("MU")] = new GeoCoordinate(-20.3484, 57.5522),

            // Europe
            [CountryCode.From("GB")] = new GeoCoordinate(55.3781, -3.4360),
            [CountryCode.From("IE")] = new GeoCoordinate(53.1424, -7.6921),
            [CountryCode.From("DE")] = new GeoCoordinate(51.1657, 10.4515),
            [CountryCode.From("FR")] = new GeoCoordinate(46.2276, 2.2137),
            [CountryCode.From("NL")] = new GeoCoordinate(52.1326, 5.2913),
            [CountryCode.From("ES")] = new GeoCoordinate(40.4637, -3.7492),
            [CountryCode.From("PT")] = new GeoCoordinate(39.3999, -8.2245),
            [CountryCode.From("IT")] = new GeoCoordinate(41.8719, 12.5674),
            [CountryCode.From("CH")] = new GeoCoordinate(46.8182, 8.2275),
            [CountryCode.From("SE")] = new GeoCoordinate(60.1282, 18.6435),

            // Americas
            [CountryCode.From("US")] = new GeoCoordinate(37.0902, -95.7129),
            [CountryCode.From("CA")] = new GeoCoordinate(56.1304, -106.3468),
            [CountryCode.From("MX")] = new GeoCoordinate(23.6345, -102.5528),
            [CountryCode.From("BR")] = new GeoCoordinate(-14.2350, -51.9253),
            [CountryCode.From("AR")] = new GeoCoordinate(-38.4161, -63.6167),

            // Asia and Oceania
            [CountryCode.From("AE")] = new GeoCoordinate(23.4241, 53.8478),
            [CountryCode.From("IN")] = new GeoCoordinate(20.5937, 78.9629),
            [CountryCode.From("CN")] = new GeoCoordinate(35.8617, 104.1954),
            [CountryCode.From("JP")] = new GeoCoordinate(36.2048, 138.2529),
            [CountryCode.From("SG")] = new GeoCoordinate(1.3521, 103.8198),
            [CountryCode.From("TH")] = new GeoCoordinate(15.8700, 100.9925),
            [CountryCode.From("AU")] = new GeoCoordinate(-25.2744, 133.7751),
            [CountryCode.From("NZ")] = new GeoCoordinate(-40.9006, 174.8860),
        }.ToFrozenDictionary();

    /// <summary>How many countries have coordinates.</summary>
    public static int Count => Centroids.Count;

    /// <summary>
    /// Finds the approximate centre of a country, if it is known.
    /// </summary>
    /// <returns>True when coordinates are available, false when the country is not in the table.</returns>
    public static bool TryGetCentroid(CountryCode country, out GeoCoordinate centroid) =>
        Centroids.TryGetValue(country, out centroid);
}
