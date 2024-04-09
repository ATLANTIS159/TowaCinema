using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace TowaCinema.Web.Auth;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    [HttpGet]
    [Route("signin")]
    public IActionResult TwitchSignIn([FromQuery] string? page)
    {
        return Challenge(
            new AuthenticationProperties { RedirectUri = $"/{(string.IsNullOrWhiteSpace(page) ? "" : page)}" },
            "Twitch");
    }

    [HttpGet]
    [Route("signout")]
    public async Task<IActionResult> TwitchSignOut([FromQuery] string? page)
    {
        await HttpContext.SignOutAsync("Cookies");
        return Redirect($"/{(string.IsNullOrWhiteSpace(page) ? "" : page)}");
    }
}