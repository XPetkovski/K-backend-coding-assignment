namespace Claims.Core.Features.Covers;

public static class PremiumRates
{
    public const decimal BaseDayRate = 1250m;

    public const int FullRateDays = 30;
    public const int FirstDiscountDays = 150;

    public static decimal TypeMultiplier(CoverType coverType) => coverType switch
    {
        CoverType.Yacht => 1.10m,
        CoverType.PassengerShip => 1.20m,
        CoverType.Tanker => 1.50m,
        _ => 1.30m,
    };

    public static decimal FirstDiscount(CoverType coverType) =>
        coverType == CoverType.Yacht ? 0.05m : 0.02m;

    public static decimal SecondDiscount(CoverType coverType) =>
        coverType == CoverType.Yacht ? 0.08m : 0.03m;
}