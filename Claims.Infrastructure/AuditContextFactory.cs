using Claims.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Claims.Infrastructure;

public class AuditContextFactory : IDesignTimeDbContextFactory<AuditContext>
{
    public AuditContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("AUDIT_DB_CONNECTION")
                               ?? "Server=localhost;Database=Claims.Audit;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AuditContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AuditContext(options);
    }
}