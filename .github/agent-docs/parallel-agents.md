# Running parallel agents on this repo

## One writer per file scope

Before spawning more than one agent to write code concurrently, check that their file scopes
genuinely don't overlap — not just "probably fine," actually trace which files each piece of work
touches. State each agent's scope explicitly in its spawn prompt so it doesn't wander into the
other's files.

## Isolation

Give every writing agent an isolated git worktree, not a shared working directory. In Claude
Code's `Agent` tool, pass `isolation: "worktree"` — this creates a separate worktree and branch
automatically, and it's cleaned up on its own if the agent makes no changes.

Read-only research or exploration can run concurrently with no isolation needed — only writes
require it.

## What never gets split across parallel agents

Shared manifests and migrations: `.csproj`/`package.json`/`package-lock.json`, and any EF Core
migration touching a table another parallel agent's work also touches. These need one owner who
integrates/reconciles, not two agents editing them independently — conflicting migrations or
manifest edits are exactly the kind of thing isolation doesn't protect against.

## Before spawning

Point each agent's prompt at the other docs in `.github/agent-docs/` relevant to its task
(`git-workflow.md` for anything that commits/opens a PR, `production-safety.md` for anything
touching prod data, `architecture-conventions.md` for anything adding a new entity/field/Claude
call) — a freshly spawned agent has none of the running session's context and won't know these
conventions exist unless told where to look.
