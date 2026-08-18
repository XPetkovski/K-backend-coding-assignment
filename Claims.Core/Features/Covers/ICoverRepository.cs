namespace Claims.Core.Features.Covers;

public interface ICoverRepository
{
    Task<IReadOnlyList<Cover>> GetAllAsync(CancellationToken cancellationToken);

    Task<Cover?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task AddAsync(Cover cover, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}