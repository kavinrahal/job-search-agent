import { describe, expect, it } from "vitest";
import { initialsFrom } from "./Avatar";

// Pure logic, so no DOM environment needed. The rendering is presentational and is reviewed in the
// gallery rather than asserted here.

describe("initialsFrom", () => {
  it("takes the first and last word of a full name", () => {
    expect(initialsFrom("Kavin Abeysinghe")).toBe("KA");
  });

  it("skips the middle rather than running to three characters", () => {
    expect(initialsFrom("Ada Byron Lovelace")).toBe("AL");
  });

  it("uses a single initial for a single word", () => {
    expect(initialsFrom("Kavin")).toBe("K");
  });

  it("drops the email domain, so colleagues do not all share a second initial", () => {
    expect(initialsFrom("kavin.abeysinghe@example.com")).toBe("KA");
    expect(initialsFrom("kavin@example.com")).toBe("K");
    expect(initialsFrom("ada@example.com")).not.toBe(initialsFrom("kavin@example.com"));
  });

  it("upper cases whatever it finds", () => {
    expect(initialsFrom("kavin abeysinghe")).toBe("KA");
  });

  it("tolerates surrounding and repeated whitespace", () => {
    expect(initialsFrom("  Kavin   Abeysinghe  ")).toBe("KA");
  });

  it("falls back to a placeholder rather than rendering an empty squircle", () => {
    expect(initialsFrom("")).toBe("?");
    expect(initialsFrom("   ")).toBe("?");
  });
});
