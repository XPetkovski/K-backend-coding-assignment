using Claims.Core.Common;

namespace Claims.Core.Features.Claims;

public class CreateClaimCommandValidator : IValidator<CreateClaimCommand>
{
    public const decimal MaxDamageCost = 100_000m;

    public ValidationResult Validate(CreateClaimCommand command)
    {
        var result = new ValidationResult();

        result.AddIf(
            string.IsNullOrWhiteSpace(command.CoverId),
            nameof(command.CoverId),
            "CoverId is required.");

        result.AddIf(
            string.IsNullOrWhiteSpace(command.Name),
            nameof(command.Name),
            "Name is required.");

        result.AddIf(
            command.DamageCost <= 0,
            nameof(command.DamageCost),
            "DamageCost must be greater than 0.");

        result.AddIf(
            command.DamageCost > MaxDamageCost,
            nameof(command.DamageCost),
            $"DamageCost cannot exceed {MaxDamageCost:N0}.");

        result.AddIf(
            !Enum.IsDefined(command.Type),
            nameof(command.Type),
            $"'{command.Type}' is not a recognised claim type.");

        return result;
    }
}