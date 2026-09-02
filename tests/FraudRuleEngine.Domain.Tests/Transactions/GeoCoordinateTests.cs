using FraudRuleEngine.Domain.Transactions;

namespace FraudRuleEngine.Domain.Tests.Transactions;

public sealed class GeoCoordinateTests
{
    private static readonly GeoCoordinate Johannesburg = new(-26.2041, 28.0473);
    private static readonly GeoCoordinate CapeTown = new(-33.9249, 18.4241);
    private static readonly GeoCoordinate London = new(51.5072, -0.1276);

    [Fact]
    public void Measures_a_known_distance_to_within_a_percent()
    {
        // Johannesburg to Cape Town is about 1,270 km great circle. The tolerance is deliberately loose:
        // the rule using this asks whether a speed is physically impossible, and nothing turns on the
        // last few kilometres.
        var distance = Johannesburg.DistanceInKilometresTo(CapeTown);

        distance.ShouldBeInRange(1_250, 1_290);
    }

    [Fact]
    public void Measures_an_intercontinental_distance()
    {
        // Johannesburg to London is about 9,070 km.
        Johannesburg.DistanceInKilometresTo(London).ShouldBeInRange(9_000, 9_150);
    }

    [Fact]
    public void Reports_zero_for_the_same_point()
    {
        // The short distance case is why this uses haversine. The spherical law of cosines loses
        // precision badly here, and a spurious few kilometres between two transactions in one shop would
        // read as an impossible speed when nobody moved.
        Johannesburg.DistanceInKilometresTo(Johannesburg).ShouldBe(0, tolerance: 0.000_001);
    }

    [Fact]
    public void Measures_the_same_distance_in_either_direction()
    {
        Johannesburg.DistanceInKilometresTo(London)
            .ShouldBe(London.DistanceInKilometresTo(Johannesburg), tolerance: 0.000_001);
    }

    [Fact]
    public void Handles_a_pair_spanning_the_antimeridian()
    {
        // Longitude 179 east and 179 west are two degrees apart, not 358. Getting this wrong would make
        // a short hop across the date line look like a journey most of the way round the planet.
        var east = new GeoCoordinate(0, 179);
        var west = new GeoCoordinate(0, -179);

        east.DistanceInKilometresTo(west).ShouldBeInRange(200, 240);
    }

    [Fact]
    public void Measures_the_poles_as_half_the_circumference_apart()
    {
        new GeoCoordinate(90, 0).DistanceInKilometresTo(new GeoCoordinate(-90, 0))
            .ShouldBeInRange(20_000, 20_020);
    }

    [Theory]
    [InlineData(-90, 0)]
    [InlineData(90, 0)]
    [InlineData(0, -180)]
    [InlineData(0, 180)]
    public void Accepts_coordinates_at_the_extremes(double latitude, double longitude)
    {
        var coordinate = new GeoCoordinate(latitude, longitude);

        coordinate.Latitude.ShouldBe(latitude);
        coordinate.Longitude.ShouldBe(longitude);
    }

    [Theory]
    [InlineData(-90.1, 0)]
    [InlineData(90.1, 0)]
    [InlineData(double.NaN, 0)]
    public void Rejects_an_out_of_range_latitude(double latitude, double longitude)
    {
        var act = () => { _ = new GeoCoordinate(latitude, longitude); };

        act.ShouldThrow<ArgumentOutOfRangeException>()
            .ParamName.ShouldBe("latitude");
    }

    [Theory]
    [InlineData(0, -180.1)]
    [InlineData(0, 180.1)]
    [InlineData(0, double.NaN)]
    public void Rejects_an_out_of_range_longitude(double latitude, double longitude)
    {
        var act = () => { _ = new GeoCoordinate(latitude, longitude); };

        act.ShouldThrow<ArgumentOutOfRangeException>()
            .ParamName.ShouldBe("longitude");
    }

    [Fact]
    public void Formats_using_invariant_culture()
    {
        Johannesburg.ToString().ShouldBe("-26.2041, 28.0473");
    }
}
