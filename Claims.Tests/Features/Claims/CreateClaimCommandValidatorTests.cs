using Claims.Core.Features.Claims;
using Xunit;

namespace Claims.Tests.Features.Claims;

public class CreateClaimCommandValidatorTests
{
    private static readonly DateOnly Created = new(2026, 8, 17);

    private readonly CreateClaimCommandValidator _validator = new();

    private static CreateClaimCommand Command(
        decimal damageCost = 1_000m,
        string coverId = "cover-1",
        string name = "Hull damage",
        ClaimType type = ClaimType.Collision) =>
        new()
        {
            CoverId = coverId,
            Name = name,
            Type = type,
            DamageCost = damageCost,
            Created = Created
        };

    [Fact]
    public void Accepts_a_valid_claim()
    {
        var result = _validator.Validate(Command());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Accepts_a_damage_cost_of_exactly_the_maximum()
    {
        var result = _validator.Validate(Command(damageCost: CreateClaimCommandValidator.MaxDamageCost));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Rejects_a_damage_cost_above_the_maximum()
    {
        var result = _validator.Validate(Command(damageCost: CreateClaimCommandValidator.MaxDamageCost + 0.01m));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Property == nameof(CreateClaimCommand.DamageCost));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_a_non_positive_damage_cost(int damageCost)
    {
        var result = _validator.Validate(Command(damageCost: damageCost));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Property == nameof(CreateClaimCommand.DamageCost));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_name(string name)
    {
        var result = _validator.Validate(Command(name: name));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Property == nameof(CreateClaimCommand.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_missing_cover_id(string coverId)
    {
        var result = _validator.Validate(Command(coverId: coverId));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Property == nameof(CreateClaimCommand.CoverId));
    }

    [Fact]
    public void Rejects_an_unrecognised_claim_type()
    {
        var result = _validator.Validate(Command(type: (ClaimType)42));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Property == nameof(CreateClaimCommand.Type));
    }

    [Fact]
    public void Reports_every_broken_rule_at_once()
    {
        var result = _validator.Validate(Command(damageCost: 0, coverId: "", name: "", type: (ClaimType)42));

        Assert.Equal(4, result.Errors.Count);
    }
}