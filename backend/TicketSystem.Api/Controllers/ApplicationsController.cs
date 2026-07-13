using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketSystem.Api.Models.DTOs.Applications;
using TicketSystem.Api.Services;

namespace TicketSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _appService;

    public ApplicationsController(IApplicationService appService) => _appService = appService;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] ApplicationQueryParams query)
        => Ok(await _appService.GetPagedAsync(query));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _appService.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplicationRequest request)
        => Ok(await _appService.CreateAsync(request));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateApplicationRequest request)
        => Ok(await _appService.UpdateAsync(id, request));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _appService.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup()
        => Ok(await _appService.GetLookupAsync());

    [HttpPut("{id}/users")]
    public async Task<IActionResult> UpdateUsers(int id, [FromBody] UpdateApplicationUsersRequest request)
    {
        await _appService.UpdateUsersAsync(id, request.UserIds, request.PrimarySpocUserId);
        return NoContent();
    }
}
