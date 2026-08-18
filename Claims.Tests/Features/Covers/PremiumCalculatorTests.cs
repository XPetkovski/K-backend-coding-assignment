using Claims.Core.Common;
using Claims.Core.Features.Covers;
using Xunit;

namespace Claims.Tests.Features.Covers;

public class PremiumCalculatorTests
{
    private static readonly DateTime Start = new(2026, 1, 1);

    private readonly PremiumCalculator _calculator = new();

    // Inclusive day counting: a 1-day cover starts and ends on the same date.
    private decimal PremiumFor(int days, CoverType coverType) =>
        _calculator.Compute(Start, Start.AddDays(days - 1), coverType);

    public static TheoryData<CoverType, int, decimal> TierBoundaries => new()
    {
        // Yacht: 1250 * 1.10 = 1375/day, discounted to 1306.25 then 1265.
        { CoverType.Yacht, 1, 1_375.00m },
        { CoverType.Yacht, 30, 41_250.00m },
        { CoverType.Yacht, 31, 42_556.25m },
        { CoverType.Yacht, 180, 237_187.50m },
        { CoverType.Yacht, 181, 238_452.50m },
        { CoverType.Yacht, 365, 471_212.50m },

        // PassengerShip: 1250 * 1.20 = 1500/day, discounted to 1470 then 1455.
        { CoverType.PassengerShip, 1, 1_500.00m },
        { CoverType.PassengerShip, 30, 45_000.00m },
        { CoverType.PassengerShip, 31, 46_470.00m },
        { CoverType.PassengerShip, 180, 265_500.00m },
        { CoverType.PassengerShip, 181, 266_955.00m },
        { CoverType.PassengerShip, 365, 534_675.00m },

        // ContainerShip ("other"): 1250 * 1.30 = 1625/day, discounted to 1592.50 then 1576.25.
        { CoverType.ContainerShip, 1, 1_625.00m },
        { CoverType.ContainerShip, 30, 48_750.00m },
        { CoverType.ContainerShip, 31, 50_342.50m },
        { CoverType.ContainerShip, 180, 287_625.00m },
        { CoverType.ContainerShip, 181, 289_201.25m },
        { CoverType.ContainerShip, 365, 579_231.25m },

        // BulkCarrier ("other"): same rates as ContainerShip.
        { CoverType.BulkCarrier, 1, 1_625.00m },
        { CoverType.BulkCarrier, 30, 48_750.00m },
        { CoverType.BulkCarrier, 31, 50_342.50m },
        { CoverType.BulkCarrier, 180, 287_625.00m },
        { CoverType.BulkCarrier, 181, 289_201.25m },
        { CoverType.BulkCarrier, 365, 579_231.25m },

        // Tanker: 1250 * 1.50 = 1875/day, discounted to 1837.50 then 1818.75.
        { CoverType.Tanker, 1, 1_875.00m },
        { CoverType.Tanker, 30, 56_250.00m },
        { CoverType.Tanker, 31, 58_087.50m },
        { CoverType.Tanker, 180, 331_875.00m },
        { CoverType.Tanker, 181, 333_693.75m },
        { CoverType.Tanker, 365, 668_343.75m },
    };

    [Theory]
    [MemberData(nameof(TierBoundaries))]
    public void Prices_each_tier_boundary(CoverType coverType, int days, decimal expected)
    {
        Assert.Equal(expected, PremiumFor(days, coverType));
    }

    [Theory]
    [InlineData(CoverType.Yacht)]
    [InlineData(CoverType.PassengerShip)]
    [InlineData(CoverType.ContainerShip)]
    [InlineData(CoverType.BulkCarrier)]
    [InlineData(CoverType.Tanker)]
    public void Charges_base_rate_times_type_multiplier_for_a_single_day(CoverType coverType)
    {
        var expected = PremiumRates.BaseDayRate * PremiumRates.TypeMultiplier(coverType);

        Assert.Equal(expected, PremiumFor(1, coverType));
    }

