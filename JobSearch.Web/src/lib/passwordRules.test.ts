import { describe, it, expect } from "vitest";
// The real server-side rule set, pulled in as text via Vite's ?raw so this test compares
// against the actual C# rather than a hand-copied list that could quietly go stale.
import passwordRulesSource from "../../../JobSearch.Data/PasswordRules.cs?raw";
import { MIN_PASSWORD_LENGTH, PASSWORD_RULES, isPasswordValid, passwordRuleResults } from "./passwordRules";

/** The server messages for the rules this password fails, in the server's own order. */
const failureMessages = (password: string) =>
  PASSWORD_RULES.filter(rule => !rule.test(password)).map(rule => rule.message);

// Satisfies every rule; each case below breaks exactly one thing about it so a rule can only
// be exercised in isolation.
const VALID = "Abcdefg1!";

describe("individual rules", () => {
  it("accepts a password that satisfies every rule", () => {
    expect(isPasswordValid(VALID)).toBe(true);
    expect(failureMessages(VALID)).toEqual([]);
    expect(passwordRuleResults(VALID).every(r => r.met)).toBe(true);
  });

  // [description, password, the single rule label it should fail]
  const CASES: [string, string, string][] = [
    ["too short", "Abc1!", "8 characters"],
    ["no lowercase", "ABCDEFG1!", "Lowercase"],
    ["no uppercase", "abcdefg1!", "Uppercase"],
    ["no number", "Abcdefgh!", "A number"],
    ["no symbol", "Abcdefg12", "A symbol"],
  ];

  for (const [description, password, failingLabel] of CASES) {
    it(`fails only the "${failingLabel}" rule when ${description}`, () => {
      expect(isPasswordValid(password)).toBe(false);
      const unmet = passwordRuleResults(password).filter(r => !r.met).map(r => r.label);
      expect(unmet).toEqual([failingLabel]);
    });
  }

  it("reports every failing rule at once, in the server's order", () => {
    expect(failureMessages("abc")).toEqual([
      "Must be at least 8 characters.",
      "Must include an uppercase letter.",
      "Must include a number.",
      "Must include a special character.",
    ]);
  });

  it("counts a space and other punctuation as a special character, like the server's !IsLetterOrDigit", () => {
    expect(isPasswordValid("Abcdefg1 ")).toBe(true);
    expect(isPasswordValid("Abcdefg1_")).toBe(true);
  });

  it("accepts non-ASCII letters and digits by Unicode category, like the server's char.IsLower/IsUpper/IsDigit", () => {
    // Greek lower/upper (Ll/Lu) and an Arabic-Indic digit (Nd) — the server's char.IsLower,
    // char.IsUpper and char.IsDigit all accept these, so an ASCII-only client mirror would
    // wrongly block a password the server takes.
    expect(isPasswordValid("αΒγδεζη١!")).toBe(true);
  });
});

// The whole point of this module is that it cannot silently disagree with the server, which is
// the only thing that actually enforces password strength. Comparing against the real
// PasswordRules.cs means editing either side alone fails here, rather than the two drifting
// apart unnoticed until a user hits a 400 the checklist said couldn't happen.
describe("agreement with JobSearch.Data/PasswordRules.cs", () => {
  const serverMinLength = Number(/public const int MinLength = (\d+);/.exec(passwordRulesSource)?.[1]);
  const serverMessages = [...passwordRulesSource.matchAll(/errors\.Add\(\$?"([^"]+)"\)/g)]
    .map(m => m[1].replace("{MinLength}", String(serverMinLength)));

  it("uses the server's minimum length", () => {
    expect(serverMinLength).toBe(MIN_PASSWORD_LENGTH);
  });

  it("mirrors every server rule message, in the server's order", () => {
    expect(serverMessages.length).toBe(PASSWORD_RULES.length);
    expect(PASSWORD_RULES.map(r => r.message)).toEqual(serverMessages);
  });
});
