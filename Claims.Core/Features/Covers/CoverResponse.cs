namespace Claims.Core.Features.Covers;

public record CoverResponse(
    string Id,
    DateOnly StartDate,
    DateOnly EndDate,
    CoverType Type,
    decimal Premium);