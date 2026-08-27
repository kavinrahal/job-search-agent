import { useState, type ComponentType } from "react";
import { useProfile, useUpdateProfile } from "../hooks/useProfile";
import { useSyncedState } from "../hooks/useSyncedState";
import { parseJobCriteriaYaml, serializeJobCriteriaYaml, type JobCriteriaData } from "../lib/jobCriteriaYaml";
import { COUNTRIES, COUNTRY_TO_CURRENCY } from "../lib/regionData";
import { Field, INPUT } from "./CardEditor";
import { Surface, Button, ChipGroup, PageHeader, Select } from "../ui";
import { EMPLOYMENT_TYPES } from "./JobCriteriaEditor";
import {
  type CriteriaPatch,
  EXPERIENCE_BUCKETS, experienceBucketPatch, nearestExperienceBucket,
  SALARY_SLIDER_MIN, SALARY_SLIDER_MAX, SALARY_SLIDER_STEP,
  salaryRangePatch, nearestSalaryRange, formatSalaryAmount,
  applySkillDimensionAnswer,
  SPONSORSHIP_YES_PATCH,
  simpleDisqualifierDescriptions, applyDisqualifierAnswer,
  sanitizeCriteriaInput,
} from "../lib/criteriaWizardMapping";

// A short, mostly-button, one-question-per-screen replacement for embedding the full
// JobCriteriaEditor directly in onboarding. Covers only the fields scored highest for driving
// evaluate_posting.md's recommendation (see the plan this shipped with) — everything else stays
// exactly where it always was, in the full editor at /criteria, unaffected by this component.
//
// Every step follows the same contract: it owns its own local "pending answer" state (so partial
// typing/clicking never touches shared data until the user explicitly hits Next), and calls
// onNext(patch) to commit. Back never carries a patch, so it can never overwrite an existing
// real value — the wizard-level commit function only ever merges what onNext explicitly hands
// it. Every step except Disqualifiers requires a real answer before Next enables at all — a
// candidate with no dealbreakers is a complete answer, but every other question has no
// meaningful "nothing" state, so Skip only exists on that one step.

const EMPTY: JobCriteriaData = parseJobCriteriaYaml("");

interface StepProps {
  data: JobCriteriaData;
  onNext: (patch: CriteriaPatch) => void;
  onSkip: () => void;
  onBack: () => void;
  isFirst: boolean;
  isLast: boolean;
}

// canSkip defaults to false — Disqualifiers is the only step that opts in. Everything else in
// this wizard is a mandatory question now: a candidate with no dealbreakers worth naming is a
// complete answer, but "no experience level," "no skills," "no location" etc. aren't real
// answers, they're gaps that would otherwise silently reach evaluate_posting.md.
function StepFooter({ onBack, onSkip, onNext, isFirst, isLast, canSkip = false, nextDisabled = false }: {
  onBack: () => void; onSkip: () => void; onNext: () => void; isFirst: boolean; isLast: boolean;
  canSkip?: boolean; nextDisabled?: boolean;
}) {
  return (
    <div className="mt-6 flex items-center justify-between">
      {isFirst ? <span /> : <Button variant="ghost" size="sm" onClick={onBack}>Back</Button>}
      <div className="flex items-center gap-4">
        {canSkip && <Button variant="ghost" size="sm" onClick={onSkip}>Skip</Button>}
        <Button onClick={onNext} disabled={nextDisabled}>
          {isLast ? "Finish" : "Next"}
        </Button>
      </div>
    </div>
  );
}

function StepHeading({ children, hint }: { children: string; hint?: string }) {
  return <PageHeader title={children} tagline={hint} className="mb-4" />;
}

function TitlesStep({ data, onNext, ...nav }: StepProps) {
  const [titles, setTitles] = useState(data.targetJobTitles);
  return (
    <div>
      <StepHeading hint="Exact job titles, comma-separated — this is what gets searched automatically for new postings.">
        What roles are you looking for?
      </StepHeading>
      <input
        className={INPUT}
        placeholder="Software Engineer, Backend Developer"
        value={titles}
        onChange={e => setTitles(e.target.value)}
      />
      <StepFooter {...nav} nextDisabled={!titles.trim()} onNext={() => onNext({ targetJobTitles: sanitizeCriteriaInput(titles) })} />
    </div>
  );
}

function ExperienceStep({ data, onNext, ...nav }: StepProps) {
  const [selected, setSelected] = useState<string | null>(nearestExperienceBucket(data));
  return (
    <div>
      <StepHeading>How much experience do you have?</StepHeading>
      <ChipGroup
        label="Experience"
        options={EXPERIENCE_BUCKETS.map(b => ({ value: b.id, label: b.label }))}
        value={selected}
        onChange={setSelected}
      />
      <StepFooter {...nav} nextDisabled={!selected} onNext={() => onNext(selected ? experienceBucketPatch(selected) : {})} />
    </div>
  );
}

