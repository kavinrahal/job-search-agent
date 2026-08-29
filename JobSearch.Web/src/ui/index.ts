// The Slate design system.
//
// Import from "../ui", not from individual files, so the surface stays one thing. Tokens live in
// ./tokens.css, which src/index.css imports once.
//
// This library supersedes lib/styles.ts and several components/ primitives (ChoiceButtons,
// ThemeToggle, InfoTooltip, PageTagline, and CardEditor's LABEL/INPUT/Field). None of those have
// been touched yet — the page-by-page swap is a separate pass.

export { cx, styleFor } from "./cx";

// Layer 0, foundation
export { ThemeProvider, useTheme } from "./ThemeProvider";
export type { ThemePreference, ResolvedTheme } from "./ThemeProvider";
export { Grain } from "./Grain";

// Layer 1, primitives
export { Surface, Well } from "./Surface";
export type { SurfaceProps, SurfaceElevation } from "./Surface";
export { Button, IconButton } from "./Button";
export type { ButtonProps, ButtonVariant, ButtonSize, IconButtonProps } from "./Button";
export { Field, Input, Textarea, Select } from "./Field";
export type { FieldProps, FieldRenderProps, InputProps, TextareaProps, SelectProps } from "./Field";
export { SegmentedControl } from "./SegmentedControl";
export type { Segment, SegmentedControlProps } from "./SegmentedControl";
export { Chip, ChipGroup } from "./Chip";
export type { ChipOption, ChipProps } from "./Chip";
export { Badge } from "./Badge";
export type { BadgeVariant } from "./Badge";
export { StatusTick } from "./StatusTick";
export type { StatusTickState, StatusTickProps } from "./StatusTick";
export { SourceStatusTile } from "./SourceStatusTile";
export type { SourceStatusTileProps } from "./SourceStatusTile";
export { Avatar, initialsFrom } from "./Avatar";
export type { AvatarProps } from "./Avatar";
export { Divider } from "./Divider";
export { Eyebrow, Kicker } from "./Eyebrow";
export { ProgressBar } from "./ProgressBar";
export type { ProgressBarProps } from "./ProgressBar";
export { Sparkline } from "./Sparkline";
export type { SparklineProps } from "./Sparkline";

// Layer 2, composites
export { Ledger, LedgerGroup, LedgerRow } from "./Ledger";
export type { LedgerRowProps } from "./Ledger";
export { FeaturePanel } from "./FeaturePanel";
export type { FeaturePanelProps, FeatureStat } from "./FeaturePanel";
export { StatBlock } from "./StatBlock";
export type { StatBlockProps } from "./StatBlock";
export { CountUp } from "./CountUp";
export type { CountUpProps } from "./CountUp";
export { MatchReason } from "./MatchReason";
export type { MatchReasonProps, MatchReasonTone } from "./MatchReason";
export { Callout } from "./Callout";
export type { CalloutProps, CalloutVariant } from "./Callout";
export { Timeline, TimelineItem } from "./Timeline";
export type { TimelineItemProps } from "./Timeline";
export { StepIndicator } from "./StepIndicator";
export type { Step, StepIndicatorProps } from "./StepIndicator";
export { DocumentPage } from "./DocumentPage";
export type { DocumentPageProps } from "./DocumentPage";
export { CreditPill } from "./CreditPill";
export type { CreditPillProps } from "./CreditPill";
export { PasswordRulesChecklist } from "./PasswordRulesChecklist";
export type { PasswordRuleState, PasswordRulesChecklistProps } from "./PasswordRulesChecklist";

// Layer 3, shell and navigation
export { AppShell, Brand } from "./AppShell";
export type { AppShellProps } from "./AppShell";
export { TopNav, NavItem, BottomTabs, Tab } from "./Nav";
export type { NavItemProps, TabProps } from "./Nav";
export { AccountMenu } from "./AccountMenu";
export type { AccountMenuProps, AccountMenuItem } from "./AccountMenu";
export { PageHeader } from "./PageHeader";
export type { PageHeaderProps } from "./PageHeader";
export { SettingsSubNav } from "./SettingsSubNav";
export type { SettingsSubNavItem, SettingsSubNavProps } from "./SettingsSubNav";
export { SkipLink } from "./SkipLink";
export { Drawer } from "./Drawer";
export type { DrawerProps } from "./Drawer";
export { Modal } from "./Modal";
export type { ModalProps } from "./Modal";
export { ThemeToggle } from "./ThemeToggle";

// Layer 4, state and feedback
export { Skeleton, SkeletonRow, SkeletonList } from "./Skeleton";
export type { SkeletonProps } from "./Skeleton";
export { EmptyState } from "./EmptyState";
export type { EmptyStateProps, EmptyStateTone } from "./EmptyState";
export { Tooltip } from "./Tooltip";
export type { TooltipProps } from "./Tooltip";

export * from "./icons";
