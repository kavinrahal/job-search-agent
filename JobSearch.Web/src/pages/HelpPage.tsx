import { PageHeader, Surface, Button } from "../ui";
import { IconTile } from "../ui/IconTile";

const FAQS: { question: string; answer: string; icon: React.ReactNode }[] = [
  {
    question: "When does it run?",
    answer: "Every night. Anything matching your criteria is waiting on Today by the morning.",
    icon: (
      <>
        <circle cx="12" cy="12" r="9" />
        <path d="M12 8v4l3 2" />
      </>
    ),
  },
  {
    question: "What uses a credit?",
    answer: "One credit per CV, cover letter, answer, or revision. Discovery and tracking are free.",
    icon: <path d="M12 2v20M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6" />,
  },
  {
    question: "Do you read my email?",
    answer: "Only if you pick full inbox access, and only read only. Filter and manual modes never touch it.",
    icon: <path d="M4 4h16v16H4zM4 7l8 6 8-6" />,
  },
  {
    question: "Can it invent things?",
    answer: "Everything is written from your background. Anything we cannot trace back gets flagged before you send it.",
    icon: <path d="M12 9v4M12 17h.01M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z" />,
  },
];

export function HelpPage({ hideHeader = false }: { hideHeader?: boolean } = {}) {
  return (
    <div className="max-w-[640px] space-y-6">
      {!hideHeader && (
        <PageHeader
          title="How it works"
          tagline="Short answers to what people ask most. If yours is not here, the support form goes straight to a real inbox."
        />
      )}

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {FAQS.map(faq => (
          <Surface key={faq.question} padding="md">
            <IconTile>{faq.icon}</IconTile>
            <div className="mb-1 text-body font-bold text-ink">{faq.question}</div>
            <p className="m-0 text-caption text-muted">{faq.answer}</p>
          </Surface>
        ))}
      </div>

      <Surface padding="md">
        <div className="flex items-center gap-3">
          <div className="flex-1">
            <div className="text-body font-[650] text-ink-2">Still stuck?</div>
            <div className="text-caption text-faint">We answer from a real inbox, usually same day.</div>
          </div>
          <Button href="/support" cap className="flex-none">
            Contact support
          </Button>
        </div>
      </Surface>
    </div>
  );
}
