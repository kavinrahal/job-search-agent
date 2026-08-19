import { OnboardingShell } from "../../components/OnboardingShell";
import { ResumeIntakePage } from "../ResumeIntakePage";

export function OnboardingCvPage({ tier }: { tier: string }) {
  return (
    <OnboardingShell
      step={1}
      tier={tier}
      title="Let's build your foundation"
      blurb="Every CV, cover letter, and answer we generate starts from what you tell us here. The more complete this is, the better everything downstream turns out. Upload your resume and we'll do the structuring for you."
    >
      <ResumeIntakePage hideHeader />
    </OnboardingShell>
  );
}
