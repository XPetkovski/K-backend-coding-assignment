using Claims.Core.Common;

namespace Claims.Core.Features.Covers;

public class CreateCoverCommandValidator : IValidator<CreateCoverCommand>
{
    private readonly IClock _clock;

    public CreateCoverCommandValidator(IClock clock)
    {
        _clock = clock;
    }

    public ValidationResult Validate(CreateCoverCommand command)
    {
        var result = new ValidationResult();

        result.AddIf(
            command.StartDate < _clock.Today,
            nameof(command.StartDate),
            "StartDate cannot be in the past.");

        result.AddIf(
            command.EndDate < command.StartDate,
            nameof(command.EndDate),
            "EndDate cannot be before StartDate.");

        // Both endpoints are covered, so one calendar year ends the day before the anniversary.
        result.AddIf(
            command.EndDate >= command.StartDate && command.EndDate > LastDayOfOneYear(command.StartDate),
            nameof(command.EndDate),
            "Total insurance period cannot exceed 1 year.");

        result.AddIf(
            !Enum.IsDefined(command.Type),
            nameof(command.Type),
            $"'{command.Type}' is not a recognised cover type.");

        return result;
    }

    private static DateOnly LastDayOfOneYear(DateOnly startDate) =>
        startDate.AddYears(1).AddDays(-1);
}