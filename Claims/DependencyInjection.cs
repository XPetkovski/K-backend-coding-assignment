using Claims.Core.Common;
using Claims.Core.Features.Claims;
using Claims.Core.Features.Covers;

namespace Claims;

public static class DependencyInjection
{
    public static IServiceCollection AddClaimsCore(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IPremiumCalculator, PremiumCalculator>();

        services.AddScoped<IValidator<CreateClaimCommand>, CreateClaimCommandValidator>();
        services.AddScoped<IValidator<CreateCoverCommand>, CreateCoverCommandValidator>();

        services.AddScoped<IClaimsService, ClaimsService>();
        services.AddScoped<ICoversService, CoversService>();

        return services;
    }
}