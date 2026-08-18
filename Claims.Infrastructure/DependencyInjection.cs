using Claims.Core.Features.Auditing;
using Claims.Core.Features.Claims;
using Claims.Core.Features.Covers;
using Claims.Infrastructure.Auditing;
using Claims.Infrastructure.Mongo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Claims.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        PersistenceOptions persistence)
    {
        persistence.Validate();

        services.AddDbContext<AuditContext>(options =>
            options.UseSqlServer(persistence.AuditDbConnectionString));

        services.AddDbContext<ClaimsContext>(options =>
            options.UseMongoDB(persistence.Mongo.ConnectionString, persistence.Mongo.DatabaseName));

        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<ICoverRepository, CoverRepository>();

        return services.AddQueuedAuditing(configuration);
    }
    
    private static IServiceCollection AddQueuedAuditing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AuditQueueOptions>(configuration.GetSection(AuditQueueOptions.SectionName));

        services.AddSingleton<AuditEventChannel>();
        services.AddSingleton<IAuditWriter, EfAuditWriter>();
        services.AddSingleton<IAuditTrail, QueuedAuditTrail>();
        services.AddHostedService<AuditBackgroundService>();

        return services;
    }
}