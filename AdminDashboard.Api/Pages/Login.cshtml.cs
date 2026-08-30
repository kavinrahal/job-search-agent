using System.Security.Claims;
using AdminDashboard.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AdminDashboard.Api.Pages;

// The one gate in front of everything else — a single shared secret (AdminPortalSecret),
// compared with a constant-time equality check (AdminSecretVerifier), same pattern as
// JobSearch.Api's SendGrid/Sentry webhook secret checks. On success, signs in under the
// independent "AdminAuth" cookie scheme (AdminAuthConstants.Scheme) — see Program.cs's own
// comment for why that's deliberately unrelated to JobSearch.Api's session cookie.
[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IConfiguration _config;

    public LoginModel(IConfiguration config)
    {
        _config = config;
    }

    [BindProperty]
    public string? Secret { get; set; }

    public string? Error { get; set; }

    public IActionResult OnGet()
    {
        // Already signed in — no reason to show the gate again.
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var configuredSecret = _config["AdminPortalSecret"];
        if (!AdminSecretVerifier.IsValid(Secret, configuredSecret))
        {
            Error = "Incorrect secret.";
            return Page();
        }

        // Single-admin tool — one fixed "owner" identity, nothing to look up. See
        // AdminAuditLog's own doc comment for the same reasoning applied to the audit trail.
        var identity = new ClaimsIdentity(AdminAuthConstants.Scheme);
        identity.AddClaim(new Claim(ClaimTypes.Name, "owner"));
        await HttpContext.SignInAsync(AdminAuthConstants.Scheme, new ClaimsPrincipal(identity));

        return RedirectToPage("/Index");
    }
}
