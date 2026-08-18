using Claims.Core.Features.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Features.Claims;

[ApiController]
[Route("[controller]")]
[Produces("application/json")]
public class ClaimsController : ControllerBase
{
    private readonly IClaimsService _claims;

    public ClaimsController(IClaimsService claims)
    {
        _claims = claims;
    }

    /// <summary>Lists every claim.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClaimResponse>>> GetAsync(CancellationToken cancellationToken) =>
        Ok(await _claims.GetAllAsync(cancellationToken));

    /// <summary>Gets a single claim by id.</summary>
    [HttpGet("{id}", Name = nameof(GetClaimAsync))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimResponse>> GetClaimAsync(string id, CancellationToken cancellationToken) =>
        Ok(await _claims.GetAsync(id, cancellationToken));

    /// <summary>Creates a claim against an existing cover.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClaimResponse>> CreateAsync(
        CreateClaimCommand command,
        CancellationToken cancellationToken)
    {
        var claim = await _claims.CreateAsync(command, cancellationToken);

        return CreatedAtRoute(nameof(GetClaimAsync), new { id = claim.Id }, claim);
    }

    /// <summary>Deletes a claim.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await _claims.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}