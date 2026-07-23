using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketSystem.Api.Models.DTOs.Applications;
using TicketSystem.Api.Models.DTOs.Departments;
using TicketSystem.Api.Models.DTOs.Users;
using TicketSystem.Api.Services;

namespace TicketSystem.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _deptService;

    public DepartmentsController(IDepartmentService deptService) => _deptService = deptService;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] DepartmentQueryParams query)
        => Ok(await _deptService.GetPagedAsync(query));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _deptService.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentRequest request)
    {
        try { return Ok(await _deptService.CreateAsync(request)); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDepartmentRequest request)
    {
        try { return Ok(await _deptService.UpdateAsync(id, request)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _deptService.DeleteAsync(id); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return Conflict("Cannot delete department: it has associated users or routing rules.");
        }
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup()
        => Ok(await _deptService.GetLookupAsync());
}
