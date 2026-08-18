using Claims.Core.Features.Covers;
using Claims.Tests.Fakes;
using Xunit;

namespace Claims.Tests.Features.Covers;

public class CreateCoverCommandValidatorTests
{
    private static readonly DateOnly Today = new(2026, 8, 17);

    private readonly CreateCoverCommandValidator _validator = new(new FakeClock(Today));

    private static CreateCoverCommand Command(DateOnly start, DateOnly end, CoverType type = CoverType.Yacht) =>
        new(start, end, type);

    [Fact]
    public void Accepts_a_cover_starting_today()
    {
        var result = _validator.Validate(Command(Today, Today.AddDays(29)));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Accepts_a_cover_starting_in_the_future()
    {
        var result = _validator.Validate(Command(Today.AddDays(60), Today.AddDays(90)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Rejects_a_start_date_in_the_past()
    {
        var result = _validator.Validate(Command(Today.AddDays(-1), Today.AddDays(30)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Property == nameof(CreateCoverCommand.StartDate));
    }

    [Fact]
    public void Rejects_an_end_date_before_the_start_date()
    {
        var result = _validator.Validate(Command(Today.AddDays(10), Today.AddDays(9)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Property == nameof(CreateCoverCommand.EndDate));
    }

    [Fact]
    public void Accepts_a_single_day_cover()
    {
        var result = _validator.Validate(Command(Today, Today));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Accepts_a_period_of_exactly_one_year()
    {
        var result = _validator.Validate(Command(Today, Today.AddYears(1).AddDays(-1)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Rejects_a_period_longer_than_one_year()
    {
        var result = _validator.Validate(Command(Today, Today.AddYears(1)));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.Property == nameof(CreateCoverCommand.EndDate) && e.Message.Contains("1 year"));
    }

    [Fact]
    public void Measures_one_year_by_the_calendar_including_a_leap_day()
    {
        var validator = new CreateCoverCommandValidator(new FakeClock(new DateOnly(2028, 1, 1)));
        var start = new DateOnly(2028, 1, 1);

        Assert.True(validator.Validate(Command(start, new DateOnly(2028, 12, 31))).IsValid);
        Assert.False(validator.Validate(Command(start, new DateOnly(2029, 1, 1))).IsValid);
    }

    [Fact]
    public void Does_not_report_the_one_year_rule_when_the_dates_are_reversed()
    {
        var result = _validator.Validate(Command(Today.AddDays(10), Today.AddDays(5)));

        Assert.DoesNotContain(result.Errors, e => e.Message.Contains("1 year"));
    }

    [Fact]
    public void Rejects_an_unrecognised_cover_type()
    {
        var result = _validator.Validate(Command(Today, Today.AddDays(30), (CoverType)99));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Property == nameof(CreateCoverCommand.Type));
    }

    [Fact]
    public void Reports_every_broken_rule_at_once()
    {
        var result = _validator.Validate(Command(Today.AddDays(-5), Today.AddDays(-10), (CoverType)99));

        Assert.Equal(3, result.Errors.Count);
    }
}