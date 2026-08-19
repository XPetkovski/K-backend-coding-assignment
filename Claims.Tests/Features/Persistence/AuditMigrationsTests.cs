using Claims.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Claims.Tests.Features.Persistence;

// Regression: the 2022 migration snapshot mapped the audit string columns as NOT NULL, while the
// entities declare them as `string?`. EF Core 9 turned PendingModelChangesWarning into a thrown
// error, so Database.Migrate() failed on startup — which took out all 52 integration tests at once.
// This compares model to snapshot in memory, so the drift is caught here instead of at boot.
public class AuditMigrationsTests
{
    [Fact]
    public void Audit_model_matches_the_latest_migration()
    {
        using var context = new AuditContext(
            new DbContextOptionsBuilder<AuditContext>()
                .UseSqlServer("Server=unused;Database=unused")
                .Options);

        Assert.False(
            context.Database.HasPendingModelChanges(),
            "AuditContext has model changes with no migration. Run: dotnet ef migrations add <Name> "
            + "--project Claims.Infrastructure --context AuditContext");
    }
}