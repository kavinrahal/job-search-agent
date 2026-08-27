import { useState } from "react";
import {
  AccountMenu,
  ActivityIcon,
  Badge,
  BottomTabs,
  Button,
  Callout,
  ChecklistIcon,
  CreditIcon,
  CreditPill,
  DocumentIcon,
  DocumentPage,
  Drawer,
  EmptyState,
  FeaturePanel,
  GiftIcon,
  HelpIcon,
  Ledger,
  LedgerGroup,
  LedgerRow,
  LifebuoyIcon,
  MatchReason,
  NavItem,
  PageHeader,
  PasswordRulesChecklist,
  SearchIcon,
  SettingsIcon,
  SignOutIcon,
  Skeleton,
  SkeletonList,
  SlidersIcon,
  StatBlock,
  StepIndicator,
  Surface,
  Tab,
  Timeline,
  TimelineItem,
  Tooltip,
  TopNav,
} from "../ui";
import { GallerySection, Specimen, SpecimenGrid } from "./Specimen";

const RULES = [
  { id: "len", label: "8 characters", met: true },
  { id: "lower", label: "Lowercase", met: true },
  { id: "upper", label: "Uppercase", met: true },
  { id: "num", label: "A number", met: true },
  { id: "sym", label: "A symbol", met: false },
];