    // Regression: the original tiers overlapped, charging the first 30 days three times.
    [Theory]
    [InlineData(CoverType.Yacht)]
    [InlineData(CoverType.Tanker)]
    public void Charges_the_first_thirty_days_exactly_once(CoverType coverType)
    {
        var dayRate = PremiumRates.BaseDayRate * PremiumRates.TypeMultiplier(coverType);

        Assert.Equal(30 * dayRate, PremiumFor(30, coverType));
    }

    [Fact]
    public void Keeps_charging_beyond_one_year()
    {
        Assert.Equal(515_487.50m, PremiumFor(400, CoverType.Yacht));
        Assert.True(PremiumFor(400, CoverType.Yacht) > PremiumFor(365, CoverType.Yacht));
    }

    [Theory]
    [InlineData(CoverType.Yacht, 1_306.25)]
    [InlineData(CoverType.Tanker, 1_837.50)]
    public void Applies_the_first_discount_from_day_thirty_one(CoverType coverType, double expectedMarginalDay)
    {
        var marginal = PremiumFor(31, coverType) - PremiumFor(30, coverType);

        Assert.Equal((decimal)expectedMarginalDay, marginal);
    }

    [Theory]
    [InlineData(CoverType.Yacht, 1_265.00)]
    [InlineData(CoverType.Tanker, 1_818.75)]
    public void Applies_the_second_discount_from_day_one_hundred_eighty_one(CoverType coverType, double expectedMarginalDay)
    {
        var marginal = PremiumFor(181, coverType) - PremiumFor(180, coverType);

        Assert.Equal((decimal)expectedMarginalDay, marginal);
    }

    [Fact]
    public void Deepens_the_discount_in_the_third_tier()
    {
        var firstTierDay = PremiumFor(30, CoverType.Yacht) - PremiumFor(29, CoverType.Yacht);
        var secondTierDay = PremiumFor(31, CoverType.Yacht) - PremiumFor(30, CoverType.Yacht);
        var thirdTierDay = PremiumFor(181, CoverType.Yacht) - PremiumFor(180, CoverType.Yacht);

        Assert.True(secondTierDay < firstTierDay);
        Assert.True(thirdTierDay < secondTierDay);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(90)]
    [InlineData(365)]
    public void Prices_container_ships_and_bulk_carriers_identically(int days)
    {
        Assert.Equal(PremiumFor(days, CoverType.ContainerShip), PremiumFor(days, CoverType.BulkCarrier));
    }

    [Fact]
    public void Never_decreases_as_the_period_lengthens()
    {
        foreach (var coverType in Enum.GetValues<CoverType>())
        {
            var previous = 0m;
            for (var days = 1; days <= 400; days++)
            {
                var premium = PremiumFor(days, coverType);
                Assert.True(premium > previous, $"{coverType} at {days} days: {premium} !> {previous}");
                previous = premium;
            }
        }
    }

    [Fact]
    public void Ignores_the_time_component_of_the_supplied_dates()
    {
        var withTime = _calculator.Compute(
            new DateTime(2026, 1, 1, 23, 59, 0),
            new DateTime(2026, 1, 30, 0, 1, 0),
            CoverType.Yacht);

        Assert.Equal(PremiumFor(30, CoverType.Yacht), withTime);
    }

    [Fact]
    public void Rejects_a_cover_ending_before_it_starts()
    {
        var exception = Assert.Throws<DomainException>(
            () => _calculator.Compute(Start, Start.AddDays(-1), CoverType.Yacht));

        Assert.Contains("end date", exception.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(99)]
    public void Rejects_a_cover_type_outside_the_enum(int coverType)
    {
        var exception = Assert.Throws<DomainException>(
            () => _calculator.Compute(Start, Start.AddDays(29), (CoverType)coverType));

        Assert.Contains("not a known cover type", exception.Message);
    }

    [Fact]
    public void Prices_every_defined_cover_type()
    {
        foreach (var coverType in Enum.GetValues<CoverType>())
        {
            Assert.True(PremiumFor(30, coverType) > 0);
        }
    }
}