namespace Claims.Core.Features.Covers;

public record CreateCoverCommand(DateOnly StartDate, DateOnly EndDate, CoverType Type);