# /test-cycle

Run a four-stage multi-agent test-writing cycle on one or more source components.
Produces a test suite that covers behavioral contracts, not implementation paths.
Ponytail (lazy senior dev) is active at stages 2 and 4 to prune what isn't worth testing.

**Usage:** `/test-cycle path/to/Component.cs [path/to/Another.cs ...]`
If no path is given, ask the user which component to target before starting.

---

## Philosophy (apply throughout)

- Tests verify **behaviour**, not implementation. "Given these inputs, the output has these properties" — not "the function calls method X".
- For agentic/LLM components: test **structural contracts** (output always deserialises, fields are always present, values are always within the valid enum) — never assert on specific content.
- Never test what the language or framework guarantees. If removing the test wouldn't allow a production bug to go undetected, the test doesn't belong.
- The excluded list from the Test Designer is as valuable as the included list. Document it.

---

## Pre-flight

Before starting the stages:

1. **Read the target file(s)** completely.
2. **Check for an existing test project.** Look for `*.Tests/` directories and existing fixture helpers (`Db.Fresh()`, `Make.*`, `Seed.*` etc.). If they exist, note them — the Implementer must reuse them.
3. **Identify the test framework** in use (xUnit, NUnit, pytest, Vitest, Jest, etc.). If none exists yet, recommend the standard for this stack and create the project after Stage 2 confirms the plan is worth it.
4. **Note external dependencies** in the target (HTTP clients, DB contexts, cloud SDKs) — these determine what fakes or in-memory substitutes are needed.

---

## Stage 1 — Analyst

Spawn (or act as) an Analyst agent. Input: the target source file(s). Output: a structured JSON analysis.

**Mandate:** Be thorough. More findings here is better — the next stage prunes.
Do not suggest tests. Just analyse the component.

Produce a JSON document with these top-level keys:

```
{
  "component": "name",
  "behaviors": [ { "name": "...", "description": "..." } ],
  "contracts": [ { "id": "C01", "statement": "...", "where": "method name" } ],
  "edge_cases": [ { "id": "E01", "description": "...", "where": "...", "silent_failure": true/false } ],
  "failure_modes": [ { "id": "F01", "description": "...", "consequence": "..." } ],
  "external_dependencies": [ { "name": "...", "role": "...", "testability": "..." } ],
  "testability_notes": [ "..." ]
}
```

`silent_failure: true` means the bug would not surface as an exception — it would corrupt state or produce wrong output silently. These are the highest-priority test targets.

---

## Stage 2 — Test Designer (Ponytail)

Ponytail rules are active. Input: Analyst JSON + source file(s). Output: a test plan.

**Ponytail filter — for every potential test ask:** "What real production failure does this catch that no other test in the suite catches?" If you can't answer clearly, exclude it and say why.

**Never include tests that:**
- Exercise language/framework semantics (EF Core `Add`, LINQ `Where`, C# bool evaluation, Python dict lookup)
- Duplicate the failure scenario of another included test with no additional discriminating power
- Assert on the exact wording of human-readable strings (they break on every copy edit)
- Pin undefined or implementation-accidental behaviour
- Cover failure modes that are code defects to fix, not behaviours to test around

Produce a JSON test plan:

```
{
  "included": [
    {
      "id": "TC01",
      "name": "GivenX_WhenY_ThenZ",
      "type": "unit | integration | contract",
      "contracts_covered": ["C01"],
      "setup": "minimal description of inputs/state",
      "assert": "one sentence: exactly what property is verified",
      "why": "one sentence: what production failure this catches"
    }
  ],
  "excluded": [
    { "ref": "E01 or description", "reason": "why not worth testing" }
  ],
  "fixtures_needed": [ "description of shared helpers to create or reuse" ]
}
```

**Test type guidance:**
- `unit`: no I/O, no DB, pure function — prefer this wherever possible
- `integration`: needs a real (in-memory) DB context or filesystem
- `contract`: makes a real external call (API, LLM); mark with `[Trait("Category","contract")]` and exclude from default CI runs

Target 10–20 tests. Fewer well-chosen tests beat more tests with weaker rationale.

---

## Stage 3 — Implementer

Input: test plan + source file(s) + existing fixture helpers (if any). Output: working test code.

**Rules:**
- Implement every included test. Do not add tests not in the plan.
- Reuse existing fixture helpers. Only create new ones listed in `fixtures_needed`.
- Re-query the DB after `SaveChanges` — don't assert on stale tracked navigation properties.
- If the code under test behaves differently from what the plan specifies, **correct the assertion and document the divergence** — don't silently adjust the test to match wrong behaviour.
- Flag any divergences clearly: "Plan said X, actual behaviour is Y, assertion corrected to Y."

**Fixture conventions (create if not already present):**
- `Db.Fresh()` — isolated in-memory DB context per call (new Guid name each time)
- `Make.<Type>(field: override)` — factory with sensible defaults so each test only sets the one field under test
- `Seed.<Entity>(db, ...)` — inserts and saves, returns entity
- For HTTP fakes: a minimal `HttpMessageHandler` subclass that returns fixture JSON — no library needed

---

## Stage 4 — Critic (Ponytail)

Ponytail rules are active. Input: implemented tests + source file(s) + test plan. Output: per-test rulings.

For each test, rule exactly one of: **keep**, **delete**, **modify** — with a one-sentence reason.

**Delete if:**
- The test passes even when the behaviour it claims to guard is broken
- Another test in the suite would catch the same production failure
- The assertion is on framework/language behaviour, not application logic

**Modify if:**
- The assertion is weaker than needed (passes even when the guarded behaviour is wrong)
- The assertion is so specific it would break on irrelevant refactors (e.g., exact human-readable string content)
- The setup is more complex than the scenario requires

**Keep if:**
- Removing the guarded logic would make this test fail and no other test would
- The assertion is on an observable output property, not an internal call sequence

After all rulings, give a 3–5 sentence overall assessment: what the suite catches well, what gap remains, and the one additional test most justified by production risk if the user wanted to add it.

---

## Post-flight

After Stage 4:

1. Apply all `modify` rulings to the implemented test file.
2. Run the test suite. All tests must pass before committing.
3. If any tests fail due to a real bug in the source (not a bad assertion), surface it — don't mask it by weakening the assertion.
4. Commit: test file(s), fixture file(s), any source refactors made to enable testing (e.g., extracting a private method to `internal`).
5. Report: how many tests, pass rate, which Critic rulings were applied, and the one next component worth running through this cycle.

---

## Notes on agentic/LLM components

If the component calls an LLM (Claude, OpenAI, etc.):
- Tag those tests as `contract` type — they require a real API key and cost money to run
- Assert only: response deserialises without error, all required fields are present, enum fields contain only valid values, numeric fields are within expected range
- Do not assert on response content, tone, or phrasing
- Run contract tests with the cheapest model available for the integration (Haiku, GPT-3.5, etc.)

## Notes on HTTP integrations

Prefer a custom `HttpMessageHandler` subclass over mocking libraries:

```csharp
class StubHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage _, CancellationToken __)
        => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(responseJson) });
}
// Usage: new HttpClient(new StubHandler(File.ReadAllText("Fixtures/response.json")))
```

Store fixture JSON files in `Tests/Fixtures/` committed to the repo.

## Target: $ARGUMENTS
