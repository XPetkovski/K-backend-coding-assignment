using Claims.Core.Features.Claims;
using Claims.Core.Features.Covers;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

namespace Claims.Infrastructure.Mongo;

public class ClaimsContext : DbContext
{
    // Must be the generic DbContextOptions<ClaimsContext> with two contexts registered, the
    // non-generic form resolves whichever was registered last, so it only worked by accident.
    public ClaimsContext(DbContextOptions<ClaimsContext> options)
        : base(options)
    {
    }

    public DbSet<Claim> Claims { get; init; } = null!;
    public DbSet<Cover> Covers { get; init; } = null!;

    // Replaces the [BsonElement] attributes that used to sit on the entities, so
    // Claims.Core stays free of MongoDB types. Stored element names are unchanged.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var claim = modelBuilder.Entity<Claim>();
        claim.ToCollection("claims");
        claim.HasKey(x => x.Id);
        claim.Property(x => x.CoverId).HasElementName("coverId");
        claim.Property(x => x.Created).HasElementName("created");
        claim.Property(x => x.Name).HasElementName("name");
        claim.Property(x => x.Type).HasElementName("claimType");
        claim.Property(x => x.DamageCost).HasElementName("damageCost");

        var cover = modelBuilder.Entity<Cover>();
        cover.ToCollection("covers");
        cover.HasKey(x => x.Id);
        cover.Property(x => x.StartDate).HasElementName("startDate");
        cover.Property(x => x.EndDate).HasElementName("endDate");
        cover.Property(x => x.Type).HasElementName("claimType");
        cover.Property(x => x.Premium).HasElementName("premium");
    }
}