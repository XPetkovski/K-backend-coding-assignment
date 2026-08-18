using Claims.Core.Features.Claims;
using Claims.Core.Features.Covers;

namespace Claims.Tests.Fakes;

public sealed class InMemoryClaimRepository : IClaimRepository
{
    public List<Claim> Items { get; } = [];

    public Task<IReadOnlyList<Claim>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Claim>>(Items.ToList());

    public Task<Claim?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(claim => claim.Id == id));

    public Task AddAsync(Claim claim, CancellationToken cancellationToken)
    {
        Items.Add(claim);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var claim = Items.SingleOrDefault(item => item.Id == id);
        if (claim is null)
        {
            return Task.FromResult(false);
        }

        Items.Remove(claim);
        return Task.FromResult(true);
    }
}

public sealed class InMemoryCoverRepository : ICoverRepository
{
    public List<Cover> Items { get; } = [];

    public Task<IReadOnlyList<Cover>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Cover>>(Items.ToList());

    public Task<Cover?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.SingleOrDefault(cover => cover.Id == id));

    public Task AddAsync(Cover cover, CancellationToken cancellationToken)
    {
        Items.Add(cover);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var cover = Items.SingleOrDefault(item => item.Id == id);
        if (cover is null)
        {
            return Task.FromResult(false);
        }

        Items.Remove(cover);
        return Task.FromResult(true);
    }
}