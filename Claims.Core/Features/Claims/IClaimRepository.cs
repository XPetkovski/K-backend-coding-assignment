namespace Claims.Core.Features.Claims;

public interface IClaimRepository
{
    Task<IReadOnlyList<Claim>> GetAllAsync(CancellationToken cancellationToken);

    Task<Claim?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task AddAsync(Claim claim, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}