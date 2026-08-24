# Production safety

## Always ask first

Always ask the repo owner before touching production data, environment variables, secrets, or
deploys — even changes that look low-risk. This applies whether the action is a schema migration,
a one-off data fix, an env var tweak, or a manual deploy trigger. Confirmation once for one action
doesn't imply standing authorization for similar actions later — ask again each time.

## Running one-off scripts against production

Use Railway's env injection rather than writing secrets to a file:

```bash
railway run --service job-search-agent -- dotnet run --project <scratchpad-path>
```

Env vars (including `DATABASE_URL` and API keys) land directly in the subprocess; nothing gets
written to disk. Reuse a single scratchpad `.csproj` with a `ProjectReference` to `JobSearch.Data`
across multiple scripts in a session (backfill, retry, verification, diagnostics) rather than
creating a new project per script — just overwrite `Program.cs` each time.

## Diagnostics must be narrowly scoped

Any script that reads real user data for debugging must print only the specific field under
investigation, not a full record dump. Example: tracking down why `GraduationYear` rendered as a
literal "| 0" in a real generated CV required reading a user's `Background` YAML — the diagnostic
printed only the `education:` block, not the whole document, even though the whole document was
already available in the same query.

## Rollout strategy for schema/behavior changes affecting live data

When the affected user count is small, prefer a staged sequential rollout over a dual-path runtime
fallback:

1. **Deploy A** — ship the new schema/renderer/logic without switching any live call site to it.
2. Backfill/migrate existing data, verify parity against real output.
3. **Deploy B** — cut the live call sites over, now that the new path is proven against real data.

This avoids maintaining two live code paths (old + new with a feature flag) when a simple
two-step sequence is safer and less code, at real user volumes where "migrate everyone, then
verify, then cut over" is actually feasible in one sitting.
