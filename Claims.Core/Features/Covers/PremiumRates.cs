using Claims.Core.Common;

namespace Claims.Core.Features.Covers;

public sealed record RateCard(decimal Multiplier, decimal FirstDiscount, decimal SecondDiscount);

public static class PremiumRates
{
    public const decimal BaseDayRate = 1250m;

    public const int FullRateDays = 30;
    public const int FirstDiscountDays = 150;

    public static RateCard For(CoverType coverType) => coverType switch
    {
        CoverType.Yacht => new RateCard(1.10m, 0.05m, 0.08m),
        CoverType.PassengerShip => new RateCard(1.20m, 0.02m, 0.03m),
        CoverType.ContainerShip => new RateCard(1.30m, 0.02m, 0.03m),
        CoverType.BulkCarrier => new RateCard(1.30m, 0.02m, 0.03m),
        CoverType.Tanker => new RateCard(1.50m, 0.02m, 0.03m),
        _ => throw new DomainException(
            $"'{(int)coverType}' is not a known cover type.",
            nameof(CreateCoverCommand.Type))
    };
}