using FraudRuleEngine.Api.Configuration;

namespace FraudRuleEngine.Api.Tests.Configuration;

public sealed class ApiKeyOptionsValidatorTests
{
    private readonly ApiKeyOptionsValidator _validator = new();

    [Fact]
    public void Accepts_a_key_of_reasonable_length_with_a_client()
    {
        _validator.Validate(null, Valid()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_having_no_keys_at_all()
    {
        // Every request would be refused, which is a misconfiguration rather than a security posture.
        var result = _validator.Validate(null, new ApiKeyOptions());

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("at least one key");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("0123456789012345")]
    public void Requires_a_key_long_enough_not_to_be_guessed(string key)
    {
        var options = new ApiKeyOptions();
        options.Keys[key] = "a-client";

        var expected = key.Length < ApiKeyOptions.MinimumKeyLength;

        _validator.Validate(null, options).Failed.ShouldBe(expected);
    }

    [Fact]
    public void Rejects_a_key_with_no_client_to_attribute_it_to()
    {
        var options = new ApiKeyOptions();
        options.Keys["0123456789abcdefgh"] = "  ";

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_an_allowance_that_would_refuse_everything(int requestsPerWindow)
    {
        var options = Valid();
        options.RequestsPerWindow = requestsPerWindow;

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_a_window_that_is_not_positive()
    {
        var options = Valid();
        options.Window = TimeSpan.Zero;

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Collects_every_problem_rather_than_stopping_at_the_first()
    {
        var options = new ApiKeyOptions();
        options.Keys["tooshort"] = "";

        var result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        (result.Failures?.Count() ?? 0).ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Accepts_a_client_given_an_allowance_of_its_own()
    {
        var options = Valid();
        options.Clients["a-client"] = new ClientOptions { RequestsPerWindow = 25 };

        _validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_an_allowance_that_would_refuse_every_request(int allowance)
    {
        var options = Valid();
        options.Clients["a-client"] = new ClientOptions { RequestsPerWindow = allowance };

        var result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("must be positive");
    }

    [Fact]
    public void Rejects_an_allowance_for_a_client_no_key_maps_to()
    {
        // Left alone this reads as working configuration while the client stays on the default, which is
        // the wrong number written down in a file nobody will read again.
        var options = Valid();
        options.Clients["a-clientt"] = new ClientOptions { RequestsPerWindow = 25 };

        var result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("no key maps to that client name");
    }

    private static ApiKeyOptions Valid()
    {
        var options = new ApiKeyOptions();
        options.Keys["0123456789abcdefgh"] = "a-client";

        return options;
    }
}
