using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketSystem.Api.Models.DTOs.Users;
using TicketSystem.Api.Services;

namespace TicketSystem.Api.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService) => _userService = userService;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] UserQueryParams query)
        => Ok(await _userService.GetPagedAsync(query));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _userService.GetByIdAsync(id);
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        try { return Ok(await _userService.CreateAsync(request)); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        try { return Ok(await _userService.UpdateAsync(id, request)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _userService.DeleteAsync(id); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            return Conflict("Cannot delete user: they have associated tickets or records.");
        }
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup([FromQuery] int? roleId = null)
        => Ok(await _userService.GetLookupAsync(roleId));

    [HttpPut("{id}/applications")]
    public async Task<IActionResult> UpdateApplications(int id, [FromBody] UpdateUserApplicationsRequest request)
    {
        await _userService.UpdateApplicationsAsync(id, request.ApplicationIds);
        return NoContent();
    }

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> GenerateResetToken(int id)
    {
        await _userService.GeneratePasswordResetTokenAsync(id);
        return Ok(new { message = "Reset token generated. Check user's RefreshToken field." });
    }
}