function SkillsStep({ data, onNext, ...nav }: StepProps) {
  const existing = data.skillDimensions[0];
  const [name, setName] = useState(existing?.name ?? "");
  const [strongMatch, setStrongMatch] = useState(existing?.strongMatch ?? "");
  const [goodMatch, setGoodMatch] = useState(existing?.goodMatch ?? "");
  return (
    <div>
      <StepHeading hint='e.g. "Backend stack", "EHR system experience", "Knife skills" — whatever matters most in your field.'>
        What's the most important skill or specialization for you?
      </StepHeading>
      <div className="space-y-3">
        <Field label="Skill or specialization *" value={name} onChange={setName} />
        <Field label="Must-haves (comma-separated) *" value={strongMatch} onChange={setStrongMatch} />
        <Field label="Nice-to-haves, optional (comma-separated)" value={goodMatch} onChange={setGoodMatch} />
      </div>
      <StepFooter {...nav} nextDisabled={!name.trim() || !strongMatch.trim()} onNext={() => onNext({
        skillDimensions: applySkillDimensionAnswer(data.skillDimensions, {
          name: sanitizeCriteriaInput(name),
          strongMatch: sanitizeCriteriaInput(strongMatch),
          goodMatch: sanitizeCriteriaInput(goodMatch),
        }),
      })} />
    </div>
  );
}

function EmploymentStep({ data, onNext, ...nav }: StepProps) {
  const [selected, setSelected] = useState<string[]>(data.employmentTypes);
  return (
    <div>
      <StepHeading>What type of work are you looking for?</StepHeading>
      <ChipGroup
        multi
        label="Employment type"
        options={EMPLOYMENT_TYPES.map(t => ({ value: t, label: t.replace("_", " ") }))}
        value={selected}
        onChange={setSelected}
      />
      <StepFooter {...nav} nextDisabled={selected.length === 0} onNext={() => onNext({ employmentTypes: selected })} />
    </div>
  );
}

const ARRANGEMENT_OPTIONS = [
  { value: "remote", label: "Remote" },
  { value: "hybrid", label: "Hybrid" },
  { value: "onsite", label: "On-site" },
];

function LocationStep({ data, onNext, ...nav }: StepProps) {
  const [country, setCountry] = useState(data.countries.split(",")[0]?.trim() ?? "");
  const [arrangements, setArrangements] = useState<string[]>([
    ...(data.remoteAccepted ? ["remote"] : []),
    ...(data.hybridAccepted ? ["hybrid"] : []),
    ...(data.onsiteAccepted ? ["onsite"] : []),
  ]);

  function commit() {
    // country's initial value comes from data.countries, which can carry an arbitrary string
    // typed into the full editor's raw-YAML Advanced box (parseJobCriteriaYaml doesn't validate
    // it against COUNTRIES) — a plain `COUNTRY_TO_CURRENCY[country]` read would resolve an
    // inherited key like "constructor" from Object.prototype instead of falling through to
    // data.currency, since the result is a truthy function rather than undefined. hasOwnProperty
    // makes this an own-key-only lookup so an unmapped/unexpected country always falls back.
    const hasCurrency = Object.prototype.hasOwnProperty.call(COUNTRY_TO_CURRENCY, country);
    onNext({
      countries: country,
      // eslint-disable-next-line security/detect-object-injection -- guarded by hasOwnProperty above
      currency: hasCurrency ? COUNTRY_TO_CURRENCY[country] : data.currency,
      remoteAccepted: arrangements.includes("remote"),
      hybridAccepted: arrangements.includes("hybrid"),
      onsiteAccepted: arrangements.includes("onsite"),
    });
  }

  return (
    <div>
      <StepHeading hint="States/regions can be added later in the full criteria editor.">
        Where do you want to work?
      </StepHeading>
      <Select label="Country" value={country} onChange={e => setCountry(e.target.value)}>
        <option value="">Select a country</option>
        {COUNTRIES.map(c => <option key={c} value={c}>{c}</option>)}
      </Select>
      <div className="mt-3">
        <ChipGroup multi label="Work arrangement" options={ARRANGEMENT_OPTIONS} value={arrangements} onChange={setArrangements} />
      </div>
      <StepFooter {...nav} nextDisabled={!country || arrangements.length === 0} onNext={commit} />
    </div>
  );
}

