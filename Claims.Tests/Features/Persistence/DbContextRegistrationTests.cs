using Claims.Infrastructure.Auditing;
using Claims.Infrastructure.Mongo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.EntityFrameworkCore.Extensions;
using Xunit;

namespace Claims.Tests.Features.Persistence;

// ClaimsContext used to take a non-generic DbContextOptions, which resolves whichever context was
// registered last. It worked only because ClaimsContext happened to be registered second.
public class DbContextRegistrationTests
{
    private const string Mongo = "mongodb://localhost:27017";
    private const string Sql = "Server=localhost;Database=Claims.Audit;Integrated Security=true";

    [Fact]
    public void Each_context_gets_its_own_provider_whatever_the_registration_order()
    {
        using var claimsFirst = Build(registerClaimsFirst: true);
        using var auditFirst = Build(registerClaimsFirst: false);

        foreach (var provider in new[] { claimsFirst, auditFirst })
        {
            using var scope = provider.CreateScope();

            Assert.Equal(
                "MongoDB.EntityFrameworkCore",
                scope.ServiceProvider.GetRequiredService<ClaimsContext>().Database.ProviderName);

            Assert.Equal(
                "Microsoft.EntityFrameworkCore.SqlServer",
                scope.ServiceProvider.GetRequiredService<AuditContext>().Database.ProviderName);
        }
    }

    private static ServiceProvider Build(bool registerClaimsFirst)
    {
        var services = new ServiceCollection();

        if (registerClaimsFirst)
        {
            AddClaims(services);
            AddAudit(services);
        }
        else
        {
            AddAudit(services);
            AddClaims(services);
        }

        return services.BuildServiceProvider();
    }

    private static void AddClaims(IServiceCollection services) =>
        services.AddDbContext<ClaimsContext>(options => options.UseMongoDB(Mongo, "Claims"));

    private static void AddAudit(IServiceCollection services) =>
        services.AddDbContext<AuditContext>(options => options.UseSqlServer(Sql));
}