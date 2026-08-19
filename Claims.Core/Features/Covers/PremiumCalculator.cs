using Claims.Core.Common;

namespace Claims.Core.Features.Covers;

public interface IPremiumCalculator
{
    decimal Compute(DateTime startDate, DateTime endDate, CoverType coverType);
}

public class PremiumCalculator : IPremiumCalculator
{
    public decimal Compute(DateTime startDate, DateTime endDate, CoverType coverType)
    {
        // Throws for a cover type outside the enum, which is why there is no separate guard here.
        var rates = PremiumRates.For(coverType);

        var days = InsuranceDays(startDate, endDate);
        if (days <= 0)
        {
            throw new DomainException(
                "Cover end date cannot be before its start date.",
                nameof(CreateCoverCommand.EndDate));
        }

        var dayRate = PremiumRates.BaseDayRate * rates.Multiplier;

        var fullRateDays = Math.Min(days, PremiumRates.FullRateDays);
        var firstDiscountDays = Math.Clamp(days - PremiumRates.FullRateDays, 0, PremiumRates.FirstDiscountDays);
        var secondDiscountDays = Math.Max(days - PremiumRates.FullRateDays - PremiumRates.FirstDiscountDays, 0);

        var total = fullRateDays * dayRate
                    + firstDiscountDays * dayRate * (1 - rates.FirstDiscount)
                    + secondDiscountDays * dayRate * (1 - rates.SecondDiscount);

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static int InsuranceDays(DateTime startDate, DateTime endDate) =>
        (endDate.Date - startDate.Date).Days + 1;
}