import { useState } from "react";
import {
  ArrowRightIcon,
  Avatar,
  Badge,
  Button,
  CheckIcon,
  Chip,
  ChipGroup,
  CloseIcon,
  Divider,
  Eyebrow,
  IconButton,
  Input,
  Kicker,
  PlusIcon,
  ProgressBar,
  Select,
  SegmentedControl,
  Sparkline,
  StatusTick,
  Surface,
  Textarea,
  Well,
} from "../ui";
import { GallerySection, Specimen, SpecimenGrid } from "./Specimen";

const RADII = [
  { name: "shell", value: "20px", use: "Outer bezel", cls: "rounded-shell" },
  { name: "core", value: "14px", use: "Inner bezel, 20 minus 6px padding", cls: "rounded-core" },
  { name: "ctl", value: "9px", use: "Inputs, ghost and subtle buttons, wells", cls: "rounded-ctl" },
  { name: "mark", value: "5px", use: "Badges, status ticks", cls: "rounded-mark" },
  { name: "pill", value: "full", use: "Primary buttons, chips", cls: "rounded-pill" },
];

// Full class strings, not `bg-${name}`: Tailwind scans source text, so an interpolated class name
// generates nothing and the swatch renders transparent.
const SWATCHES: Array<Array<[string, string]>> = [
  [
    ["bg", "bg-bg"],
    ["shell", "bg-shell"],
    ["core", "bg-core"],
    ["sunk", "bg-sunk"],
  ],
  [
    ["ink", "bg-ink"],
    ["ink-2", "bg-ink-2"],
    ["muted", "bg-muted"],
    ["faint", "bg-faint"],
  ],
  [
    ["ember", "bg-ember"],
    ["ember-hi", "bg-ember-hi"],
    ["ember-wash", "bg-ember-wash"],
    ["on-ember", "bg-on-ember"],
  ],
  [
    ["pos", "bg-pos"],
    ["pos-wash", "bg-pos-wash"],
    ["brass", "bg-brass"],
    ["brass-wash", "bg-brass-wash"],
  ],
  [
    ["feat", "bg-feat"],
    ["feat-ink", "bg-feat-ink"],
    ["hair", "bg-hair"],
    ["hair-2", "bg-hair-2"],
  ],
];

