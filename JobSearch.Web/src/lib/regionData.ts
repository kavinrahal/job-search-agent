// Currency codes come from the browser's own Intl data (ECMA-402) rather than a bundled
// dataset — always correct, never goes stale. There's no equivalent supportedValuesOf for
// countries (Intl.supportedValuesOf only covers currency/calendar/collation/numberingSystem/
// timeZone/unit), so the ISO 3166-1 alpha-2 code list below is a static list of stable,
// rarely-changing codes — only Intl.DisplayNames (for the actual display names) is native.

// prettier-ignore
const COUNTRY_CODES = [
  "AF","AL","DZ","AD","AO","AG","AR","AM","AU","AT","AZ","BS","BH","BD","BB","BY","BE","BZ",
  "BJ","BT","BO","BA","BW","BR","BN","BG","BF","BI","CV","KH","CM","CA","CF","TD","CL","CN",
  "CO","KM","CG","CD","CR","CI","HR","CU","CY","CZ","DK","DJ","DM","DO","EC","EG","SV","GQ",
  "ER","EE","SZ","ET","FJ","FI","FR","GA","GM","GE","DE","GH","GR","GD","GT","GN","GW","GY",
  "HT","HN","HU","IS","IN","ID","IR","IQ","IE","IL","IT","JM","JP","JO","KZ","KE","KI","KP",
  "KR","KW","KG","LA","LV","LB","LS","LR","LY","LI","LT","LU","MG","MW","MY","MV","ML","MT",
  "MH","MR","MU","MX","FM","MD","MC","MN","ME","MA","MZ","MM","NA","NR","NP","NL","NZ","NI",
  "NE","NG","MK","NO","OM","PK","PW","PA","PG","PY","PE","PH","PL","PT","QA","RO","RU","RW",
  "KN","LC","VC","WS","SM","ST","SA","SN","RS","SC","SL","SG","SK","SI","SB","SO","ZA","SS",
  "ES","LK","SD","SR","SE","CH","SY","TW","TJ","TZ","TH","TL","TG","TO","TT","TN","TR","TM",
  "TV","UG","UA","AE","GB","US","UY","UZ","VU","VA","VE","VN","YE","ZM","ZW",
];

function regionNames() {
  try {
    const display = new Intl.DisplayNames(["en"], { type: "region" });
    return COUNTRY_CODES
      .map(code => display.of(code))
      .filter((name): name is string => !!name)
      .sort((a, b) => a.localeCompare(b));
  } catch {
    return null;
  }
}

export const COUNTRIES: string[] = regionNames() ?? [
  "Australia", "Canada", "Germany", "India", "Ireland", "New Zealand",
  "Singapore", "United Kingdom", "United States",
];

function currencyList(): { code: string; label: string }[] {
  try {
    const codes = Intl.supportedValuesOf("currency");
    const display = new Intl.DisplayNames(["en"], { type: "currency" });
    return codes
      .map(code => {
        const name = display.of(code);
        return { code, label: name && name !== code ? `${code} — ${name}` : code };
      })
      .sort((a, b) => a.code.localeCompare(b.code));
  } catch {
    return ["AUD", "CAD", "EUR", "GBP", "INR", "NZD", "SGD", "USD"].map(code => ({ code, label: code }));
  }
}

export const CURRENCIES = currencyList();

// Only the handful of countries where a state/province multi-select is worth showing — any
// other country falls back to free-text states, which is exactly what the user asked for.
export const STATES_BY_COUNTRY: Record<string, string[]> = {
  Australia: [
    "Australian Capital Territory", "New South Wales", "Northern Territory", "Queensland",
    "South Australia", "Tasmania", "Victoria", "Western Australia",
  ],
  "United States": [
    "Alabama", "Alaska", "Arizona", "Arkansas", "California", "Colorado", "Connecticut",
    "Delaware", "Florida", "Georgia", "Hawaii", "Idaho", "Illinois", "Indiana", "Iowa",
    "Kansas", "Kentucky", "Louisiana", "Maine", "Maryland", "Massachusetts", "Michigan",
    "Minnesota", "Mississippi", "Missouri", "Montana", "Nebraska", "Nevada", "New Hampshire",
    "New Jersey", "New Mexico", "New York", "North Carolina", "North Dakota", "Ohio",
    "Oklahoma", "Oregon", "Pennsylvania", "Rhode Island", "South Carolina", "South Dakota",
    "Tennessee", "Texas", "Utah", "Vermont", "Virginia", "Washington", "West Virginia",
    "Wisconsin", "Wyoming",
  ],
  Canada: [
    "Alberta", "British Columbia", "Manitoba", "New Brunswick", "Newfoundland and Labrador",
    "Northwest Territories", "Nova Scotia", "Nunavut", "Ontario", "Prince Edward Island",
    "Quebec", "Saskatchewan", "Yukon",
  ],
  "United Kingdom": ["England", "Northern Ireland", "Scotland", "Wales"],
};