export function GalleryComposites() {
  const [drawerOpen, setDrawerOpen] = useState(false);

  return (
    <>
      <GallerySection
        id="ledger"
        title="8. Ledger"
        note="Rows separated by a hairline and nothing else, so twelve of them read as one scannable list. Narrow the pane with the width control above: the role line truncates and the badge column never moves."
      >
        <Surface padding="none" clip>
          <Ledger>
            <LedgerGroup>This week</LedgerGroup>
            <LedgerRow
              tick="done"
              title="Willow Inc"
              subtitle="Senior Software Engineer, Platform and Developer Experience"
              meta={
                <>
                  <Badge variant="good">Offer</Badge>
                  <span className="text-meta text-faint">04 Feb</span>
                </>
              }
              href="#ledger"
            />
            <LedgerRow
              tick="live"
              title="Kolmeo"
              subtitle="Backend Engineer"
              meta={
                <>
                  <Badge variant="live">Interviewing</Badge>
                  <span className="text-meta text-faint">03 Feb</span>
                </>
              }
              href="#ledger"
            />
            <LedgerGroup>Earlier</LedgerGroup>
            <LedgerRow
              tick="done"
              title="NCS Australia"
              subtitle="Senior Software Engineer, .Net and AWS"
              meta={<Badge variant="weak">Rejected</Badge>}
              href="#ledger"
            />
            <LedgerRow tick="pending" title="Victorian Government" subtitle="Developer, Digital Services" meta={<Badge>Draft</Badge>} />
          </Ledger>
        </Surface>
      </GallerySection>

      <GallerySection
        id="feature"
        title="9. FeaturePanel and StatBlock"
        note="The feature panel stays dark in both themes. Stat blocks render unboxed: the density rules ban a card around a metric, so containing them is the surrounding Surface's job, not theirs."
      >
        <FeaturePanel
          eyebrow="While you were asleep"
          title="5 postings checked. 2 worth a look."
          subtitle="Last run 6:12am. Next run tonight."
          stats={[
            { value: 2, label: "Strong matches" },
            { value: 12, label: "Live applications" },
            { value: 3, label: "Need a reply" },
          ]}
        />
        <Specimen label="StatBlock, unboxed, inside one Surface separated by a hairline" wide>
          <Surface padding="none">
            <div className="grid grid-cols-2">
              <div className="px-3.5 py-3">
                <StatBlock value={34} label="Applications sent" trend={[28, 50, 40, 68, 56, 86, 100]} />
              </div>
              <div className="hairline-l px-3.5 py-3">
                <StatBlock value={18} suffix="%" label="Reply rate" trend={[42, 34, 58, 46, 70, 62, 80]} />
              </div>
            </div>
          </Surface>
        </Specimen>
        <Specimen label="StatBlock, no trend">
          <StatBlock value={128} label="Credits remaining" />
        </Specimen>
      </GallerySection>

      <GallerySection
        id="reason"
        title="10. MatchReason and Callout"
        note="A pos rule means the system is recommending this. A faint rule means it found it and is telling you why it held back. Same shape, opposite meaning."
      >
        <SpecimenGrid>
          <Specimen label="why" wide>
            <MatchReason heading="Why this one.">Matches C# and Azure, salary band above your floor, Melbourne hybrid.</MatchReason>
          </Specimen>
          <Specimen label="held-back" wide>
            <MatchReason tone="held-back" heading="Held back.">
              Recruiter listing with no named client and no salary band.
            </MatchReason>
          </Specimen>
        </SpecimenGrid>
        <Specimen label="Callout, the accuracy warning bar and its two siblings" wide>
          <div className="flex flex-col gap-2.5">
            <Callout title="Worth checking before you send.">
              The phrase &ldquo;led a team of six&rdquo; does not appear anywhere in your background.
            </Callout>
            <Callout variant="info" title="Sources run overnight.">
              Anything matching your criteria will be here in the morning.
            </Callout>
            <Callout variant="danger" title="That posting could not be fetched.">
              Paste the description instead and we will work from that.
            </Callout>
          </div>
        </Specimen>
      </GallerySection>

      <GallerySection id="timeline" title="11. Timeline and StepIndicator">
        <Specimen label="Timeline, newest first, last item drops the rule" wide>
          <Surface>
            <Timeline>
              <TimelineItem
                state="live"
                title="Interview scheduled, round 2"
                detail="Detected from an email from talent@example.com"
                meta="03 Feb"
              />
              <TimelineItem state="done" title="Recruiter screen completed" detail="Screening to Interviewing" meta="29 Jan" />
              <TimelineItem state="done" title="Applied" detail="CV and cover letter generated here" meta="24 Jan" last />
            </Timeline>
          </Surface>
        </Specimen>
        <Specimen label="StepIndicator, at each of the three positions" wide>
          <div className="flex flex-col gap-4">
            {[0, 1, 2].map(current => (
              <StepIndicator
                key={current}
                current={current}
                steps={[{ label: "Your background" }, { label: "Job criteria" }, { label: "Sources" }]}
              />
            ))}
          </div>
        </Specimen>
      </GallerySection>

      <GallerySection
        id="document"
        title="12. DocumentPage"
        note="A4 locked at 210/297. Narrow the pane with the width control: the page scrolls sideways rather than reflowing or shrinking, because a preview that reflows is lying about what will print."
      >
        {/* The resume builder holds the page to 330px so it sits beside the editor; the Generate
            preview lets it fill its column. Both are shown, since maxWidth is the only thing that
            differs between them. */}
        <Specimen label="maxWidth 330, the resume builder's preview" wide>
          <DocumentPage maxWidth={330}>
            <div className="px-4 py-3.5">
              <p className="m-0 text-[12px] font-bold tracking-[-.03em]">Kavin Abeysinghe</p>
              <p className="mb-2 text-[7px] text-[#6a707a]">Melbourne VIC · kavin@example.com</p>
              <p className="mb-1 border-b border-[#e4e8ee] pb-[2px] text-[6.5px] font-bold tracking-[.16em] text-[#39404a] uppercase">
                Summary
              </p>
              <p className="m-0 text-[6.5px] text-[#39404a]">
                Software engineer with four years building production systems across ASP.NET Core and React.
              </p>
            </div>
          </DocumentPage>
        </Specimen>
        <Specimen label="default maxWidth, the Generate preview" wide>
          <DocumentPage>
            <div className="px-5 py-4.5">
            <p className="m-0 text-[14px] font-bold tracking-[-.03em]">Kavin Abeysinghe</p>
            <p className="mb-2.5 text-[8px] text-[#6a707a]">Melbourne VIC · kavin@example.com</p>
            <p className="mb-1.5 border-b border-[#e4e8ee] pb-[3px] text-[7.5px] font-bold tracking-[.16em] text-[#39404a] uppercase">
              Summary
            </p>
            <p className="m-0 text-[7.5px] text-[#39404a]">
              Software engineer with four years building production systems across ASP.NET Core and React, most recently on high volume
              sensor ingestion.
            </p>
            <p className="mt-2.5 mb-1.5 border-b border-[#e4e8ee] pb-[3px] text-[7.5px] font-bold tracking-[.16em] text-[#39404a] uppercase">
              Experience
            </p>
            <p className="m-0 text-[9px] font-bold">Software Engineer, Willow Inc</p>
            <p className="m-0 text-[7.5px] text-[#767c86]">2023 to 2026</p>
            <ul className="m-0 list-disc pl-3 text-[7.5px] text-[#39404a]">
              <li>Built ingestion handling 50,000 concurrent MQTT sensor connections</li>
              <li>Migrated CI from Azure DevOps to GitHub Actions</li>
            </ul>
            </div>
          </DocumentPage>
        </Specimen>
      </GallerySection>

      <GallerySection id="credit" title="13. CreditPill and PasswordRulesChecklist">
        <Specimen label="CreditPill, healthy, low and compact">
          <CreditPill credits={128} />
          <CreditPill credits={2} />
          <CreditPill credits={128} compact />
        </Specimen>
        <Specimen label="PasswordRulesChecklist, presentational only" wide>
          <div className="grid gap-3.5 sm:grid-cols-2">
            <PasswordRulesChecklist rules={RULES} />
            <PasswordRulesChecklist rules={RULES.map(r => ({ ...r, met: true }))} />
          </div>
        </Specimen>
      </GallerySection>

      <GallerySection
        id="shell"
        title="14. Navigation, AccountMenu, PageHeader"
        note="TopNav hides below md and BottomTabs hides at md and up, so only one of the two blocks below is visible at any width. The account menu is where the seven cut nav items live."
      >
        <Specimen label="TopNav, active and inactive" wide>
          <div className="hairline-b flex items-center justify-between gap-4 rounded-core bg-core px-3.5 py-2.5">
            <TopNav className="!flex">
              <NavItem href="#shell" active>
                Today
              </NavItem>
              <NavItem href="#shell">Discover</NavItem>
              <NavItem href="#shell">Generate</NavItem>
              <NavItem href="#shell">Applications</NavItem>
            </TopNav>
            <div className="flex items-center gap-2.5">
              <CreditPill credits={128} />
              <AccountMenu
                name="Kavin Abeysinghe"
                email="kavin@example.com"
                items={[
                  { label: "Resume", href: "#shell", icon: <DocumentIcon /> },
                  { label: "Criteria", href: "#shell", icon: <SlidersIcon /> },
                  { label: "Sources", href: "#shell", icon: <SearchIcon /> },
                  { label: "Settings", href: "#shell", icon: <SettingsIcon /> },
                  { label: "Help", href: "#shell", icon: <HelpIcon /> },
                  { label: "Support", href: "#shell", icon: <LifebuoyIcon /> },
                  { label: "Sign out", onSelect: () => {}, icon: <SignOutIcon />, separated: true },
                ]}
              />
            </div>
          </div>
        </Specimen>

        <Specimen label="BottomTabs, the 4-item Tier 2 set and the 3-item Tier 1 set" wide>
          <div className="flex flex-col gap-3">
            <div className="relative h-14 overflow-hidden rounded-core bg-core">
              <BottomTabs className="!static !flex !pb-2">
                <Tab href="#shell" active icon={<ActivityIcon />} label="Today" />
                <Tab href="#shell" icon={<SearchIcon />} label="Discover" />
                <Tab href="#shell" icon={<DocumentIcon />} label="Generate" />
                <Tab href="#shell" icon={<ChecklistIcon />} label="Apps" />
              </BottomTabs>
            </div>
            <div className="relative h-14 overflow-hidden rounded-core bg-core">
              <BottomTabs className="!static !flex !pb-2">
                <Tab href="#shell" active icon={<ActivityIcon />} label="Today" />
                <Tab href="#shell" icon={<DocumentIcon />} label="Generate" />
                <Tab href="#shell" icon={<ChecklistIcon />} label="Apps" />
              </BottomTabs>
            </div>
          </div>
        </Specimen>

        <Specimen label="PageHeader, with and without actions" wide>
          <div className="flex flex-col gap-4">
            <PageHeader title="Applications" tagline="Everything you have sent, and where each one got to." />
            <PageHeader
              title="Discover"
              tagline="Filtered against your criteria."
              actions={
                <Button size="sm" cap>
                  Generate CV
                </Button>
              }
            />
          </div>
        </Specimen>
      </GallerySection>

      <GallerySection
        id="drawer"
        title="15. Drawer"
        note="Open it and try Escape, then Tab past the last control. Focus is trapped, the background does not scroll, and closing returns focus to the button that opened it. This is the one component the two panes cannot show separately: it portals to the document body to escape ancestor stacking contexts, so it always follows the document theme rather than the pane it was opened from. Use the theme control in the bar to see the other one."
      >
        <Specimen label="Side panel on desktop, bottom sheet on mobile">
          <Button cap onClick={() => setDrawerOpen(true)}>
            Open drawer
          </Button>
        </Specimen>
        <Drawer
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
          title="Victorian Government"
          description="Senior Developer, Enterprise Technology"
          footer={
            <>
              <Button fullWidth cap onClick={() => setDrawerOpen(false)}>
                Generate CV
              </Button>
              <Button fullWidth variant="ghost" onClick={() => setDrawerOpen(false)}>
                Cancel
              </Button>
            </>
          }
        >
          <MatchReason heading="Why this one.">Matches C# and Azure, salary band above your floor, Melbourne hybrid.</MatchReason>
          <p className="mt-3 mb-0 text-meta text-faint">Generating uses 1 credit.</p>
        </Drawer>
      </GallerySection>

      <GallerySection
        id="states"
        title="16. Skeleton, EmptyState, Tooltip"
        note="Skeletons are shaped like the rows they replace, never a spinner. Empty state copy says what happens next rather than reporting the absence."
      >
        <SpecimenGrid>
          <Specimen label="Skeleton, a ledger's worth, then line and block" wide>
            <Surface>
              <SkeletonList rows={3} label="Loading discoveries" />
              <div className="mt-3 flex flex-col gap-2">
                <Skeleton width="70%" />
                <Skeleton variant="block" />
              </div>
            </Surface>
          </Specimen>
          <Specimen label="EmptyState" wide>
            <Surface padding="none">
              <EmptyState
                icon={<GiftIcon />}
                tone="positive"
                title="Nothing delivered yet"
                body="The first run happens tonight. Anything matching your criteria will be here in the morning."
                action={
                  <Button variant="ghost" size="sm" href="#states">
                    Check criteria
                  </Button>
                }
              />
            </Surface>
          </Specimen>
        </SpecimenGrid>
        <Specimen label="EmptyState, out of credits and page not found" wide>
          <SpecimenGrid>
            <Surface padding="none">
              <EmptyState
                icon={<CreditIcon />}
                tone="ember"
                title="You are out of credits"
                body="Discovery and tracking keep running. You just cannot generate new documents until you top up."
                action={
                  <Button size="sm" cap href="#states">
                    Top up
                  </Button>
                }
              />
            </Surface>
            <Surface padding="none">
              <EmptyState
                icon={<SearchIcon />}
                title="That page is not here"
                body="The link may be old, or the thing it pointed at was removed. Nothing is broken on your account."
                action={
                  <Button size="sm" cap href="#states">
                    Back to Today
                  </Button>
                }
              />
            </Surface>
          </SpecimenGrid>
        </Specimen>
        <Specimen label="Tooltip, click or hover, Escape dismisses">
          <span className="flex items-center gap-1 text-caption text-muted">
            Salary floor
            <Tooltip label="About salary floor" text="Postings below this are still collected, but they are never marked as a strong match." />
          </span>
        </Specimen>
      </GallerySection>
    </>
  );
}
