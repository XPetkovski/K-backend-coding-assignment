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
        // Guarded here rather than only in the validator: PremiumRates.TypeMultiplier falls back to
        // the "other types" rate, so an undefined value would otherwise be priced silently.
        if (!Enum.IsDefined(coverType))
        {
            throw new DomainException($"'{(int)coverType}' is not a known cover type.");
        }

        var days = InsuranceDays(startDate, endDate);
        if (days <= 0)
        {
            throw new DomainException("Cover end date cannot be before its start date.");
        }

        var dayRate = PremiumRates.BaseDayRate * PremiumRates.TypeMultiplier(coverType);

        var fullRateDays = Math.Min(days, PremiumRates.FullRateDays);
        var firstDiscountDays = Math.Clamp(days - PremiumRates.FullRateDays, 0, PremiumRates.FirstDiscountDays);
        var secondDiscountDays = Math.Max(days - PremiumRates.FullRateDays - PremiumRates.FirstDiscountDays, 0);

        var total = fullRateDays * dayRate
                    + firstDiscountDays * dayRate * (1 - PremiumRates.FirstDiscount(coverType))
                    + secondDiscountDays * dayRate * (1 - PremiumRates.SecondDiscount(coverType));

        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    private static int InsuranceDays(DateTime startDate, DateTime endDate) =>
        (endDate.Date - startDate.Date).Days + 1;
}