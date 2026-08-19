using Claims.Core.Common;
using Claims.Core.Features.Auditing;
using Claims.Core.Features.Claims;
using Claims.Core.Features.Covers;
using Claims.Tests.Fakes;
using Xunit;

namespace Claims.Tests.Features.Claims;

public class ClaimsServiceTests
{
    private static readonly DateOnly Today = new(2026, 8, 17);
    private static readonly DateOnly CoverStart = new(2026, 9, 1);
    private static readonly DateOnly CoverEnd = new(2026, 9, 30);
    private const string CoverId = "cover-1";

    private readonly InMemoryClaimRepository _claims = new();
    private readonly InMemoryCoverRepository _covers = new();
    private readonly RecordingAuditTrail _auditTrail = new();
    private readonly ClaimsService _service;

    public ClaimsServiceTests()
    {
        _covers.Items.Add(new Cover
        {
            Id = CoverId,
            StartDate = CoverStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            EndDate = CoverEnd.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Type = CoverType.Yacht,
            Premium = 1_000m
        });

        _service = new ClaimsService(
            _claims,
            _covers,
            new CreateClaimCommandValidator(),
            _auditTrail,
            new FakeClock(Today));
    }

    private static CreateClaimCommand Command(DateOnly? created = null, decimal damageCost = 500m, string coverId = CoverId) =>
        new()
        {
            CoverId = coverId,
            Name = "Hull damage",
            Type = ClaimType.Collision,
            DamageCost = damageCost,
            Created = created ?? CoverStart
        };

    [Fact]
    public async Task Creates_a_claim_and_assigns_an_id()
    {
        var response = await _service.CreateAsync(Command(), TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(response.Id));
        Assert.Equal(CoverId, response.CoverId);
        Assert.Single(_claims.Items);
        Assert.Equal(response.Id, _claims.Items[0].Id);
    }

    [Fact]
    public async Task Records_an_audit_event_when_a_claim_is_created()
    {
        var response = await _service.CreateAsync(Command(), TestContext.Current.CancellationToken);

        var recorded = Assert.Single(_auditTrail.Events);
        Assert.Equal(AuditedEntity.Claim, recorded.Entity);
        Assert.Equal(AuditAction.Created, recorded.Action);
        Assert.Equal(response.Id, recorded.EntityId);
    }

    [Fact]
    public async Task Stamps_the_audit_event_with_utc_time_from_the_clock()
    {
        await _service.CreateAsync(Command(), TestContext.Current.CancellationToken);

        var recorded = Assert.Single(_auditTrail.Events);
        Assert.Equal(DateTimeKind.Utc, recorded.OccurredAtUtc.Kind);
        Assert.Equal(Today, DateOnly.FromDateTime(recorded.OccurredAtUtc));
    }

    [Fact]
    public async Task Rejects_a_claim_that_breaks_a_validation_rule()
    {
        var command = Command(damageCost: 100_001m);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(command, TestContext.Current.CancellationToken));

        Assert.Contains(exception.Errors, error => error.Property == nameof(CreateClaimCommand.DamageCost));
        Assert.Empty(_claims.Items);
        Assert.Empty(_auditTrail.Events);
    }

    [Fact]
    public async Task Rejects_a_claim_for_a_cover_that_does_not_exist()
    {
        var command = Command(coverId: "missing-cover");

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.CreateAsync(command, TestContext.Current.CancellationToken));

        Assert.Empty(_claims.Items);
    }

    [Theory]
    [InlineData(2026, 9, 1)]
    [InlineData(2026, 9, 15)]
    [InlineData(2026, 9, 30)]
    public async Task Accepts_a_created_date_inside_the_cover_period(int year, int month, int day)
    {
        var command = Command(created: new DateOnly(year, month, day));

        var response = await _service.CreateAsync(command, TestContext.Current.CancellationToken);

        Assert.Equal(new DateOnly(year, month, day), response.Created);
    }

    [Theory]
    [InlineData(2026, 8, 31)]
    [InlineData(2026, 10, 1)]
    public async Task Rejects_a_created_date_outside_the_cover_period(int year, int month, int day)
    {
        var command = Command(created: new DateOnly(year, month, day));

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => _service.CreateAsync(command, TestContext.Current.CancellationToken));

        Assert.Equal(nameof(CreateClaimCommand.Created), exception.Property);
        Assert.Empty(_claims.Items);
        Assert.Empty(_auditTrail.Events);
    }

    [Fact]
    public async Task Returns_every_stored_claim()
    {
        await _service.CreateAsync(Command(), TestContext.Current.CancellationToken);
        await _service.CreateAsync(Command(created: CoverEnd), TestContext.Current.CancellationToken);

        var all = await _service.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Throws_when_reading_a_claim_that_does_not_exist()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.GetAsync("missing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Deletes_a_claim_and_audits_it()
    {
        var created = await _service.CreateAsync(Command(), TestContext.Current.CancellationToken);
        _auditTrail.Events.Clear();

        await _service.DeleteAsync(created.Id, TestContext.Current.CancellationToken);

        Assert.Empty(_claims.Items);
        var recorded = Assert.Single(_auditTrail.Events);
        Assert.Equal(AuditAction.Deleted, recorded.Action);
        Assert.Equal(created.Id, recorded.EntityId);
    }

    [Fact]
    public async Task Does_not_audit_a_delete_of_a_claim_that_does_not_exist()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.DeleteAsync("missing", TestContext.Current.CancellationToken));

        Assert.Empty(_auditTrail.Events);
    }
}