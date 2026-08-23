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
  SALARY_BUCKETS, salaryBucketPatch, nearestSalaryBucket,
  applySkillDimensionAnswer,
  SPONSORSHIP_YES_PATCH,
  disqualifiersToTextareaValue, applyDisqualifierAnswer,
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
      <StepFooter {...nav} onNext={() => onNext({ targetJobTitles: titles })} />
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
        skillDimensions: applySkillDimensionAnswer(data.skillDimensions, { name, strongMatch, goodMatch }),
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

function SalaryStep({ data, onNext, ...nav }: StepProps) {
  const [selected, setSelected] = useState<string | null>(nearestSalaryBucket(data));
  const currency = data.currency || "AUD";
  return (
    <div>
      <StepHeading>What salary are you targeting? <span className="font-normal text-gray-400">(in {currency})</span></StepHeading>
      <ChoiceButtons
        options={SALARY_BUCKETS.map(b => ({ value: b.id, label: b.label }))}
        value={selected}
        onChange={setSelected}
      />
      <StepFooter {...nav} onNext={() => onNext(selected ? salaryBucketPatch(selected, currency) : {})} />
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

function DisqualifiersStep({ data, onNext, ...nav }: StepProps) {
  const [text, setText] = useState(disqualifiersToTextareaValue(data.disqualifiers));
  return (
    <div>
      <StepHeading hint='One per line, optional — e.g. "Requires on-call rotation", "Requires relocation". Anything that would end the conversation immediately.'>
        Anything that would make you say no right away?
      </StepHeading>
      <textarea className={`${INPUT} font-mono`} rows={5} value={text} onChange={e => setText(e.target.value)} />
      <StepFooter {...nav} onNext={() => onNext({ disqualifiers: applyDisqualifierAnswer(data.disqualifiers, text) })} />
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
