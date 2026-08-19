import { allCountries } from "country-region-data";

// Currency codes come from the browser's own Intl data (ECMA-402) — always correct, never
// goes stale, no bundled dataset needed.
function currencyList(): { code: string; label: string }[] {
  try {
    const codes = Intl.supportedValuesOf("currency");
    const display = new Intl.DisplayNames(["en"], { type: "currency" });
    return codes
      .map(code => {
        const name = display.of(code);
        return { code, label: name && name !== code ? `${code} (${name})` : code };
      })
      .sort((a, b) => a.code.localeCompare(b.code));
  } catch {
    return ["AUD", "CAD", "EUR", "GBP", "INR", "NZD", "SGD", "USD"].map(code => ({ code, label: code }));
  }
}

export const CURRENCIES = currencyList();

// Countries and their subdivisions come from country-region-data (ISO 3166-2, MIT licensed) —
// covers all 249 countries/territories, so there's no hand-maintained list to keep in sync.
export const COUNTRIES: string[] = allCountries.map(([name]) => name).sort((a, b) => a.localeCompare(b));

// A handful of small territories (Aruba, Gibraltar, ...) only have one "region" entry that's
// just the country's own name repeated back — not a real subdivision. Those, along with any
// country with zero regions, are left out entirely so the UI falls back to free-text states
// exactly like a country the dataset doesn't cover at all.
export const STATES_BY_COUNTRY: Record<string, string[]> = Object.fromEntries(
  allCountries
    .filter(([name, , regions]) => regions.length > 1 || (regions.length === 1 && regions[0][0] !== name))
    .map(([name, , regions]) => [name, regions.map(([regionName]) => regionName)]),
);
