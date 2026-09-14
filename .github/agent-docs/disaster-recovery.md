# Disaster recovery (Railway Postgres)

Status as of 2026-09-14: backups are enabled and a real restore has been tested end to end on
both `staging` and `production` — not just configured and assumed to work.

## What's enabled

Both mechanisms are on for both environments (project `job-search-agent-DB`, service `Postgres`):

- **Point-in-time recovery (PITR)** — continuous WAL archiving via pgBackRest to a private Railway
  bucket. Restore window is roughly 4 weeks (last 4 full backups retained), but only covers
  from-enablement forward — anything before the environment's PITR was turned on is not recoverable
  this way.
- **Volume Backups** — scheduled snapshots of the whole volume, weekly schedule with 1-month
  retention, configured via the dashboard (Postgres service → **Backups** tab). Manual on-demand
  backups are also available from that same tab if needed before a risky change.

Neither existed before this pass — previously there was no backup script, no DR runbook, and no
restore had ever been tested; whatever protection existed was an unverified Railway platform
default.

## Restoring (PITR — the mechanism actually tested)

```bash
railway postgres pitr restore --at <TIME> --new-service-name <NAME> \
  -p job-search-agent-DB -e <staging|production> -s Postgres -y
```

- `--at` accepts RFC3339 (`2026-07-20T12:00:00Z`), `"YYYY-MM-DD HH:MM:SS"` local time, or a
  relative offset (`30m`, `2h`, `1d`).
- This **creates a brand-new, separate service** (`<source>-restored-YYYYMMDD-HHMM`, or your
  `--new-service-name`). The source service is never touched and keeps serving traffic throughout
  restore, so this is safe to run directly against production if needed — verify the restored copy
  before cutting anything over to it, never assume.
- Runs as a background provisioning workflow; the CLI returns immediately. Poll
  `railway status -p job-search-agent-DB -e <env>` until the new service appears, then check its
  deploy status is `Online` before querying it.

**Gotcha confirmed during testing**: Railway service names must be unique *per project*, not just
per environment. If a service with your chosen name already exists in the *other* environment
under the same project, Railway silently appends a numeric suffix (e.g.
`postgres-dr-verify-test-8957`) instead of erroring — always re-check the actual name via
`railway status --json` before trying to connect to what you think you just created, rather than
assuming the name you asked for is the name you got.

**Gotcha confirmed during testing**: `DATABASE_URL` on a Railway service is the *internal*
(`*.railway.internal`) hostname, unreachable from outside Railway's own network. To query a
restored service from a local machine or script, use `DATABASE_PUBLIC_URL` instead (the public TCP
proxy variant) — `railway run -s <service> -- <command>` injects both, but only
`DATABASE_PUBLIC_URL` resolves externally.

**Gotcha confirmed during testing**: any verification script must fail loudly if neither
`DATABASE_URL` nor `DATABASE_PUBLIC_URL` is actually present in the injected environment, rather
than silently falling back to a local-dev default connection string — a variable-resolution
hiccup (e.g. wrong/suffixed service name) can otherwise look like a successful query against a
tiny, wrong, local database instead of an honest connection failure.

### Verifying a restored copy

A restore that hasn't been checked isn't verified, just assumed. After the new service is
`Online`, connect to it (via `railway run -s <restored-service> -- <command>`, using
`DATABASE_PUBLIC_URL` per the gotcha above) and compare row counts on a handful of representative
tables (e.g. `Users`, `UserProfiles`, `Applications`, `DiscoveredPostings`) against the same query
run against the source. A minimal `AppDbContext`-based script works fine for this — set
`db.CrossTenantAccess = true` and `.IgnoreQueryFilters()` per query to get real totals rather than
tenant-scoped counts. Per this repo's diagnostics-scoping convention, print counts only, never
actual row content.

### Cleanup after a restore test

Deleting the restored service (`railway service delete --service <name> --environment <env>
--project job-search-agent-DB --yes`) does **not** delete its underlying volume — it's left
behind, detached, still incurring storage cost. Check the environment's Volumes list afterward
(`railway volume list --json` while linked to that project/environment) for an orphaned volume
with `serviceName: null` and delete it too (via the dashboard's Volumes tab is the safer path for
this one — a volume delete is irreversible and worth eyeballing directly rather than trusting a
raw ID from a script).

## Logical dumps (pg_dump) — not set up, worth doing eventually

Both mechanisms above are Railway-platform features. Neither survives a Railway-level platform
incident, only accidental deletion / bad migration / bad data scenarios. A scheduled `pg_dump` to
storage outside Railway entirely (e.g. an offsite bucket) is the one gap left, and the only thing
that would survive Railway itself having a bad day. Not built in this pass — worth a follow-up if
that specific risk matters enough to justify the extra moving part.

```bash
pg_dump "postgresql://postgres:<password>@localhost:<port>/railway" --format=custom --no-owner --file=backup.dump
pg_restore --dbname="postgresql://..." --no-owner --exit-on-error backup.dump
```

## Measured RTO (real, from the 2026-09-14 test)

- **Staging**: ~5 minutes from triggering the restore to a confirmed-queryable copy with row
  counts matching the source exactly across `Users`, `UserProfiles`, `Applications`, and
  `DiscoveredPostings`. The new service itself showed `Online` within ~30 seconds; most of the
  remaining time was working out the internal-vs-public hostname issue above, not actual restore
  latency.
- **Production**: ~3-5 minutes, same verification method — row counts on the same four tables
  matched exactly between source and restored copy, confirmed on independently distinct hosts
  (proving these were genuinely two separate database instances, not the same one read twice).
  Real counts intentionally omitted here — this is a public repo, and DB-size figures aren't
  something to leave in permanent git history.
- **RPO**: effectively near-zero while PITR is active — continuous WAL archiving, not
  daily-snapshot-only, so data loss on a real incident is bounded by WAL archiving lag rather than
  by the backup schedule interval.

## When to reach for what

- Accidental bad migration / bad data change, need to recover a specific point in time → **PITR
  restore** (above). This is the tested, proven path.
- Need a full volume-level snapshot restore (Railway's other mechanism, not the one tested here) →
  Volume Backups tab, restores *in place* (replaces the current volume, unlike PITR's separate-
  service approach) — treat as more destructive, confirm before use.
- Total Railway platform incident → nothing above helps; this is exactly the gap `pg_dump`
  off-platform backups would cover, not yet built.
