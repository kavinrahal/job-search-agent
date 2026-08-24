# Git & PR workflow

## Branching

- Always branch off updated `master` for new work. Never push directly to `master`.
- If a branch you're working from has already been merged into `master`, don't keep building on
  it — branch fresh off `master` again for the next piece of work.

## Commits

Every commit made on behalf of the repo owner ends with:

```
Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: <session URL>
```

## Opening PRs — use the app workflow, not `gh pr create`

**Never run `gh pr create` directly.** It authors the PR under whichever personal account is
authenticated locally, and GitHub hard-blocks a PR's own author from approving it — so a
directly-created PR can't go through the repo owner's normal review/approve flow.

This actually happened: PR #32 was opened with `gh pr create` under the operator's own account,
had to be closed, and was redone correctly as PR #33. Don't repeat it.

Instead, once your branch is pushed:

```bash
gh workflow run manual-pr.yml \
  -f branch="your-branch-name" \
  -f title="PR title" \
  -f body="PR body markdown"
```

This runs `.github/workflows/manual-pr.yml`, which opens the PR under the same GitHub App used by
the crash-fix pipeline — an app-authored PR the owner can actually approve.

Then poll for completion and find the resulting PR:

```bash
gh run view <run-id> --json status,conclusion   # wait for status: completed
gh pr list --head your-branch-name --state open --json number,url,author
```

Finally, assign it to the repo owner:

```bash
gh pr edit <pr-number> --add-assignee kavinrahal
```

## Before every commit

1. `dotnet build && dotnet test` — the full suite, not just tests near the files you touched.
2. Run a ponytail-review pass on the diff before shipping (see `ponytail-review.md` in this
   directory).
