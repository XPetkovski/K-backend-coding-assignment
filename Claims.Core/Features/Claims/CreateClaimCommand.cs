namespace Claims.Core.Features.Claims;

public record CreateClaimCommand(
    string CoverId,
    string Name,
    ClaimType Type,
    decimal DamageCost,
    DateOnly Created);