export function GalleryPrimitives() {
  const [filter, setFilter] = useState<"all" | "strong" | "good" | "weak">("all");
  const [arrangements, setArrangements] = useState<string[]>(["remote", "hybrid"]);
  const [industry, setIndustry] = useState<string | null>("tech");
  const [chipOn, setChipOn] = useState(true);

  return (
    <>
      <GallerySection
        id="tokens"
        title="1. Tokens"
        note="Every colour, radius and shadow in the system. Both themes are declared, never derived from one another, so a value that looks wrong here is wrong in the token layer rather than in a component."
      >
        <Specimen label="Colour" wide>
          <div className="flex flex-col gap-1.5">
            {SWATCHES.map(row => (
              <div key={row[0][0]} className="flex flex-wrap gap-1.5">
                {row.map(([name, swatchClass]) => (
                  <div key={name} className="hairline-ring flex items-center gap-2 rounded-ctl bg-core px-2 py-1.5">
                    <span className={`hairline-ring block h-5 w-5 rounded-mark ${swatchClass}`} />
                    <span className="text-meta text-muted">{name}</span>
                  </div>
                ))}
              </div>
            ))}
          </div>
        </Specimen>

        <Specimen label="Shape lock, the only five radii in the system" wide>
          <div className="flex flex-wrap gap-2.5">
            {RADII.map(r => (
              <div key={r.name} className="flex items-center gap-2.5">
                <span className={`hairline-ring block h-11 w-11 bg-shell ${r.cls}`} />
                <span className="text-meta text-muted">
                  <b className="block text-caption text-ink">
                    {r.name} · {r.value}
                  </b>
                  {r.use}
                </span>
              </div>
            ))}
          </div>
        </Specimen>

        <Specimen label="Elevation, blue-tinted in light and neutral in dark" wide>
          <div className="flex flex-wrap gap-3.5">
            <span className="grid h-16 w-28 place-items-center rounded-core bg-core text-meta text-muted shadow-e1">e1</span>
            <span className="grid h-16 w-28 place-items-center rounded-core bg-core text-meta text-muted shadow-e2">e2</span>
          </div>
        </Specimen>
      </GallerySection>

      <GallerySection
        id="surface"
        title="2. Surface, the double bezel"
        note="Shell wrapping core, 6px apart, curves concentric. This is the signature. If the inner and outer curves ever stop looking parallel, the radii or the padding have drifted."
      >
        <SpecimenGrid>
          <Specimen label="flat" wide>
            <Surface elevation="flat">
              <p className="m-0 text-body text-ink-2">No drop shadow. For a surface already inside another surface.</p>
            </Surface>
          </Specimen>
          <Specimen label="raised, the default" wide>
            <Surface>
              <p className="m-0 text-body text-ink-2">The standard card. e1 shadow.</p>
            </Surface>
          </Specimen>
          <Specimen label="floating" wide>
            <Surface elevation="floating">
              <p className="m-0 text-body text-ink-2">Drawers, sheets, menus. e2 shadow.</p>
            </Surface>
          </Specimen>
          <Specimen label="clip, padding none" wide>
            <Surface padding="none" clip>
              <p className="hairline-b m-0 px-3.5 py-2.5 text-body font-[650] text-ink">A header that paints to the edge</p>
              <p className="m-0 px-3.5 py-2.5 text-caption text-muted">Only possible because the core clips.</p>
            </Surface>
          </Specimen>
          <Specimen label="padding sm" wide>
            <Surface padding="sm">
              <p className="m-0 text-body text-ink-2">Dense rows, mobile cards.</p>
            </Surface>
          </Specimen>
          <Specimen label="padding lg" wide>
            <Surface padding="lg">
              <p className="m-0 text-body text-ink-2">Onboarding panels, anything with a single centred action.</p>
            </Surface>
          </Specimen>
        </SpecimenGrid>
        <Specimen label="Well, the recessed variant" wide>
          <Well className="px-3 py-2.5">
            <p className="m-0 text-caption text-ink-2">Inputs, segmented tracks and match reasons all sit in this.</p>
          </Well>
        </Specimen>
      </GallerySection>

      <GallerySection
        id="button"
        title="3. Button and IconButton"
        note="Primary is always a full pill, ghost and subtle are always 9px, at either size. Hover the primary buttons: the cap drifts up and right. That is the one flourish in the system."
      >
        <Specimen label="Primary, md, with and without a cap">
          <Button cap>Create account</Button>
          <Button cap={<CheckIcon className="h-3 w-3" />}>Save resume</Button>
          <Button>Sign in</Button>
        </Specimen>
        <Specimen label="Primary, sm">
          <Button size="sm" cap={<PlusIcon className="h-2.5 w-2.5" />}>
            Log application
          </Button>
          <Button size="sm">Generate CV</Button>
        </Specimen>
        <Specimen label="Ghost and subtle">
          <Button variant="ghost">Cover letter</Button>
          <Button variant="ghost" size="sm">
            PDF
          </Button>
          <Button variant="subtle">Back</Button>
          <Button variant="subtle" size="sm">
            Word
          </Button>
        </Specimen>
        <Specimen label="Loading and disabled">
          <Button loading cap>
            Generating&hellip;
          </Button>
          <Button disabled cap>
            Create account
          </Button>
          <Button variant="ghost" disabled>
            Cover letter
          </Button>
        </Specimen>
        <Specimen label="fullWidth, with a cap and without" wide>
          <div className="flex max-w-xs flex-col gap-2">
            <Button fullWidth cap>
              Create account
            </Button>
            <Button fullWidth variant="ghost">
              Continue with Google
            </Button>
          </div>
        </Specimen>
        <Specimen label="IconButton, aria-label required by the type">
          <IconButton aria-label="Close">
            <CloseIcon />
          </IconButton>
          <IconButton aria-label="Add" variant="subtle">
            <PlusIcon />
          </IconButton>
          <IconButton aria-label="Next" size="sm">
            <ArrowRightIcon />
          </IconButton>
        </Specimen>
      </GallerySection>

      <GallerySection
        id="forms"
        title="4. Field, Input, Textarea, Select"
        note="Field owns the ids. Click any label and focus lands on its control; the error states below are wired with aria-invalid and aria-describedby without the caller writing either."
      >
        <SpecimenGrid>
          <Specimen label="Input, plain and with a hint" wide>
            <div className="flex flex-col gap-3">
              <Input label="Email" type="email" inputMode="email" autoComplete="email" spellCheck={false} placeholder="you@example.com" />
              <Input
                label="Job posting link"
                type="url"
                inputMode="url"
                autoComplete="off"
                spellCheck={false}
                hint="Each generation uses 1 credit."
                defaultValue="seek.com.au/job/84412209"
              />
            </div>
          </Specimen>
          <Specimen label="Required, and the error state" wide>
            <div className="flex flex-col gap-3">
              <Input label="Password" type="password" autoComplete="new-password" required />
              <Input label="Email" type="email" defaultValue="kavin@" error="Add everything after the @ to continue." />
            </div>
          </Specimen>
          <Specimen label="Textarea" wide>
            <Textarea label="Summary" defaultValue="Software engineer with four years building production systems across ASP.NET Core and React." />
          </Specimen>
          <Specimen label="Select" wide>
            <Select label="Country" defaultValue="Australia">
              <option>Australia</option>
              <option>New Zealand</option>
            </Select>
          </Specimen>
        </SpecimenGrid>
      </GallerySection>

      <GallerySection
        id="selection"
        title="5. SegmentedControl, Chip, ChipGroup"
        note="Tab into the segmented control and use the arrow keys: one tab stop, arrows move and select, Home and End jump to the ends, and it wraps."
      >
        <Specimen label="SegmentedControl, single select with counts" wide>
          <SegmentedControl
            label="Filter discoveries"
            value={filter}
            onChange={setFilter}
            segments={[
              { value: "all", label: "All", count: 14 },
              { value: "strong", label: "Strong", count: 2 },
              { value: "good", label: "Good", count: 5 },
              { value: "weak", label: "Weak", count: 7 },
            ]}
          />
        </Specimen>
        <Specimen label="SegmentedControl, fullWidth (the mobile form)" wide>
          <div className="max-w-xs">
            <SegmentedControl
              label="Resume view"
              fullWidth
              value={filter === "all" ? "all" : "strong"}
              onChange={v => setFilter(v)}
              segments={[
                { value: "all", label: "Edit" },
                { value: "strong", label: "Preview" },
              ]}
            />
          </div>
        </Specimen>
        <Specimen label="Chip, on and off">
          <Chip selected={chipOn} onClick={() => setChipOn(o => !o)}>
            Remote
          </Chip>
          <Chip selected={!chipOn} onClick={() => setChipOn(o => !o)}>
            On site
          </Chip>
          <Chip selected={false} onClick={() => {}} disabled>
            Disabled
          </Chip>
        </Specimen>
        <Specimen label="ChipGroup, multi select" wide>
          <ChipGroup
            label="Work arrangement"
            multi
            value={arrangements}
            onChange={setArrangements}
            options={[
              { value: "remote", label: "Remote" },
              { value: "hybrid", label: "Hybrid" },
              { value: "onsite", label: "On site" },
            ]}
          />
        </Specimen>
        <Specimen label="ChipGroup, single select" wide>
          <ChipGroup
            label="Industry template"
            value={industry}
            onChange={setIndustry}
            options={[
              { value: "tech", label: "Tech" },
              { value: "finance", label: "Finance" },
              { value: "legal", label: "Legal" },
              { value: "healthcare", label: "Healthcare" },
              { value: "creative", label: "Creative" },
            ]}
          />
        </Specimen>
      </GallerySection>

      <GallerySection
        id="marks"
        title="6. Badge, StatusTick, Avatar, Divider, Eyebrow, Kicker"
        note="Badges are 5px marks and chips are pills, deliberately: shape alone says whether a thing is pressable."
      >
        <Specimen label="Badge, all five variants">
          <Badge variant="strong">Strong</Badge>
          <Badge variant="good">Good</Badge>
          <Badge variant="weak">Weak</Badge>
          <Badge variant="live">Interviewing</Badge>
          <Badge variant="neutral">Draft</Badge>
        </Specimen>
        <Specimen label="StatusTick, three states at three sizes">
          {(["sm", "md", "lg"] as const).map(size => (
            <span key={size} className="flex items-center gap-2">
              <StatusTick state="done" size={size} />
              <StatusTick state="live" size={size} />
              <StatusTick state="pending" size={size} number={3} />
              <span className="text-meta text-faint">{size}</span>
            </span>
          ))}
        </Specimen>
        <Specimen label="Avatar, squircle not circle">
          <Avatar name="Kavin Abeysinghe" />
          <Avatar name="Kavin Abeysinghe" size="md" />
          <Avatar name="kavin@example.com" size="md" />
        </Specimen>
        <Specimen label="Divider, plain and labelled" wide>
          <div className="flex max-w-xs flex-col gap-4">
            <Divider />
            <Divider>or</Divider>
          </div>
        </Specimen>
        <Specimen label="Eyebrow and Kicker">
          <Eyebrow>While you were asleep</Eyebrow>
          <Kicker>Handled overnight</Kicker>
        </Specimen>
      </GallerySection>

      <GallerySection id="meters" title="7. ProgressBar and Sparkline">
        <Specimen label="ProgressBar, wizard progress and a credit meter" wide>
          <div className="flex max-w-sm flex-col gap-3">
            <ProgressBar value={5} max={8} label="Question 5 of 8" />
            <ProgressBar value={128} max={200} label="128 of 200 credits" />
            <ProgressBar value={0} max={8} label="Not started" />
          </div>
        </Specimen>
        <Specimen label="Sparkline, last value emphasised" wide>
          <div className="flex max-w-xs gap-6">
            <Sparkline values={[28, 50, 40, 68, 56, 86, 100]} label="Applications sent, rising" className="w-24" />
            <Sparkline values={[42, 34, 58, 46, 70, 62, 80]} label="Reply rate, rising" className="w-24" />
          </div>
        </Specimen>
      </GallerySection>
    </>
  );
}
