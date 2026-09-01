using AdminDashboard.Api.Data;
using AdminDashboard.Api.Services;
using JobSearch.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AdminDashboard.Api.Pages;

// The break-glass screen. Every action here requires typing "CONFIRM" — validated again in
// every OnPost handler below (ConfirmTextValidator), because the client-side JS disabling the
// submit button is not itself a safety control; it's trivially bypassed.
//
// Actions 1-4 (credit adjust, tier change, deactivate, clear worker lock) and the two
// SiteStatus toggles all go through the write-configured AppDbContext, injected separately
// from the read one this page uses for its own display data (current SiteStatus, recent audit
// log) — see AdminDbContextKeys. Every action writes exactly one AdminAuditLog row via
// AdminAuditService, using the same write context instance so the mutation and its audit row
// commit in one SaveChangesAsync.
public class EmergencyModel : PageModel
{
    private const int RecentAuditLogCount = 20;

    private readonly AppDbContext _readDb;
    private readonly AppDbContext _writeDb;

    public EmergencyModel(
        [FromKeyedServices(AdminDbContextKeys.Read)] AppDbContext readDb,
        [FromKeyedServices(AdminDbContextKeys.Write)] AppDbContext writeDb)
    {
        _readDb = readDb;
        _writeDb = writeDb;
    }

    public SiteStatus CurrentStatus { get; private set; } = null!;
    public List<AdminAuditLog> RecentAuditLog { get; private set; } = [];

    [TempData]
    public string? FlashMessage { get; set; }

    [TempData]
    public bool FlashIsError { get; set; }

    public async Task OnGetAsync()
    {
        await LoadDisplayDataAsync();
    }

    public async Task<IActionResult> OnPostAdjustCreditAsync(int targetUserId, int amount, string? confirmText)
    {
        if (!ConfirmTextValidator.IsValid(confirmText))
            return await Invalid();

        var user = await _writeDb.Users.FindAsync(targetUserId);
        if (user is null) return await Invalid($"No user with id {targetUserId}.");

        var before = user.CreditBalance;
        // Floored at zero — a negative balance has no meaning anywhere else in the app
        // (CreditService.HasCreditAsync only ever checks > 0).
        user.CreditBalance = Math.Max(0, user.CreditBalance + amount);
        user.CreditVersion += 1; // same convention as CreditService's own writes to this field

        await AdminAuditService.LogAsync(_writeDb, AdminAuditActions.CreditAdjust, targetUserId,
            $"creditBalance: {before} -> {user.CreditBalance} (delta {amount:+0;-0;0})");

        return Success($"Adjusted credit balance for user {targetUserId}: {before} -> {user.CreditBalance}.");
    }

    public async Task<IActionResult> OnPostChangeTierAsync(int targetUserId, string newTier, string? confirmText)
    {
        if (!ConfirmTextValidator.IsValid(confirmText))
            return await Invalid();

        if (newTier != UserTier.Tier1 && newTier != UserTier.Tier2)
            return await Invalid("Tier must be Tier1 or Tier2.");

        var user = await _writeDb.Users.FindAsync(targetUserId);
        if (user is null) return await Invalid($"No user with id {targetUserId}.");

        var before = user.Tier;
        user.Tier = newTier;

        await AdminAuditService.LogAsync(_writeDb, AdminAuditActions.TierChange, targetUserId,
            $"tier: {before} -> {newTier}");

        return Success($"Changed tier for user {targetUserId}: {before} -> {newTier}.");
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int targetUserId, string? confirmText)
    {
        if (!ConfirmTextValidator.IsValid(confirmText))
            return await Invalid();

        var user = await _writeDb.Users.FindAsync(targetUserId);
        if (user is null) return await Invalid($"No user with id {targetUserId}.");

        var before = user.DeactivatedAt;
        user.DeactivatedAt = DateTime.UtcNow;

        await AdminAuditService.LogAsync(_writeDb, AdminAuditActions.Deactivate, targetUserId,
            $"deactivatedAt: {(before?.ToString("O") ?? "null")} -> {user.DeactivatedAt:O}");

        return Success($"Deactivated user {targetUserId}.");
    }

