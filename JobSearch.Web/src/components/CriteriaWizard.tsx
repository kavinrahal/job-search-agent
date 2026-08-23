import { useEffect, useState, type ComponentType, type ReactNode } from "react";
import { useProfile, useUpdateProfile } from "../hooks/useProfile";
import { parseJobCriteriaYaml, serializeJobCriteriaYaml, type JobCriteriaData } from "../lib/jobCriteriaYaml";
import { COUNTRIES, COUNTRY_TO_CURRENCY } from "../lib/regionData";
import { Field, INPUT } from "./CardEditor";
import { ChoiceButtons } from "./ChoiceButtons";
import { EMPLOYMENT_TYPES } from "./JobCriteriaEditor";
import { CARD, PRIMARY_BUTTON } from "../lib/styles";
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
// onNext(patch) to commit. Skip and Back never carry a patch, so neither can ever overwrite an
// existing real value — the wizard-level commit function only ever merges what onNext explicitly
// hands it.

const GHOST_BUTTON = "text-sm text-gray-400 transition-colors hover:text-gray-600 dark:text-gray-500 dark:hover:text-gray-300";
const EMPTY: JobCriteriaData = parseJobCriteriaYaml("");

interface StepProps {
  data: JobCriteriaData;
  onNext: (patch: CriteriaPatch) => void;
  onSkip: () => void;
  onBack: () => void;
  isFirst: boolean;
  isLast: boolean;
}

function StepFooter({ onBack, onSkip, onNext, isFirst, isLast }: {
  onBack: () => void; onSkip: () => void; onNext: () => void; isFirst: boolean; isLast: boolean;
}) {
  return (
    <div className="mt-6 flex items-center justify-between">
      {isFirst ? <span /> : <button onClick={onBack} className={GHOST_BUTTON}>Back</button>}
      <div className="flex items-center gap-4">
        <button onClick={onSkip} className={GHOST_BUTTON}>Skip</button>
        <button onClick={onNext} className={PRIMARY_BUTTON}>{isLast ? "Finish" : "Next"}</button>
      </div>
    </div>
  );
}

function StepHeading({ children, hint }: { children: ReactNode; hint?: string }) {
  return (
    <div className="mb-4">
      <h2 className="text-lg font-semibold text-gray-700 dark:text-gray-200">{children}</h2>
      {hint && <p className="mt-1 text-sm text-gray-400 dark:text-gray-500">{hint}</p>}
    </div>
  );
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
      <StepFooter {...nav} onNext={() => onNext({ targetJobTitles: sanitizeCriteriaInput(titles) })} />
    </div>
  );
}

