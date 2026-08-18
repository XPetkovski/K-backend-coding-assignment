using System.Net;
using System.Net.Http.Json;
using Claims.Core.Features.Covers;
using Xunit;

namespace Claims.IntegrationTests;

[Collection(nameof(ClaimsApiCollection))]
public class CoversEndpointsTests : ApiTestBase
{
    public CoversEndpointsTests(ClaimsApiFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Creating_a_cover_returns_201_with_a_location_header()
    {
        var command = new CreateCoverCommand(Today.AddDays(1), Today.AddDays(30), CoverType.Yacht);

        var response = await Client.PostAsJsonAsync("/Covers", command, Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Creating_a_cover_stores_the_computed_premium()
    {
        var cover = await CreateCoverAsync(Today.AddDays(1), Today.AddDays(30), CoverType.Yacht);

        Assert.Equal(41_250.00m, cover.Premium);

        var fetched = await Client.GetAsync($"/Covers/{cover.Id}", Ct);
        Assert.Equal(41_250.00m, (await ReadAsync<CoverResponse>(fetched)).Premium);
    }

    [Fact]
    public async Task A_created_cover_appears_in_the_list()
    {
        var created = await CreateCoverAsync();

        var response = await Client.GetAsync("/Covers", Ct);
        var covers = await ReadAsync<List<CoverResponse>>(response);

        Assert.Contains(covers, cover => cover.Id == created.Id);
    }

    [Fact]
    public async Task Deleting_a_cover_returns_204_and_the_cover_is_gone()
    {
        var created = await CreateCoverAsync();

        var deleted = await Client.DeleteAsync($"/Covers/{created.Id}", Ct);
        var fetched = await Client.GetAsync($"/Covers/{created.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fetched.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_cover_that_does_not_exist_returns_404()
    {
        var response = await Client.DeleteAsync($"/Covers/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Fetching_a_cover_that_does_not_exist_returns_404()
    {
        var response = await Client.GetAsync($"/Covers/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_start_date_in_the_past_returns_400_naming_the_field()
    {
        var command = new CreateCoverCommand(Today.AddDays(-1), Today.AddDays(30), CoverType.Yacht);

        var response = await Client.PostAsJsonAsync("/Covers", command, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.NotNull(problem.Errors);
        Assert.Contains(nameof(CreateCoverCommand.StartDate), problem.Errors!.Keys);
    }

    [Fact]
    public async Task A_period_longer_than_one_year_returns_400()
    {
        var start = Today.AddDays(1);
        var command = new CreateCoverCommand(start, start.AddYears(1), CoverType.Yacht);

        var response = await Client.PostAsJsonAsync("/Covers", command, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Contains(nameof(CreateCoverCommand.EndDate), problem.Errors!.Keys);
    }

    [Fact]
    public async Task A_period_of_exactly_one_year_is_accepted()
    {
        var start = Today.AddDays(1);

        var cover = await CreateCoverAsync(start, start.AddYears(1).AddDays(-1));

        Assert.Equal(start, cover.StartDate);
    }

    [Fact]
    public async Task An_end_date_before_the_start_date_returns_400()
    {
        var command = new CreateCoverCommand(Today.AddDays(10), Today.AddDays(5), CoverType.Yacht);

        var response = await Client.PostAsJsonAsync("/Covers", command, Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(CoverType.Yacht, 41_250)]
    [InlineData(CoverType.PassengerShip, 45_000)]
    [InlineData(CoverType.ContainerShip, 48_750)]
    [InlineData(CoverType.BulkCarrier, 48_750)]
    [InlineData(CoverType.Tanker, 56_250)]
    public async Task Computing_a_premium_matches_the_documented_rates(CoverType coverType, int expected)
    {
        var start = Today.AddDays(1);
        var end = start.AddDays(29);

        var response = await Client.GetAsync(
            $"/Covers/compute?startDate={start:yyyy-MM-dd}&endDate={end:yyyy-MM-dd}&coverType={coverType}",
            Ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal(expected, await ReadAsync<decimal>(response));
    }

    [Theory]
    [InlineData("99")]
    [InlineData("-1")]
    [InlineData("Submarine")]
    public async Task Computing_a_premium_rejects_an_unknown_cover_type(string coverType)
    {
        var start = Today.AddDays(1);

        var response = await Client.GetAsync(
            $"/Covers/compute?startDate={start:yyyy-MM-dd}&endDate={start.AddDays(29):yyyy-MM-dd}&coverType={coverType}",
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Computing_a_premium_stores_nothing()
    {
        var before = await ReadAsync<List<CoverResponse>>(await Client.GetAsync("/Covers", Ct));

        var start = Today.AddDays(1);
        await Client.GetAsync(
            $"/Covers/compute?startDate={start:yyyy-MM-dd}&endDate={start.AddDays(29):yyyy-MM-dd}&coverType=Tanker",
            Ct);

        var after = await ReadAsync<List<CoverResponse>>(await Client.GetAsync("/Covers", Ct));
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public async Task Dates_are_exchanged_without_a_time_component()
    {
        var cover = await CreateCoverAsync();

        var response = await Client.GetAsync($"/Covers/{cover.Id}", Ct);
        var raw = await response.Content.ReadAsStringAsync(Ct);

        Assert.Contains($"\"startDate\":\"{cover.StartDate:yyyy-MM-dd}\"", raw);
        Assert.Contains($"\"endDate\":\"{cover.EndDate:yyyy-MM-dd}\"", raw);
    }

    [Fact]
    public async Task Cover_type_is_exchanged_as_a_name_not_a_number()
    {
        var cover = await CreateCoverAsync(coverType: CoverType.Tanker);

        var response = await Client.GetAsync($"/Covers/{cover.Id}", Ct);
        var raw = await response.Content.ReadAsStringAsync(Ct);

        Assert.Contains("\"type\":\"Tanker\"", raw);
    }
}