    public async Task<IActionResult> OnPostReactivateAsync(int targetUserId, string? confirmText)
    {
        if (!ConfirmTextValidator.IsValid(confirmText))
            return await Invalid();

        var user = await _writeDb.Users.FindAsync(targetUserId);
        if (user is null) return await Invalid($"No user with id {targetUserId}.");
        if (user.DeactivatedAt is null) return await Invalid($"User {targetUserId} is not deactivated.");

        var before = user.DeactivatedAt;
        user.DeactivatedAt = null;

        await AdminAuditService.LogAsync(_writeDb, AdminAuditActions.Reactivate, targetUserId,
            $"deactivatedAt: {before:O} -> null");

        return Success($"Reactivated user {targetUserId}.");
    }

    public async Task<IActionResult> OnPostClearWorkerLockAsync(string? confirmText)
    {
        if (!ConfirmTextValidator.IsValid(confirmText))
            return await Invalid();

        var existing = await _writeDb.WorkerLocks.FirstOrDefaultAsync();
        var before = existing?.AcquiredAt;

        if (existing is not null)
        {
            existing.AcquiredAt = null;
        }

        await AdminAuditService.LogAsync(_writeDb, AdminAuditActions.WorkerLockCleared, null,
            $"acquiredAt: {(before?.ToString("O") ?? "null")} -> null");

        return Success("Cleared the worker lock.");
    }

    public async Task<IActionResult> OnPostToggleMaintenanceAsync(bool maintenanceMode, string? maintenanceMessage, string? confirmText)
    {
        if (!ConfirmTextValidator.IsValid(confirmText))
            return await Invalid();

        var status = await _writeDb.SiteStatuses.SingleAsync();
        var before = $"maintenanceMode={status.MaintenanceMode}, message={FormatMessage(status.MaintenanceMessage)}";

        status.MaintenanceMode = maintenanceMode;
        status.MaintenanceMessage = string.IsNullOrWhiteSpace(maintenanceMessage) ? null : maintenanceMessage.Trim();
        status.UpdatedAt = DateTime.UtcNow;
        status.UpdatedBy = "owner";

        var after = $"maintenanceMode={status.MaintenanceMode}, message={FormatMessage(status.MaintenanceMessage)}";
        await AdminAuditService.LogAsync(_writeDb, AdminAuditActions.MaintenanceModeToggled, null, $"{before} -> {after}");

        return Success($"Maintenance mode is now {(maintenanceMode ? "ON" : "off")}.");
    }

    public async Task<IActionResult> OnPostUpdateBannerAsync(bool bannerActive, string? bannerMessage, string? confirmText)
    {
        if (!ConfirmTextValidator.IsValid(confirmText))
            return await Invalid();

        var status = await _writeDb.SiteStatuses.SingleAsync();
        var before = $"bannerActive={status.BannerActive}, message={FormatMessage(status.BannerMessage)}";

        status.BannerActive = bannerActive;
        status.BannerMessage = string.IsNullOrWhiteSpace(bannerMessage) ? null : bannerMessage.Trim();
        status.UpdatedAt = DateTime.UtcNow;
        status.UpdatedBy = "owner";

        var after = $"bannerActive={status.BannerActive}, message={FormatMessage(status.BannerMessage)}";
        await AdminAuditService.LogAsync(_writeDb, AdminAuditActions.BannerUpdated, null, $"{before} -> {after}");

        return Success($"Announcement banner is now {(bannerActive ? "ON" : "off")}.");
    }

    private static string FormatMessage(string? message) => message is null ? "null" : $"\"{message}\"";

    private async Task<IActionResult> Invalid(string? message = null)
    {
        FlashMessage = message ?? $"Type \"{ConfirmTextValidator.RequiredText}\" exactly to confirm this action.";
        FlashIsError = true;
        await LoadDisplayDataAsync();
        return Page();
    }

    private IActionResult Success(string message)
    {
        FlashMessage = message;
        FlashIsError = false;
        // Redirect rather than returning Page() directly — a refresh after a successful,
        // safety-critical action must not silently resubmit it.
        return RedirectToPage();
    }

    private async Task LoadDisplayDataAsync()
    {
        CurrentStatus = await _readDb.SiteStatuses.SingleAsync();
        RecentAuditLog = await _readDb.AdminAuditLogs
            .OrderByDescending(a => a.PerformedAt)
            .Take(RecentAuditLogCount)
            .ToListAsync();
    }
}
