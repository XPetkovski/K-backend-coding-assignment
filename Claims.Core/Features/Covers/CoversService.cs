using Claims.Core.Common;
using Claims.Core.Features.Auditing;

namespace Claims.Core.Features.Covers;

public interface ICoversService
{
    Task<IReadOnlyList<CoverResponse>> GetAllAsync(CancellationToken cancellationToken);

    Task<CoverResponse> GetAsync(string id, CancellationToken cancellationToken);

    Task<CoverResponse> CreateAsync(CreateCoverCommand command, CancellationToken cancellationToken);

    Task DeleteAsync(string id, CancellationToken cancellationToken);

    decimal ComputePremium(DateOnly startDate, DateOnly endDate, CoverType coverType);
}

public class CoversService : ICoversService
{
    private readonly ICoverRepository _covers;
    private readonly IValidator<CreateCoverCommand> _validator;
    private readonly IPremiumCalculator _premiumCalculator;
    private readonly IAuditTrail _auditTrail;
    private readonly IClock _clock;

    public CoversService(
        ICoverRepository covers,
        IValidator<CreateCoverCommand> validator,
        IPremiumCalculator premiumCalculator,
        IAuditTrail auditTrail,
        IClock clock)
    {
        _covers = covers;
        _validator = validator;
        _premiumCalculator = premiumCalculator;
        _auditTrail = auditTrail;
        _clock = clock;
    }

    public async Task<IReadOnlyList<CoverResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var covers = await _covers.GetAllAsync(cancellationToken);

        return covers.Select(ToResponse).ToList();
    }

    public async Task<CoverResponse> GetAsync(string id, CancellationToken cancellationToken)
    {
        var cover = await _covers.GetByIdAsync(id, cancellationToken)
                    ?? throw NotFound(id);

        return ToResponse(cover);
    }

    public async Task<CoverResponse> CreateAsync(CreateCoverCommand command, CancellationToken cancellationToken)
    {
        _validator.Validate(command).ThrowIfInvalid();

        var cover = new Cover
        {
            Id = Guid.NewGuid().ToString(),
            StartDate = command.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            EndDate = command.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Type = command.Type,
            Premium = ComputePremium(command.StartDate, command.EndDate, command.Type)
        };

        await _covers.AddAsync(cover, cancellationToken);
        _auditTrail.Record(new AuditEvent(AuditedEntity.Cover, cover.Id, AuditAction.Created, _clock.UtcNow));

        return ToResponse(cover);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var deleted = await _covers.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            throw NotFound(id);
        }

        _auditTrail.Record(new AuditEvent(AuditedEntity.Cover, id, AuditAction.Deleted, _clock.UtcNow));
    }

    public decimal ComputePremium(DateOnly startDate, DateOnly endDate, CoverType coverType) =>
        _premiumCalculator.Compute(
            startDate.ToDateTime(TimeOnly.MinValue),
            endDate.ToDateTime(TimeOnly.MinValue),
            coverType);

    private static NotFoundException NotFound(string id) => new($"Cover '{id}' was not found.");

    private static CoverResponse ToResponse(Cover cover) => new(
        cover.Id,
        DateOnly.FromDateTime(cover.StartDate),
        DateOnly.FromDateTime(cover.EndDate),
        cover.Type,
        cover.Premium);
}