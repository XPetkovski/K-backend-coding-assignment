namespace Claims.Core.Features.Covers;

public record CreateCoverCommand
{
    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public required CoverType Type { get; init; }
}