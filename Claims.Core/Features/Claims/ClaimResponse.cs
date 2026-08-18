namespace Claims.Core.Features.Claims;

public record ClaimResponse(
    string Id,
    string CoverId,
    string Name,
    ClaimType Type,
    decimal DamageCost,
    DateOnly Created);