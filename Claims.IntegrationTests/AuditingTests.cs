using System.Net.Http.Json;
using Claims.Core.Features.Claims;
using Claims.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Claims.IntegrationTests;

// Exercises the real path: request thread queues, background service drains, EfAuditWriter
// persists through its own DI scope.
[Collection(nameof(ClaimsApiCollection))]
public class AuditingTests : ApiTestBase
{
    public AuditingTests(ClaimsApiFixture fixture) : base(fixture)
    {
    }

    private async Task<ClaimAudit?> FindClaimAuditAsync(string claimId, string requestType)
    {
        using var scope = Fixture.Services.CreateScope();
        var auditContext = scope.ServiceProvider.GetRequiredService<AuditContext>();

        return await auditContext.ClaimAudits
            .AsNoTracking()
            .FirstOrDefaultAsync(audit => audit.ClaimId == claimId && audit.HttpRequestType == requestType, Ct);
    }

    private async Task<CoverAudit?> FindCoverAuditAsync(string coverId, string requestType)
    {
        using var scope = Fixture.Services.CreateScope();
        var auditContext = scope.ServiceProvider.GetRequiredService<AuditContext>();

        return await auditContext.CoverAudits
            .AsNoTracking()
            .FirstOrDefaultAsync(audit => audit.CoverId == coverId && audit.HttpRequestType == requestType, Ct);
    }

    [Fact]
    public async Task Creating_a_claim_eventually_writes_an_audit_row()
    {
        var cover = await CreateCoverAsync();
        var claim = await CreateClaimAsync(cover.Id, cover.StartDate);

        var audit = await EventuallyAsync(() => FindClaimAuditAsync(claim.Id, "POST"));

        Assert.NotNull(audit);
        Assert.Equal(claim.Id, audit!.ClaimId);
    }

    [Fact]
    public async Task Deleting_a_claim_eventually_writes_a_delete_audit_row()
    {
        var cover = await CreateCoverAsync();
        var claim = await CreateClaimAsync(cover.Id, cover.StartDate);

        await Client.DeleteAsync($"/Claims/{claim.Id}", Ct);

        var audit = await EventuallyAsync(() => FindClaimAuditAsync(claim.Id, "DELETE"));

        Assert.NotNull(audit);
    }

    [Fact]
    public async Task Creating_a_cover_eventually_writes_an_audit_row()
    {
        var cover = await CreateCoverAsync();

        var audit = await EventuallyAsync(() => FindCoverAuditAsync(cover.Id, "POST"));

        Assert.NotNull(audit);
        Assert.Equal(cover.Id, audit!.CoverId);
    }

    [Fact]
    public async Task Audit_rows_are_stamped_in_utc()
    {
        var cover = await CreateCoverAsync();

        var audit = await EventuallyAsync(() => FindCoverAuditAsync(cover.Id, "POST"));

        Assert.NotNull(audit);
        // The old implementation used DateTime.Now; allow generous slack for CI clocks.
        Assert.True(
            Math.Abs((DateTime.UtcNow - audit!.Created).TotalMinutes) < 10,
            $"Audit timestamp {audit.Created:O} is not close to UTC now.");
    }

    // A rejected request must not appear in the audit trail.
    [Fact]
    public async Task A_rejected_claim_writes_no_audit_row()
    {
        var cover = await CreateCoverAsync();
        var command = new CreateClaimCommand
        {
            CoverId = cover.Id,
            Name = "Too expensive",
            Type = ClaimType.Fire,
            DamageCost = 100_001m,
            Created = cover.StartDate
        };

        var before = await CountClaimAuditsAsync();

        var response = await Client.PostAsJsonAsync("/Claims", command, Json, Ct);
        Assert.False(response.IsSuccessStatusCode);

        await Task.Delay(TimeSpan.FromSeconds(1), Ct);

        Assert.Equal(before, await CountClaimAuditsAsync());
    }

    private async Task<int> CountClaimAuditsAsync()
    {
        using var scope = Fixture.Services.CreateScope();
        var auditContext = scope.ServiceProvider.GetRequiredService<AuditContext>();

        return await auditContext.ClaimAudits.AsNoTracking().CountAsync(Ct);
    }

    // The original controller audited deletes before performing them.
    [Fact]
    public async Task Deleting_a_claim_that_does_not_exist_writes_no_audit_row()
    {
        var missingId = Guid.NewGuid().ToString();

        await Client.DeleteAsync($"/Claims/{missingId}", Ct);
        await Task.Delay(TimeSpan.FromSeconds(1), Ct);

        Assert.Null(await FindClaimAuditAsync(missingId, "DELETE"));
    }
}