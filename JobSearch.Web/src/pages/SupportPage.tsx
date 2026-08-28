import { useState } from "react";
import { useSupportMessage } from "../hooks/useSupport";
import { useMe } from "../hooks/useAuth";
import { PageHeader, Surface, Select, Textarea, Button, Callout } from "../ui";

// Matches the prototype's "What is this about?" <select> exactly (#s22). The backend's
// SupportMessageRequest is message-only (see Program.cs's POST /support) — rather than widen
// that contract, the chosen topic is folded into the message text client-side, same principle
// as any other free-text field that carries structured context inline.
const TOPICS = ["Something is not working", "Billing or credits", "Account and privacy", "Feedback or a feature idea"];

export function SupportPage() {
  const [topic, setTopic] = useState(TOPICS[0]);
  const [message, setMessage] = useState("");
  const [sent, setSent] = useState(false);
  const { data: me } = useMe();
  const { execute, loading, error } = useSupportMessage();

  async function handleSubmit() {
    await execute(`Topic: ${topic}\n\n${message}`);
    setMessage("");
    setSent(true);
  }

  return (
    <div className="max-w-[520px] space-y-6">
      <PageHeader
        title="Contact support"
        tagline={`Tell us what happened and we will reply to ${me?.email ?? "your email"}, usually the same day.`}
      />

      <Surface padding="lg">
        <div className="space-y-4">
          <Select label="What is this about?" value={topic} onChange={e => setTopic(e.target.value)}>
            {TOPICS.map(t => (
              <option key={t} value={t}>{t}</option>
            ))}
          </Select>
          <Textarea
            label="Message"
            value={message}
            onChange={e => { setMessage(e.target.value); setSent(false); }}
            rows={5}
            placeholder="The more detail the better. Include what you expected and what happened instead."
          />
        </div>

        <div className="mt-4 flex items-center justify-between gap-3">
          <span className="text-note text-faint">Replies go to {me?.email ?? "your email"}</span>
          <Button onClick={handleSubmit} disabled={message.trim().length === 0 || loading}>
            {loading ? "Sending…" : "Send message"}
          </Button>
        </div>
        {sent && <p className="mt-2 text-body text-pos">Sent. We'll get back to you.</p>}
      </Surface>

      {error && <Callout variant="danger" title={error} />}
    </div>
  );
}
