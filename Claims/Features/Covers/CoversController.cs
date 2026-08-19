using Claims.Core.Features.Covers;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Features.Covers;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class CoversController : ControllerBase
{
    private readonly ICoversService _covers;

    public CoversController(ICoversService covers)
    {
        _covers = covers;
    }

    /// <summary>Quotes the premium for a period without storing a cover.</summary>
    [HttpGet("compute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<decimal> ComputePremium(DateOnly startDate, DateOnly endDate, CoverType coverType) =>
        Ok(_covers.ComputePremium(startDate, endDate, coverType));

    /// <summary>Lists every cover.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CoverResponse>>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await _covers.GetAllAsync(cancellationToken));

    /// <summary>Gets a single cover by id.</summary>
    [HttpGet("{id}", Name = nameof(GetCoverAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoverResponse>> GetCoverAsync(string id, CancellationToken cancellationToken) =>
        Ok(await _covers.GetAsync(id, cancellationToken));

    /// <summary>Creates a cover and stores its computed premium.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CoverResponse>> CreateAsync(
        CreateCoverCommand command,
        CancellationToken cancellationToken)
    {
        var cover = await _covers.CreateAsync(command, cancellationToken);

        return CreatedAtRoute(nameof(GetCoverAsync), new { id = cover.Id }, cover);
    }

    /// <summary>Deletes a cover.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _covers.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}