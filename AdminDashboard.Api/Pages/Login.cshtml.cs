using System.Security.Claims;
using AdminDashboard.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminDashboard.Api.Pages;

// The one gate in front of everything else — a shared username/password pair
// (AdminPortalUsername/AdminPortalPassword), compared with a constant-time equality check
// (AdminCredentialVerifier), same pattern as JobSearch.Api's SendGrid/Sentry webhook secret
// checks. On success, signs in under the independent "AdminAuth" cookie scheme
// (AdminAuthConstants.Scheme) — see Program.cs's own comment for why that's deliberately
// unrelated to JobSearch.Api's session cookie.
[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IConfiguration _config;

    public LoginModel(IConfiguration config)
    {
        _config = config;
    }

    [BindProperty]
    public string? Username { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public string? Error { get; set; }

    public IActionResult OnGet()
    {
        // Already signed in — no reason to show the gate again.
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var configuredUsername = _config["AdminPortalUsername"];
        var configuredPassword = _config["AdminPortalPassword"];
        if (!AdminCredentialVerifier.IsValid(Username, Password, configuredUsername, configuredPassword))
        {
            Error = "Incorrect username or password.";
            return Page();
        }

        // Single-admin tool — still one fixed identity behind the scenes (no user lookup, no
        // account table), but the claim now carries the real configured username instead of a
        // hardcoded "owner" string. AdminAuditService still logs the actions themselves under
        // its own separate "owner" constant — not wired to this claim, by design, since
        // changing that is a separate follow-up, not part of this login change.
        var identity = new ClaimsIdentity(AdminAuthConstants.Scheme);
        // Non-null: AdminCredentialVerifier.IsValid already returned true above, which requires
        // configuredUsername to be non-empty.
        identity.AddClaim(new Claim(ClaimTypes.Name, configuredUsername!));
        await HttpContext.SignInAsync(AdminAuthConstants.Scheme, new ClaimsPrincipal(identity));

        return RedirectToPage("/Index");
    }
}