function ExperienceStep({ data, onNext, ...nav }: StepProps) {
  const [selected, setSelected] = useState<string | null>(nearestExperienceBucket(data));
  return (
    <div>
      <StepHeading>How much experience do you have?</StepHeading>
      <ChoiceButtons
        options={EXPERIENCE_BUCKETS.map(b => ({ value: b.id, label: b.label }))}
        value={selected}
        onChange={setSelected}
      />
      <StepFooter {...nav} onNext={() => onNext(selected ? experienceBucketPatch(selected) : {})} />
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
        <Field label="Skill or specialization" value={name} onChange={setName} />
        <Field label="Must-haves (comma-separated)" value={strongMatch} onChange={setStrongMatch} />
        <Field label="Nice-to-haves, optional (comma-separated)" value={goodMatch} onChange={setGoodMatch} />
      </div>
      <StepFooter {...nav} onNext={() => onNext({
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
      <ChoiceButtons
        multi
        options={EMPLOYMENT_TYPES.map(t => ({ value: t, label: t.replace("_", " ") }))}
        value={selected}
        onChange={setSelected}
      />
      <StepFooter {...nav} onNext={() => onNext({ employmentTypes: selected })} />
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
    onNext({
      countries: country,
      currency: country ? (COUNTRY_TO_CURRENCY[country] ?? data.currency) : data.currency,
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
      <select className={INPUT} value={country} onChange={e => setCountry(e.target.value)}>
        <option value="">Select a country</option>
        {COUNTRIES.map(c => <option key={c} value={c}>{c}</option>)}
      </select>
      <div className="mt-3">
        <ChoiceButtons multi options={ARRANGEMENT_OPTIONS} value={arrangements} onChange={setArrangements} />
      </div>
      <StepFooter {...nav} onNext={commit} />
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
  "[&::-webkit-slider-thumb]:pointer-events-auto [&::-webkit-slider-thumb]:h-4 [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-violet-600 [&::-webkit-slider-thumb]:shadow " +
  "[&::-moz-range-thumb]:pointer-events-auto [&::-moz-range-thumb]:h-4 [&::-moz-range-thumb]:w-4 [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:border-0 [&::-moz-range-thumb]:bg-violet-600";

function DualRangeSlider({ min, max, step, value, onChange }: {
  min: number; max: number; step: number; value: [number, number]; onChange: (v: [number, number]) => void;
}) {
  const [lo, hi] = value;
  const pctLo = ((lo - min) / (max - min)) * 100;
  const pctHi = ((hi - min) / (max - min)) * 100;
  return (
    <div className="relative h-6">
      <div className="absolute top-1/2 h-1.5 w-full -translate-y-1/2 rounded-full bg-gray-200 dark:bg-gray-700" />
      <div
        className="absolute top-1/2 h-1.5 -translate-y-1/2 rounded-full bg-violet-600"
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
      <p className="flex items-baseline gap-2 text-xl font-semibold text-gray-700 dark:text-gray-200">
        <span>{formatSalaryAmount(min, currency)}</span>
        <span className="text-sm font-normal text-gray-400">to</span>
        <span>{formatSalaryAmount(max, currency)}</span>
      </p>

      <div className="mt-5">
        <DualRangeSlider min={SALARY_SLIDER_MIN} max={SALARY_SLIDER_MAX} step={SALARY_SLIDER_STEP} value={range} onChange={setRange} />
      </div>

      <div className="mt-1 flex justify-between text-xs text-gray-400 dark:text-gray-500">
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
      <ChoiceButtons options={SPONSORSHIP_OPTIONS} value={selected} onChange={setSelected} />
      <StepFooter {...nav} onNext={() => onNext(selected === "yes" ? SPONSORSHIP_YES_PATCH : {})} />
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
        className="mt-2 text-sm font-medium text-violet-600 transition-colors hover:text-violet-700 dark:text-violet-400 dark:hover:text-violet-300"
      >
        + Add another
      </button>
      <StepFooter {...nav} onNext={() => onNext({
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
  const [data, setData] = useState<JobCriteriaData>(EMPTY);
  const [stepIndex, setStepIndex] = useState(0);
  const [saveError, setSaveError] = useState<string | null>(null);

  // Reflects whatever's already saved rather than always starting blank — re-entering the
  // wizard (or a returning user who filled some of this in via the full editor) sees their
  // real answers pre-filled, not a fresh start. Only Tier2 needs Target job titles: it drives
  // automatic discovery, not fit-scoring, and doesn't apply to Tier1 at all.
  useEffect(() => {
    if (profile) setData(parseJobCriteriaYaml(profile.jobCriteria));
  }, [profile]);

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
    return <div className="py-12 text-center text-sm text-gray-400 dark:text-gray-500">Loading…</div>;
  }

  const stepId = steps[stepIndex];
  const StepComponent = STEP_COMPONENTS[stepId];

  return (
    <div className={CARD}>
      <p className="mb-4 text-xs font-medium uppercase tracking-wide text-gray-400 dark:text-gray-500">
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
      {saveError && <p className="mt-3 text-xs text-red-600 dark:text-red-400">{saveError}</p>}
    </div>
  );
}
