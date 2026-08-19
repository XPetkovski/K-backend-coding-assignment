using System.Net;
using System.Net.Http.Json;
using Claims.Core.Features.Claims;
using Xunit;

namespace Claims.IntegrationTests;

[Collection(nameof(ClaimsApiCollection))]
public class ClaimsEndpointsTests : ApiTestBase
{
    public ClaimsEndpointsTests(ClaimsApiFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Listing_claims_returns_json()
    {
        var response = await Client.GetAsync("/Claims", Ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Creating_a_claim_returns_201_with_a_location_header()
    {
        var cover = await CreateCoverAsync();
        var command = new CreateClaimCommand
        {
            CoverId = cover.Id,
            Name = "Hull damage",
            Type = ClaimType.Collision,
            DamageCost = 500m,
            Created = cover.StartDate
        };

        var response = await Client.PostAsJsonAsync("/Claims", command, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await ReadAsync<ClaimResponse>(response);
        Assert.False(string.IsNullOrWhiteSpace(created.Id));
        Assert.Equal(cover.Id, created.CoverId);
        Assert.Equal(500m, created.DamageCost);
    }

    [Fact]
    public async Task The_location_header_points_at_the_new_claim()
    {
        var cover = await CreateCoverAsync();
        var command = new CreateClaimCommand
        {
            CoverId = cover.Id,
            Name = "Hull damage",
            Type = ClaimType.Collision,
            DamageCost = 500m,
            Created = cover.StartDate
        };

        var created = await Client.PostAsJsonAsync("/Claims", command, Json, Ct);
        var fetched = await Client.GetAsync(created.Headers.Location, Ct);

        fetched.EnsureSuccessStatusCode();
        var claim = await ReadAsync<ClaimResponse>(fetched);
        Assert.Equal((await ReadAsync<ClaimResponse>(created)).Id, claim.Id);
    }

    [Fact]
    public async Task A_created_claim_appears_in_the_list()
    {
        var cover = await CreateCoverAsync();
        var created = await CreateClaimAsync(cover.Id, cover.StartDate);

        var response = await Client.GetAsync("/Claims", Ct);
        var claims = await ReadAsync<List<ClaimResponse>>(response);

        Assert.Contains(claims, claim => claim.Id == created.Id);
    }

    [Fact]
    public async Task Deleting_a_claim_returns_204_and_the_claim_is_gone()
    {
        var cover = await CreateCoverAsync();
        var created = await CreateClaimAsync(cover.Id, cover.StartDate);

        var deleted = await Client.DeleteAsync($"/Claims/{created.Id}", Ct);
        var fetched = await Client.GetAsync($"/Claims/{created.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_claim_that_does_not_exist_returns_404()
    {
        var response = await Client.DeleteAsync($"/Claims/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Fetching_a_claim_that_does_not_exist_returns_404()
    {
        var response = await Client.GetAsync($"/Claims/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_damage_cost_above_the_maximum_returns_400_naming_the_field()
    {
        var cover = await CreateCoverAsync();
        var command = new CreateClaimCommand
        {
            CoverId = cover.Id,
            Name = "Total loss",
            Type = ClaimType.Fire,
            DamageCost = 100_001m,
            Created = cover.StartDate
        };

        var response = await Client.PostAsJsonAsync("/Claims", command, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.NotNull(problem.Errors);
        Assert.Contains(nameof(CreateClaimCommand.DamageCost), problem.Errors!.Keys);
    }

    [Fact]
    public async Task A_damage_cost_of_exactly_the_maximum_is_accepted()
    {
        var cover = await CreateCoverAsync();
        var command = new CreateClaimCommand
        {
            CoverId = cover.Id,
            Name = "Total loss",
            Type = ClaimType.Fire,
            DamageCost = 100_000m,
            Created = cover.StartDate
        };

        var response = await Client.PostAsJsonAsync("/Claims", command, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_claim_against_an_unknown_cover_returns_404()
    {
        var command = new CreateClaimCommand
        {
            CoverId = Guid.NewGuid().ToString(),
            Name = "Hull damage",
            Type = ClaimType.Collision,
            DamageCost = 500m,
            Created = Today.AddDays(2)
        };

        var response = await Client.PostAsJsonAsync("/Claims", command, Json, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_claim_created_before_the_cover_starts_returns_400()
    {
        var cover = await CreateCoverAsync(startDate: Today.AddDays(10), endDate: Today.AddDays(40));
        var command = new CreateClaimCommand
        {
            CoverId = cover.Id,
            Name = "Hull damage",
            Type = ClaimType.Collision,
            DamageCost = 500m,
            Created = cover.StartDate.AddDays(-1)
        };

        var response = await Client.PostAsJsonAsync("/Claims", command, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);

        // The message used to arrive as prose in `detail`; it is now keyed by the field it concerns,
        // matching the shape a shape-rule failure returns.
        Assert.NotNull(problem.Errors);
        Assert.Contains(nameof(CreateClaimCommand.Created), problem.Errors!.Keys);
        Assert.Contains("cover period", string.Join(" ", problem.Errors[nameof(CreateClaimCommand.Created)]));
    }

    [Fact]
    public async Task A_claim_created_after_the_cover_ends_returns_400()
    {
        var cover = await CreateCoverAsync(startDate: Today.AddDays(10), endDate: Today.AddDays(40));
        var command = new CreateClaimCommand
        {
            CoverId = cover.Id,
            Name = "Hull damage",
            Type = ClaimType.Collision,
            DamageCost = 500m,
            Created = cover.EndDate.AddDays(1)
        };

        var response = await Client.PostAsJsonAsync("/Claims", command, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_claim_created_on_the_first_or_last_day_of_the_cover_is_accepted()
    {
        var cover = await CreateCoverAsync(startDate: Today.AddDays(10), endDate: Today.AddDays(40));

        var onFirstDay = await CreateClaimAsync(cover.Id, cover.StartDate);
        var onLastDay = await CreateClaimAsync(cover.Id, cover.EndDate);

        Assert.Equal(cover.StartDate, onFirstDay.Created);
        Assert.Equal(cover.EndDate, onLastDay.Created);
    }

    [Fact]
    public async Task Dates_are_exchanged_without_a_time_component()
    {
        var cover = await CreateCoverAsync();
        var created = await CreateClaimAsync(cover.Id, cover.StartDate);

        var response = await Client.GetAsync($"/Claims/{created.Id}", Ct);
        var raw = await response.Content.ReadAsStringAsync(Ct);

        Assert.Contains($"\"created\":\"{cover.StartDate:yyyy-MM-dd}\"", raw);
    }

    [Theory]
    [InlineData("{\"name\":\"Hull damage\",\"damageCost\":500,\"created\":\"2030-01-02\"}")]
    [InlineData("{\"coverId\":\"c1\",\"damageCost\":500,\"created\":\"2030-01-02\"}")]
    [InlineData("{\"coverId\":\"c1\",\"name\":\"Hull damage\",\"created\":\"2030-01-02\"}")]
    [InlineData("{}")]
    public async Task A_claim_missing_a_required_field_returns_400(string body)
    {
        var response = await Client.PostAsync("/Claims", JsonBody(body), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Both kinds of bad input now report the offending field, so a client can bind either to a form
    // instead of string-matching prose out of "detail".
    [Fact]
    public async Task A_created_date_outside_the_cover_period_names_the_field()
    {
        var cover = await CreateCoverAsync();
        var command = new CreateClaimCommand
        {
            CoverId = cover.Id,
            Name = "Hull damage",
            Type = ClaimType.Collision,
            DamageCost = 500m,
            Created = cover.EndDate.AddDays(1)
        };

        var response = await Client.PostAsJsonAsync("/Claims", command, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.NotNull(problem.Errors);
        Assert.Contains(nameof(CreateClaimCommand.Created), problem.Errors!.Keys);
    }
}
