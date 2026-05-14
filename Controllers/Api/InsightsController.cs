using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StudentManagementSystem.Models.AI.Dtos;
using StudentManagementSystem.Services.AI;

namespace StudentManagementSystem.Controllers.Api;

[ApiController]
[Route("api/ai/[controller]")]
[Authorize(Roles = "Admin")]
[IgnoreAntiforgeryToken]
[EnableRateLimiting("ai")]
public class InsightsController : ControllerBase
{
    private readonly IAiDashboardComposer _composer;
    private readonly IAiSecurityContextFactory _securityFactory;

    public InsightsController(IAiDashboardComposer composer, IAiSecurityContextFactory securityFactory)
    {
        _composer = composer;
        _securityFactory = securityFactory;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(AiDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiDashboardDto>> Dashboard(CancellationToken cancellationToken)
    {
        var security = await _securityFactory.CreateAsync(User, cancellationToken);
        var dto = await _composer.BuildAsync(security, cancellationToken);
        return Ok(dto);
    }
}
