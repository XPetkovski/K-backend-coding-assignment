using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Core.Features.Claims;
using Claims.Core.Features.Covers;
using Xunit;

namespace Claims.IntegrationTests;

public abstract class ApiTestBase
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected ApiTestBase(ClaimsApiFixture fixture)
    {
        Fixture = fixture;
        Client = fixture.CreateClient();
    }

    protected ClaimsApiFixture Fixture { get; }

    protected HttpClient Client { get; }

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    protected async Task<CoverResponse> CreateCoverAsync(
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CoverType coverType = CoverType.Yacht)
    {
        var command = new CreateCoverCommand(
            startDate ?? Today.AddDays(1),
            endDate ?? Today.AddDays(30),
            coverType);

        var response = await Client.PostAsJsonAsync("/Covers", command, Json, Ct);
        response.EnsureSuccessStatusCode();

        return await ReadAsync<CoverResponse>(response);
    }

    protected async Task<ClaimResponse> CreateClaimAsync(string coverId, DateOnly created, decimal damageCost = 500m)
    {
        var command = new CreateClaimCommand(coverId, "Hull damage", ClaimType.Collision, damageCost, created);

        var response = await Client.PostAsJsonAsync("/Claims", command, Json, Ct);
        response.EnsureSuccessStatusCode();

        return await ReadAsync<ClaimResponse>(response);
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json, Ct);

        Assert.NotNull(value);

        return value;
    }

    protected static async Task<ProblemResponse> ReadProblemAsync(HttpResponseMessage response) =>
        await ReadAsync<ProblemResponse>(response);

    protected static async Task<T?> EventuallyAsync<T>(Func<Task<T?>> probe, TimeSpan? timeout = null)
        where T : class
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (DateTime.UtcNow < deadline)
        {
            var result = await probe();
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(100, Ct);
        }

        return await probe();
    }
}

public sealed record ProblemResponse(
    string? Title,
    int? Status,
    string? Detail,
    Dictionary<string, string[]>? Errors);