import { cx, styleFor } from "./cx";

// Squircle, never a circle. A circular avatar next to the system's rounded-square marks and
// bezelled surfaces is the one element that would look borrowed from somewhere else.
//
// Initials only. There is no photo anywhere in the design, so there is no image variant here.

export interface AvatarProps {
  /** Full name or email. Initials are derived from it, and it is what assistive tech announces. */
  name: string;
  size?: "sm" | "md";
  className?: string;
}

const SIZE = {
  sm: "h-[23px] w-[23px] rounded-avatar-sm text-[9.5px]",
  md: "h-8 w-8 rounded-avatar-md text-caption",
} as const;

/**
 * "Kavin Abeysinghe" to KA, "kavin.abeysinghe@example.com" to KA, "kavin@example.com" to K.
 *
 * The domain is dropped before anything else: without that, every user at the same company gets
 * the same second initial, which is the opposite of what an avatar is for. Two initials at most,
 * because three stop being readable at 23px.
 */
export function initialsFrom(name: string): string {
  const local = name.trim().split("@").at(0) ?? "";
  const words = local.split(/[\s._-]+/).filter(Boolean);
  if (words.length === 0) return "?";
  const first = words.at(0);
  const last = words.length > 1 ? words.at(-1) : undefined;
  return ((first?.[0] ?? "") + (last?.[0] ?? "")).toUpperCase();
}

export function Avatar({ name, size = "sm", className }: AvatarProps) {
  return (
    <span
      // The initials are a visual shorthand, so they are hidden and the real name is announced.
      role="img"
      aria-label={name}
      className={cx("grid flex-none place-items-center bg-feat font-bold text-feat-ink", styleFor(SIZE, size), className)}
    >
      <span aria-hidden="true">{initialsFrom(name)}</span>
    </span>
  );
}
