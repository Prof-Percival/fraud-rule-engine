using FraudRuleEngine.Api.Configuration;

namespace FraudRuleEngine.Api.Tests.Configuration;

public sealed class ApiKeyOptionsTests
{
    [Fact]
    public void Falls_back_to_the_default_allowance_for_a_client_with_none_of_its_own()
    {
        var options = new ApiKeyOptions { RequestsPerWindow = 500 };

        options.AllowanceFor("anyone").ShouldBe(500);
    }

    [Fact]
    public void Uses_the_allowance_a_client_was_given()
    {
        var options = new ApiKeyOptions { RequestsPerWindow = 500 };
        options.Clients["noisy"] = new ClientOptions { RequestsPerWindow = 20 };

        options.AllowanceFor("noisy").ShouldBe(20);
        options.AllowanceFor("everyone-else").ShouldBe(500);
    }

    [Fact]
    public void Treats_an_entry_with_no_allowance_as_the_default()
    {
        var options = new ApiKeyOptions { RequestsPerWindow = 500 };
        options.Clients["listed-but-unset"] = new ClientOptions();

        options.AllowanceFor("listed-but-unset").ShouldBe(500);
    }

    [Fact]
    public void Gives_two_keys_of_one_client_the_same_allowance()
    {
        // A rotation has both keys valid at once, and they have to share one allowance or rotating
        // doubles what that client may send.
        var options = new ApiKeyOptions { RequestsPerWindow = 500 };
        options.Keys["0123456789abcdefgh"] = "one-client";
        options.Keys["hgfedcba9876543210"] = "one-client";
        options.Clients["one-client"] = new ClientOptions { RequestsPerWindow = 30 };

        foreach (var client in options.Keys.Values)
        {
            options.AllowanceFor(client).ShouldBe(30);
        }
    }
}
