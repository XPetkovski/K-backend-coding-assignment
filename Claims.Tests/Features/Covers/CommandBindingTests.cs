using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Core.Features.Claims;
using Claims.Core.Features.Covers;
using Xunit;

namespace Claims.Tests.Features.Covers;

// Regression: the commands were positional records, so a non-nullable enum had no "absent" value and
// an omitted type bound silently to the zero member Yacht for covers, Collision for claims.
public class CommandBindingTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Theory]
    [InlineData("""{"startDate":"2030-01-01","endDate":"2030-01-30"}""")]
    [InlineData("""{"endDate":"2030-01-30","type":"Tanker"}""")]
    [InlineData("""{"startDate":"2030-01-01","type":"Tanker"}""")]
    [InlineData("{}")]
    public void A_cover_command_missing_any_member_fails_to_deserialise(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateCoverCommand>(json, Json));
    }

    [Theory]
    [InlineData("""{"name":"Hull damage","damageCost":500,"created":"2030-01-02"}""")]
    [InlineData("""{"coverId":"c1","damageCost":500,"created":"2030-01-02"}""")]
    [InlineData("""{"coverId":"c1","name":"Hull damage","created":"2030-01-02"}""")]
    [InlineData("{}")]
    public void A_claim_command_missing_any_member_fails_to_deserialise(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateClaimCommand>(json, Json));
    }

    [Fact]
    public void A_complete_cover_command_still_deserialises()
    {
        var command = JsonSerializer.Deserialize<CreateCoverCommand>(
            """{"startDate":"2030-01-01","endDate":"2030-01-30","type":"Tanker"}""", Json);

        Assert.Equal(new DateOnly(2030, 1, 1), command!.StartDate);
        Assert.Equal(new DateOnly(2030, 1, 30), command.EndDate);
        Assert.Equal(CoverType.Tanker, command.Type);
    }

    [Fact]
    public void A_complete_claim_command_still_deserialises()
    {
        var command = JsonSerializer.Deserialize<CreateClaimCommand>(
            """
            {"coverId":"c1","name":"Hull damage","type":"Fire","damageCost":500,"created":"2030-01-02"}
            """, Json);

        Assert.Equal("c1", command!.CoverId);
        Assert.Equal(ClaimType.Fire, command.Type);
        Assert.Equal(500m, command.DamageCost);
    }
}