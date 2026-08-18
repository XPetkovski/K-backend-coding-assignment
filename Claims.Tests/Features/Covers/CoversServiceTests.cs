using Claims.Core.Common;
using Claims.Core.Features.Auditing;
using Claims.Core.Features.Covers;
using Claims.Tests.Fakes;
using Xunit;

namespace Claims.Tests.Features.Covers;

public class CoversServiceTests
{
    private static readonly DateOnly Today = new(2026, 8, 17);

    private readonly InMemoryCoverRepository _covers = new();
    private readonly RecordingAuditTrail _auditTrail = new();
    private readonly CoversService _service;

    public CoversServiceTests()
    {
        var clock = new FakeClock(Today);

        _service = new CoversService(
            _covers,
            new CreateCoverCommandValidator(clock),
            new PremiumCalculator(),
            _auditTrail,
            clock);
    }

    private static CreateCoverCommand Command(int days = 30, CoverType type = CoverType.Yacht) =>
        new(Today, Today.AddDays(days - 1), type);

    [Fact]
    public async Task Creates_a_cover_and_assigns_an_id()
    {
        var response = await _service.CreateAsync(Command(), TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(response.Id));
        Assert.Single(_covers.Items);
    }

    [Fact]
    public async Task Stores_the_computed_premium()
    {
        var response = await _service.CreateAsync(Command(days: 30, type: CoverType.Yacht), TestContext.Current.CancellationToken);

        Assert.Equal(41_250.00m, response.Premium);
        Assert.Equal(41_250.00m, _covers.Items[0].Premium);
    }

    [Fact]
    public async Task Round_trips_the_requested_dates()
    {
        var command = Command(days: 90);

        var response = await _service.CreateAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal(command.StartDate, response.StartDate);
        Assert.Equal(command.EndDate, response.EndDate);
    }

    [Fact]
    public async Task Records_an_audit_event_when_a_cover_is_created()
    {
        var response = await _service.CreateAsync(Command(), TestContext.Current.CancellationToken);

        var recorded = Assert.Single(_auditTrail.Events);
        Assert.Equal(AuditedEntity.Cover, recorded.Entity);
        Assert.Equal(AuditAction.Created, recorded.Action);
        Assert.Equal(response.Id, recorded.EntityId);
    }

    [Fact]
    public async Task Rejects_a_cover_starting_in_the_past()
    {
        var command = new CreateCoverCommand(Today.AddDays(-1), Today.AddDays(30), CoverType.Yacht);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(command, TestContext.Current.CancellationToken));

        Assert.Contains(exception.Errors, error => error.Property == nameof(CreateCoverCommand.StartDate));
        Assert.Empty(_covers.Items);
        Assert.Empty(_auditTrail.Events);
    }

    [Fact]
    public async Task Rejects_a_period_longer_than_one_year()
    {
        var command = new CreateCoverCommand(Today, Today.AddYears(1), CoverType.Yacht);

        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(command, TestContext.Current.CancellationToken));

        Assert.Empty(_covers.Items);
    }

    [Fact]
    public async Task Throws_when_reading_a_cover_that_does_not_exist()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetAsync("missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Deletes_a_cover_and_audits_it()
    {
        var created = await _service.CreateAsync(Command(), TestContext.Current.CancellationToken);
        _auditTrail.Events.Clear();

        await _service.DeleteAsync(created.Id, TestContext.Current.CancellationToken);

        Assert.Empty(_covers.Items);
        var recorded = Assert.Single(_auditTrail.Events);
        Assert.Equal(AuditAction.Deleted, recorded.Action);
    }

    [Fact]
    public async Task Does_not_audit_a_delete_of_a_cover_that_does_not_exist()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DeleteAsync("missing", TestContext.Current.CancellationToken));

        Assert.Empty(_auditTrail.Events);
    }

    [Fact]
    public void Quotes_a_premium_without_storing_a_cover()
    {
        var premium = _service.ComputePremium(Today, Today.AddDays(29), CoverType.Yacht);

        Assert.Equal(41_250.00m, premium);
        Assert.Empty(_covers.Items);
        Assert.Empty(_auditTrail.Events);
    }
}