// Two overlapping native <input type="range"> elements sharing one visual track, rather than a
// slider library — the well-worn zero-dependency technique for this exact UI: pointer-events is
// disabled on the input itself and re-enabled only on its thumb pseudo-element, so each thumb is
// independently grabbable even though both inputs occupy the same position. The colored bar
// between the two thumbs is a plain positioned div, not part of either input.
const RANGE_THUMB_CLASSES =
  "absolute inset-0 h-6 w-full cursor-pointer appearance-none bg-transparent pointer-events-none " +
  "[&::-webkit-slider-runnable-track]:bg-transparent [&::-moz-range-track]:bg-transparent " +
  "[&::-webkit-slider-thumb]:pointer-events-auto [&::-webkit-slider-thumb]:h-4 [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-ember [&::-webkit-slider-thumb]:shadow " +
  "[&::-moz-range-thumb]:pointer-events-auto [&::-moz-range-thumb]:h-4 [&::-moz-range-thumb]:w-4 [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:border-0 [&::-moz-range-thumb]:bg-ember";

function DualRangeSlider({ min, max, step, value, onChange }: {
  min: number; max: number; step: number; value: [number, number]; onChange: (v: [number, number]) => void;
}) {
  const [lo, hi] = value;
  const pctLo = ((lo - min) / (max - min)) * 100;
  const pctHi = ((hi - min) / (max - min)) * 100;
  return (
    <div className="relative h-6">
      <div className="absolute top-1/2 h-1.5 w-full -translate-y-1/2 rounded-full bg-sunk" />
      <div
        className="absolute top-1/2 h-1.5 -translate-y-1/2 rounded-full bg-ember"
        style={{ left: `${pctLo}%`, right: `${100 - pctHi}%` }}
      />
      <input
        type="range" min={min} max={max} step={step} value={lo} aria-label="Minimum salary"
        onChange={e => onChange([Math.min(Number(e.target.value), hi), hi])}
        className={RANGE_THUMB_CLASSES}
      />
      <input
        type="range" min={min} max={max} step={step} value={hi} aria-label="Maximum salary"
        onChange={e => onChange([lo, Math.max(Number(e.target.value), lo)])}
        className={RANGE_THUMB_CLASSES}
      />
    </div>
  );
}

function SalaryStep({ data, onNext, ...nav }: StepProps) {
  const initial = nearestSalaryRange(data);
  const [range, setRange] = useState<[number, number]>([initial.min, initial.max]);
  const [min, max] = range;
  const currency = data.currency || "AUD";

  return (
    <div>
      <StepHeading>What salary range are you happy with?</StepHeading>
      <p className="flex items-baseline gap-2 text-display font-bold text-ink-2">
        <span>{formatSalaryAmount(min, currency)}</span>
        <span className="text-note font-normal text-faint">to</span>
        <span>{formatSalaryAmount(max, currency)}</span>
      </p>

      <div className="mt-5">
        <DualRangeSlider min={SALARY_SLIDER_MIN} max={SALARY_SLIDER_MAX} step={SALARY_SLIDER_STEP} value={range} onChange={setRange} />
      </div>

      <div className="mt-1 flex justify-between text-caption text-faint">
        <span>{formatSalaryAmount(SALARY_SLIDER_MIN, currency)}</span>
        <span>{formatSalaryAmount(SALARY_SLIDER_MAX, currency)}+</span>
      </div>
      <StepFooter {...nav} onNext={() => onNext(salaryRangePatch({ min, max }, currency))} />
    </div>
  );
}

const SPONSORSHIP_OPTIONS = [
  { value: "yes", label: "Yes, I need sponsorship" },
  { value: "no", label: "No, I don't need sponsorship" },
];

function SponsorshipStep({ data, onNext, ...nav }: StepProps) {
  const initial = data.sponsorshipModel || data.sponsorshipDiscardDescription ? "yes" : null;
  const [selected, setSelected] = useState<string | null>(initial);
  return (
    <div>
      <StepHeading>Do you need visa or work sponsorship?</StepHeading>
      <ChipGroup label="Sponsorship" options={SPONSORSHIP_OPTIONS} value={selected} onChange={setSelected} />
      <StepFooter {...nav} nextDisabled={!selected} onNext={() => onNext(selected === "yes" ? SPONSORSHIP_YES_PATCH : {})} />
    </div>
  );
}

const DISQUALIFIER_PLACEHOLDERS = ["e.g. Requires on-call rotation", "e.g. Requires relocation", "e.g. Gambling industry"];
const MIN_DISQUALIFIER_INPUTS = 3;

