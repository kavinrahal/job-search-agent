namespace AdminDashboard.Api;

public static class AdminAuthConstants
{
    // Its own scheme name, deliberately unrelated to JobSearch.Api's
    // CookieAuthenticationDefaults.AuthenticationScheme — no shared cookie, no shared session
    // between the two apps (see Program.cs's own comment).
    public const string Scheme = "AdminAuth";
}
