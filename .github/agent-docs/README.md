# Agent docs

Two kinds of file live here.

## Vendored copies

Instruction files that normally live in Kavin's local Claude Code install:

| File | Copied from |
|------|-------------|
| `ponytail-review.md` | `~/.claude/plugins/cache/ponytail/ponytail/<version>/skills/ponytail-review/SKILL.md` |
| `test-cycle.md` | `~/.claude/commands/test-cycle.md` |

They exist here because the crash-fix agent runs on a GitHub Actions runner, which has none
of that local setup. Without these committed, an automated fix would skip the review and
testing discipline every hand-written change in this repo goes through.

These are **copies, not the source of truth**. If the local versions change meaningfully,
re-copy them — nothing detects drift automatically.

## Repo-native conventions

Written for this repo specifically — this *is* the source of truth, not a copy of anything else:

| File | Covers |
|------|--------|
| `git-workflow.md` | Branching, commit trailer, opening PRs via `manual-pr.yml` (not `gh pr create`) |
| `production-safety.md` | Confirming before touching prod, running one-off scripts safely, PII-safe diagnostics, staged rollouts |
| `architecture-conventions.md` | JSON-in-string-column convention, PK-reuse for 1:1 entities, forced-tool-use Claude output, trusting real data over assumption |
| `parallel-agents.md` | File-scope isolation and worktree usage when running more than one agent at once |

Any Claude Code session working in this repo should read the relevant file before the matching
kind of action — see the root `CLAUDE.md`'s pointer to this directory. GitHub Actions bots
(`crash-fix.yml`, `pr-feedback.yml`) don't currently reference these — they already achieve PR
authorship a different way (their own App token), so `git-workflow.md`'s PR-workflow rule doesn't
apply to them as written.

Ruflo is deliberately *not* vendored: it's local-only tooling (see `.gitignore`), and a
`npx ruflo init` per run would add minutes and cost to every crash fix for capabilities a
single-file bug fix doesn't need.
