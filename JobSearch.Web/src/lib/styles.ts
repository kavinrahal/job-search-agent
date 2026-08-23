// Shared class-string constants for the app's primary visual patterns — extracted after the
// same gradient button/card strings were being duplicated near-verbatim across most page
// files. Keep using template literals to extend these (`${PRIMARY_BUTTON} mt-4`) rather than
// copy-pasting the whole string again.

export const CARD = "rounded-xl border border-gray-200 bg-white p-5 shadow-sm dark:border-gray-800 dark:bg-gray-900";

export const PRIMARY_BUTTON =
  "rounded-lg bg-gradient-to-r from-violet-600 to-fuchsia-500 px-4 py-2 text-sm font-medium text-white shadow-sm shadow-violet-600/20 transition-all duration-150 hover:from-violet-500 hover:to-fuchsia-400 hover:shadow-md hover:shadow-violet-600/30 disabled:cursor-not-allowed disabled:from-gray-300 disabled:to-gray-300 disabled:shadow-none dark:disabled:from-gray-700 dark:disabled:to-gray-700";

export const PRIMARY_BUTTON_SM =
  "rounded-lg bg-gradient-to-r from-violet-600 to-fuchsia-500 px-3 py-1.5 text-sm font-medium text-white shadow-sm shadow-violet-600/20 transition-all duration-150 hover:from-violet-500 hover:to-fuchsia-400 hover:shadow-md hover:shadow-violet-600/30";

export const SECONDARY_BUTTON =
  "rounded-lg bg-gray-100 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors duration-150 hover:bg-gray-200 disabled:cursor-not-allowed disabled:opacity-50 dark:bg-gray-800 dark:text-gray-200 dark:hover:bg-gray-700";

// Distinct from CardEditor's own INPUT (which the profile/criteria card editors share) —
// this one carries the dark-mode placeholder colour those don't need.
export const INPUT =
  "w-full rounded-lg border border-gray-200 bg-white p-2 text-sm text-gray-700 focus:outline-none focus:ring-2 focus:ring-violet-400 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-100 dark:placeholder:text-gray-500 dark:focus:ring-violet-500";
