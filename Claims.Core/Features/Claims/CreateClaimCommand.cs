namespace Claims.Core.Features.Claims;

public record CreateClaimCommand
{
    public required string CoverId { get; init; }

    public required string Name { get; init; }

    public required ClaimType Type { get; init; }

    public required decimal DamageCost { get; init; }

    public required DateOnly Created { get; init; }
}