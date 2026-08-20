# Agent docs

Vendored copies of instruction files that normally live in Kavin's local Claude Code install:

| File | Copied from |
|------|-------------|
| `ponytail-review.md` | `~/.claude/plugins/cache/ponytail/ponytail/<version>/skills/ponytail-review/SKILL.md` |
| `test-cycle.md` | `~/.claude/commands/test-cycle.md` |

They exist here because the crash-fix agent runs on a GitHub Actions runner, which has none
of that local setup. Without these committed, an automated fix would skip the review and
testing discipline every hand-written change in this repo goes through.

These are **copies, not the source of truth**. If the local versions change meaningfully,
re-copy them — nothing detects drift automatically.

Ruflo is deliberately *not* vendored: it's local-only tooling (see `.gitignore`), and a
`npx ruflo init` per run would add minutes and cost to every crash fix for capabilities a
single-file bug fix doesn't need.
