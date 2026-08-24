# Git & PR workflow

## Branching and the staging gate

- `master` is the production branch (`staging.worksanta.com`'s production counterpart at
  `worksanta.com`, auto-deploys on push). `staging` is the staging branch (deploys to
  `staging.worksanta.com` / `api-staging.worksanta.com`). Regular feature/fix work branches off
  **updated `staging`**, not `master`, and its PR targets `staging` — `manual-pr.yml` already
  defaults to this. Never push directly to either branch.
- Exception: `crash-fix.yml`'s automated pipeline branches off and targets `master` directly —
  an urgent, already-CI-verified crash fix shouldn't wait on a staging gate. This is the only
  path that skips staging.
- If a branch you're working from has already been merged into `staging`, don't keep building on
  it — branch fresh off `staging` again for the next piece of work.

## Promoting staging to production

Once a change has been verified working on `staging.worksanta.com` / `api-staging.worksanta.com`
(manual click-through, or an agent's staging-verification report), promote it to production:

```bash
git fetch origin
git checkout master && git merge --ff-only origin/staging && git push
```

This is a manual step by design, not automated — the point is a deliberate release, not another
auto-deploy. **Always proactively remind the repo owner to do this once staging testing passes**
— it's an easy step to forget once attention moves to the next piece of work, and the owner has
explicitly asked to be reminded rather than needing to bring it up themselves.

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

This runs `.github/workflows/manual-pr.yml`, which opens the PR under the `worksanta-jnr-engineer-
bot` GitHub App — an app-authored PR the owner can actually approve. This is a separate App from
the one `crash-fix.yml`/`pr-feedback.yml` use (`worksanta-crash-bot`): PRs #32-#39 were all opened
under the crash-fix identity before this App existed, which was misleading since none of them were
crash fixes — don't reuse the crash-fix App for regular feature/fix work going forward.

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
