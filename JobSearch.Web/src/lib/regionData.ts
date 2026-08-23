import { allCountries } from "country-region-data";
import currencyData from "currency-codes/data";
import countryToCurrency from "country-to-currency";

// currency-codes ships the full, current ISO 4217 list (~180 currencies) as a static dataset —
// unlike Intl.supportedValuesOf("currency"), which is a fairly recent addition to the spec and
// silently returns a much shorter list (or throws) on older/less-complete Intl implementations,
// this is the same everywhere regardless of the user's browser.
export const CURRENCIES: { code: string; label: string }[] = currencyData
  .map(c => ({ code: c.code, label: `${c.code} (${c.currency})` }))
  .sort((a, b) => a.code.localeCompare(b.code));

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

// country-to-currency maps by ISO 3166-1 alpha-2 code, not by name — country-region-data's own
// tuples already carry that code as the second element, so this reuses the same country list
// rather than introducing a second, potentially-drifting one. A handful of alpha-2 codes (e.g.
// disputed/unrecognized territories) have no entry in country-to-currency; those are simply
// omitted here, same fallback behavior as an unmapped country in STATES_BY_COUNTRY above.
export const COUNTRY_TO_CURRENCY: Record<string, string> = Object.fromEntries(
  allCountries
    .filter(([, code]) => code in countryToCurrency)
    .map(([name, code]) => [name, countryToCurrency[code as keyof typeof countryToCurrency]]),
);
