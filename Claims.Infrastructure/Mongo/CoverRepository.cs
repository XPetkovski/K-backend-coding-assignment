using Claims.Core.Features.Covers;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Mongo;

public class CoverRepository : ICoverRepository
{
    private readonly ClaimsContext _context;

    public CoverRepository(ClaimsContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Cover>> GetAllAsync(CancellationToken cancellationToken) =>
        await _context.Covers.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<Cover?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await _context.Covers
            .AsNoTracking()
            .SingleOrDefaultAsync(cover => cover.Id == id, cancellationToken);

    public async Task AddAsync(Cover cover, CancellationToken cancellationToken)
    {
        _context.Covers.Add(cover);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var cover = await _context.Covers.SingleOrDefaultAsync(cover => cover.Id == id, cancellationToken);
        if (cover is null)
        {
            return false;
        }

        _context.Covers.Remove(cover);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}