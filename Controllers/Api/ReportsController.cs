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
public class ReportsController : ControllerBase
{
    private readonly IAiReportGenerationService _reports;
    private readonly IAiSecurityContextFactory _securityFactory;

    public ReportsController(IAiReportGenerationService reports, IAiSecurityContextFactory securityFactory)
    {
        _reports = reports;
        _securityFactory = securityFactory;
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] AiReportRequestDto request, CancellationToken cancellationToken)
    {
        var security = await _securityFactory.CreateAsync(User, cancellationToken);
        try
        {
            if (string.Equals(request.Format, "pdf", StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.ReportType, "executive_summary", StringComparison.OrdinalIgnoreCase))
            {
                var pdf = await _reports.ExecutiveSummaryPdfAsync(security, cancellationToken);
                return File(pdf, "application/pdf", $"sms-executive-summary-{DateTime.UtcNow:yyyyMMdd}.pdf");
            }

            if (string.Equals(request.Format, "xlsx", StringComparison.OrdinalIgnoreCase)
                && string.Equals(request.ReportType, "fee_defaulters", StringComparison.OrdinalIgnoreCase))
            {
                var xlsx = await _reports.FeeDefaultersExcelAsync(request.ClassFilter, security, cancellationToken);
                return File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"fee-defaulters-{DateTime.UtcNow:yyyyMMdd}.xlsx");
            }

            return BadRequest(new { error = "Unsupported reportType/format combination." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
