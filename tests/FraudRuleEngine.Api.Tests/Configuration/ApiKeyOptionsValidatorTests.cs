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

    private static ApiKeyOptions Valid()
    {
        var options = new ApiKeyOptions();
        options.Keys["0123456789abcdefgh"] = "a-client";

        return options;
    }
}
