import { useState } from "react";

// Each entry handles both a known first name (post-onboarding) and null (still mid-onboarding,
// no CV uploaded yet) rather than having two separate pools to keep in sync.
const GREETINGS: ((name: string | null) => string)[] = [
  name => name ? `Hi ${name}, ready to go job hunting again?` : "Ready to go job hunting again?",
  name => name ? `Welcome back, ${name}. Let's find you something good today.` : "Welcome back. Let's find you something good today.",
  name => name ? `${name}! Good to see you. Let's get you hired.` : "Good to see you. Let's get you hired.",
  name => name ? `Hey ${name}, the hunt continues.` : "Hey, the hunt continues.",
  name => name ? `Back for more, ${name}? Let's see what's out there.` : "Back for more? Let's see what's out there.",
  name => name ? `${name}, your next role is out there. Let's go find it.` : "Your next role is out there. Let's go find it.",
  name => name ? `Welcome back, ${name}. No rest for the job hunter.` : "Welcome back. No rest for the job hunter.",
  name => name ? `Hi ${name}. Let's turn "maybe" into "hired."` : `Let's turn "maybe" into "hired."`,
];

// Picked once per mount, not per render, so it stays put for the length of the visit rather
// than shuffling every time something else on the page re-renders.
export function DashboardGreeting({ name }: { name: string | null }) {
  const [greeting] = useState(() => GREETINGS[Math.floor(Math.random() * GREETINGS.length)](name));
  return <p className="text-sm text-gray-400">{greeting}</p>;
}
