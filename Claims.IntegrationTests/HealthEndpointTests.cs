using System.Net;
using Xunit;

namespace Claims.IntegrationTests;

[Collection(nameof(ClaimsApiCollection))]
public class HealthEndpointTests : ApiTestBase
{
    public HealthEndpointTests(ClaimsApiFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Health_reports_healthy_when_both_databases_are_reachable()
    {
        var response = await Client.GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(Ct));
    }
}