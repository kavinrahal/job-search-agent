# Architecture conventions

## Semi-structured entity fields: JSON-in-string-column

When an entity needs a field that's structured but doesn't warrant its own table (a list of
overrides, a history array, a config blob), store it as a plain `string` column, manually
(de)serialized via `System.Text.Json` at the point of use — not a native Postgres `jsonb` EF
mapping. Matches `AgentThread.HistoryJson`/`EvaluationJson`/`AccuracyWarningsJson` and
`UserResume`'s `SectionConfigJson`/`ExperienceOverridesJson`/`SkillsSectionJson`/
`ProjectOverridesJson`. Keep this consistent — don't introduce a native `jsonb` column alongside
these without a specific reason.

## 1:1 entities: PK-reuse pattern

For a table that's always exactly one row per `User` (`UserProfile`, `UserResume`), reuse the
`User`'s own primary key rather than a separate auto-increment id + foreign key + unique
constraint. No EF query filter — every lookup is by exact known `UserId`, so a filter adds nothing.
Row-absence is a meaningful state (not yet provisioned for this user), not an error condition to
guard against — check for it explicitly at call sites that require the row to exist.

## Structured output from Claude: forced tool-use, not free text

Any Claude call whose output downstream code will deserialize should force a tool call against an
explicit JSON schema (`ToolChoice = new ToolChoiceAny()` with one tool), not ask for free text and
parse it. See `PostingEvaluator.cs` for the established pattern. This eliminates a whole class of
parsing fragility that free-text output has.

When a single call risks truncation on dense/large inputs (a long resume, many job criteria),
split it into several smaller parallel tool-use calls with separate token budgets rather than one
large call — this codebase has hit real `stop_reason: max_tokens` truncation in production at
smaller budgets than expected more than once. If you inherit a token budget from a similar-but-not-
identical call, don't assume it's safe — a comparable job that requires judgment across more input
(e.g. ranking relevance across every role in a resume vs. transcribing one section) needs a higher
budget than a superficially similar prior call.

## Trust real data over assumption

Before trusting or removing a schema assumption, confirm it against real production or fixture
data — don't assume from the shape of the code. This codebase has repeatedly found real divergence
between what a schema *should* contain per its own comments/types and what real user data actually
contains: `Skills`/`Stack`/`TechStack` were removed as typed fields after real production data hit
parsing exceptions their assumed shape didn't allow for, and `EducationEntry.GraduationYear` had to
become nullable after a real user's data turned out to genuinely lack the field (not a parsing
bug — legitimate data drift from the repo's own seed fixture). When you find a mismatch like this,
grep for actual consumers before deciding whether to keep, loosen, or remove the field.
