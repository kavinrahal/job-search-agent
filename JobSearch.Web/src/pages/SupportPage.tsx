import { useState } from "react";
import { useSupportMessage } from "../hooks/useSupport";
import { PageHeader, Surface, Textarea, Button, Callout } from "../ui";

export function SupportPage() {
  const [message, setMessage] = useState("");
  const [sent, setSent] = useState(false);
  const { execute, loading, error } = useSupportMessage();

  async function handleSubmit() {
    await execute(message);
    setMessage("");
    setSent(true);
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Support" tagline="Something broken, confusing, or just want to say hi? We're listening." />

      <Surface padding="lg">
        <Textarea
          label="Describe the issue or question"
          value={message}
          onChange={e => { setMessage(e.target.value); setSent(false); }}
          rows={6}
        />
        <div className="mt-4 flex items-center gap-3">
          <Button onClick={handleSubmit} disabled={message.trim().length === 0 || loading}>
            {loading ? "Sending…" : "Send"}
          </Button>
          {sent && <span className="text-body text-pos">Sent. We'll get back to you.</span>}
        </div>
      </Surface>

      {error && <Callout variant="danger" title={error} />}
    </div>
  );
}
