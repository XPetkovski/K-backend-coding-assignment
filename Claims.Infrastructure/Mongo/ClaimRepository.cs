using Claims.Core.Features.Claims;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Mongo;

public class ClaimRepository : IClaimRepository
{
    private readonly ClaimsContext _context;

    public ClaimRepository(ClaimsContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Claim>> GetAllAsync(CancellationToken cancellationToken) =>
        await _context.Claims.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<Claim?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await _context.Claims
            .AsNoTracking()
            .SingleOrDefaultAsync(claim => claim.Id == id, cancellationToken);

    public async Task AddAsync(Claim claim, CancellationToken cancellationToken)
    {
        _context.Claims.Add(claim);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var claim = await _context.Claims.SingleOrDefaultAsync(claim => claim.Id == id, cancellationToken);
        if (claim is null)
        {
            return false;
        }

        _context.Claims.Remove(claim);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}