function DisqualifiersStep({ data, onNext, ...nav }: StepProps) {
  const existing = simpleDisqualifierDescriptions(data.disqualifiers);
  const [inputs, setInputs] = useState<string[]>(
    existing.length >= MIN_DISQUALIFIER_INPUTS
      ? existing
      : [...existing, ...Array(MIN_DISQUALIFIER_INPUTS - existing.length).fill("")],
  );

  function updateInput(i: number, value: string) {
    setInputs(prev => prev.map((v, idx) => (idx === i ? value : v)));
  }

  return (
    <div>
      <StepHeading hint="Optional — anything that would end the conversation immediately.">
        Anything that would make you say no right away?
      </StepHeading>
      <div className="space-y-2">
        {inputs.map((value, i) => (
          <input
            key={i}
            className={INPUT}
            placeholder={DISQUALIFIER_PLACEHOLDERS[i % DISQUALIFIER_PLACEHOLDERS.length]}
            value={value}
            onChange={e => updateInput(i, e.target.value)}
          />
        ))}
      </div>
      <button
        type="button"
        onClick={() => setInputs(prev => [...prev, ""])}
        className="mt-2 text-note font-[650] text-ember transition-colors hover:text-ember-hi focus-ring rounded-ctl"
      >
        + Add another
      </button>
      <StepFooter {...nav} canSkip onNext={() => onNext({
        disqualifiers: applyDisqualifierAnswer(data.disqualifiers, inputs.map(sanitizeCriteriaInput)),
      })} />
    </div>
  );
}

type StepId = "titles" | "experience" | "skills" | "employment" | "location" | "salary" | "sponsorship" | "disqualifiers";

const STEP_COMPONENTS: Record<StepId, ComponentType<StepProps>> = {
  titles: TitlesStep, experience: ExperienceStep, skills: SkillsStep, employment: EmploymentStep,
  location: LocationStep, salary: SalaryStep, sponsorship: SponsorshipStep, disqualifiers: DisqualifiersStep,
};

export function CriteriaWizard({ tier, onSaved }: { tier: string; onSaved: () => void }) {
  const { data: profile, loading: loadingProfile } = useProfile();
  const { execute } = useUpdateProfile();
  // Reflects whatever's already saved rather than always starting blank — re-entering the
  // wizard (or a returning user who filled some of this in via the full editor) sees their
  // real answers pre-filled, not a fresh start. Only Tier2 needs Target job titles: it drives
  // automatic discovery, not fit-scoring, and doesn't apply to Tier1 at all.
  const [data, setData] = useSyncedState(profile, EMPTY, p => parseJobCriteriaYaml(p.jobCriteria));
  const [stepIndex, setStepIndex] = useState(0);
  const [saveError, setSaveError] = useState<string | null>(null);

  const steps: StepId[] = tier === "Tier2"
    ? ["titles", "experience", "skills", "employment", "location", "salary", "sponsorship", "disqualifiers"]
    : ["experience", "skills", "employment", "location", "salary", "sponsorship", "disqualifiers"];

  // The one commit path for every step, in every direction. patch is {} for Skip and Back —
  // neither can ever overwrite an existing value, since nothing is merged in unless a step's own
  // Next handler explicitly builds a patch from what the user answered. Saves via the same
  // PUT /profile the full editor uses, on every Next/Back/Skip — so abandoning the wizard partway
  // still keeps everything answered up to that point, with no new backend endpoint required.
  async function commit(patch: CriteriaPatch, direction: 1 | -1) {
    const next = { ...data, ...patch };
    setData(next);
    setSaveError(null);
    try {
      await execute({ jobCriteria: serializeJobCriteriaYaml(next) });
    } catch {
      setSaveError("Couldn't save — check your connection and try again.");
      return;
    }
    if (direction === -1) {
      setStepIndex(i => Math.max(0, i - 1));
    } else if (stepIndex === steps.length - 1) {
      onSaved();
    } else {
      setStepIndex(i => i + 1);
    }
  }

  if (loadingProfile) {
    return <div className="py-12 text-center text-note text-faint">Loading…</div>;
  }

  // stepIndex is clamped to [0, steps.length - 1] by commit() above, and stepId is always a
  // StepId so STEP_COMPONENTS (a Record<StepId, ...>) is a fully typed, closed lookup.
  // eslint-disable-next-line security/detect-object-injection
  const stepId = steps[stepIndex];
  // eslint-disable-next-line security/detect-object-injection
  const StepComponent = STEP_COMPONENTS[stepId];

  return (
    <Surface padding="lg">
      <p className="mb-4 text-eyebrow text-faint uppercase">
        Question {stepIndex + 1} of {steps.length}
      </p>
      <StepComponent
        data={data}
        onNext={patch => commit(patch, 1)}
        onSkip={() => commit({}, 1)}
        onBack={() => commit({}, -1)}
        isFirst={stepIndex === 0}
        isLast={stepIndex === steps.length - 1}
      />
      {saveError && <p className="mt-3 text-caption text-ember">{saveError}</p>}
    </Surface>
  );
}
