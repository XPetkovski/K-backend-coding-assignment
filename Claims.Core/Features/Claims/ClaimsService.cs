using Claims.Core.Common;
using Claims.Core.Features.Auditing;
using Claims.Core.Features.Covers;

namespace Claims.Core.Features.Claims;

public interface IClaimsService
{
    Task<IReadOnlyList<ClaimResponse>> GetAllAsync(CancellationToken cancellationToken);

    Task<ClaimResponse> GetAsync(string id, CancellationToken cancellationToken);

    Task<ClaimResponse> CreateAsync(CreateClaimCommand command, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);
}

public class ClaimsService : IClaimsService
{
    private readonly IClaimRepository _claims;
    private readonly ICoverRepository _covers;
    private readonly IValidator<CreateClaimCommand> _validator;
    private readonly IAuditTrail _auditTrail;
    private readonly IClock _clock;

    public ClaimsService(
        IClaimRepository claims,
        ICoverRepository covers,
        IValidator<CreateClaimCommand> validator,
        IAuditTrail auditTrail,
        IClock clock)
    {
        _claims = claims;
        _covers = covers;
        _validator = validator;
        _auditTrail = auditTrail;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ClaimResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var claims = await _claims.GetAllAsync(cancellationToken);

        return claims.Select(ToResponse).ToList();
    }

    public async Task<ClaimResponse> GetAsync(string id, CancellationToken cancellationToken)
    {
        var claim = await _claims.GetByIdAsync(id, cancellationToken)
                    ?? throw NotFound(id);

        return ToResponse(claim);
    }

    public async Task<ClaimResponse> CreateAsync(CreateClaimCommand command, CancellationToken cancellationToken)
    {
        _validator.Validate(command).ThrowIfInvalid();

        var cover = await _covers.GetByIdAsync(command.CoverId, cancellationToken)
                    ?? throw new NotFoundException($"Cover '{command.CoverId}' was not found.");

        EnsureCreatedWithinCoverPeriod(command.Created, cover);

        var claim = new Claim
        {
            Id = Guid.NewGuid().ToString(),
            CoverId = command.CoverId,
            Name = command.Name,
            Type = command.Type,
            DamageCost = command.DamageCost,
            Created = command.Created.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        };

        await _claims.AddAsync(claim, cancellationToken);
        _auditTrail.Record(new AuditEvent(AuditedEntity.Claim, claim.Id, AuditAction.Created, _clock.UtcNow));

        return ToResponse(claim);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var deleted = await _claims.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            throw NotFound(id);
        }

        _auditTrail.Record(new AuditEvent(AuditedEntity.Claim, id, AuditAction.Deleted, _clock.UtcNow));
    }

    private static void EnsureCreatedWithinCoverPeriod(DateOnly created, Cover cover)
    {
        var start = DateOnly.FromDateTime(cover.StartDate);
        var end = DateOnly.FromDateTime(cover.EndDate);

        if (created < start || created > end)
        {
            throw new DomainException(
                $"Claim created date must fall within the cover period {start:yyyy-MM-dd} to {end:yyyy-MM-dd}.",
                nameof(CreateClaimCommand.Created));
        }
    }

    private static NotFoundException NotFound(string id) => new($"Claim '{id}' was not found.");

    private static ClaimResponse ToResponse(Claim claim) => new(
        claim.Id,
        claim.CoverId,
        claim.Name,
        claim.Type,
        claim.DamageCost,
        DateOnly.FromDateTime(claim.Created));
}