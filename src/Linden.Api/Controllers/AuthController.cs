using System.Security.Claims;
using Linden.Api.Data;
using Linden.Api.Dtos;
using Linden.Api.Models;
using Linden.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Linden.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<IdentityUser> _signIn;
    private readonly UserManager<IdentityUser> _users;
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public AuthController(SignInManager<IdentityUser> signIn, UserManager<IdentityUser> users,
        AppDbContext db, IAuditService audit)
    {
        _signIn = signIn;
        _users = users;
        _db = db;
        _audit = audit;
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<ActionResult<MeDto>> Login(LoginRequest request)
    {
        var result = await _signIn.PasswordSignInAsync(request.Email, request.Password,
            isPersistent: false, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            // The cookie is not set yet on this request, so the user name is passed in by hand.
            _audit.Add(AuditActions.LoginFailed, "User",
                details: result.IsLockedOut ? "account locked" : null,
                userName: Shorten(request.Email));
            await _db.SaveChangesAsync();

            return Problem(
                title: result.IsLockedOut ? "Account locked" : "Sign in failed",
                detail: result.IsLockedOut
                    ? "Too many failed attempts. Try again in 15 minutes."
                    : "Wrong email or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = (await _users.FindByEmailAsync(request.Email))!;
        var roles = await _users.GetRolesAsync(user);

        _audit.Add(AuditActions.LoginSucceeded, "User", userName: user.Email);
        await _db.SaveChangesAsync();

        return new MeDto(user.Email!, roles.FirstOrDefault() ?? "");
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    public ActionResult<MeDto> Me()
        => new MeDto(User.FindFirstValue(ClaimTypes.Name) ?? "", User.FindFirstValue(ClaimTypes.Role) ?? "");

    private static string Shorten(string s) => s.Length <= 100 ? s : s[..100];
}
