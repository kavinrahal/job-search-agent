# Job Search Agent

An always-on, agentic job search management system. Monitors Gmail for job-related emails, discovers and evaluates job postings, generates tailored CVs and cover letters, and surfaces everything via a Telegram bot and a React dashboard.

## Architecture

```
Gmail API ──────────────────────┐
                                ▼
                       JobSearchAgent (C# worker)
                       ├── Email classification (Claude)
                       ├── Job discovery & evaluation (Claude)
                       └── PostgreSQL (shared with API)

Telegram webhook ──────────────┐
                                ▼
                       JobSearch.Api (ASP.NET Core)
                       ├── GET /api/v1/... — dashboard data
                       ├── POST /api/v1/telegram/webhook
                       │   ├── URL evaluation (Claude)
                       │   ├── /cv — tailored CV → PDF
                       │   ├── /letter — cover letter
                       │   ├── /answer — conversational application Q&A
                       │   └── /edit — revise a previous CV, letter, or answer
                       └── React SPA (served from wwwroot)
```

Both projects share the same PostgreSQL database and the same `skills/` directory.

---

## Features

### Email pipeline
- Fetches and classifies job-related emails from Gmail using Claude
- Categories: application confirmed, interview invite, recruiter outreach, scheduling request, offer, rejection, action needed, not relevant
- Tracks application lifecycle — applied, screening, interviewing, offer, rejected, etc.

### Job posting evaluation
- Send any job URL to the Telegram bot to get a structured evaluation
- Claude evaluates against configurable criteria defined in `skills/context/job_criteria.yaml` — location, stack, salary, experience level, company type, and hard disqualifiers
- Output: structured breakdown with recommendation (strong/good/weak match or discard) and orange flags

### Telegram bot commands
- **Send a URL** — evaluate the posting, get a structured breakdown
- **`/cv <url>`** — generate a tailored CV as a PDF download
- **`/letter <url>`** — generate a tailored cover letter
- **`/answer <question>`** — get a human-sounding answer to an application question, grounded in the candidate's real background
- **`/edit <feedback>`** — reply to a CV, cover letter, or answer to get a revised version
- `/cv`, `/letter`, and `/answer` all work by replying to a job notification for context; `/edit` always works by replying to whatever it should revise

### CV generation
- Starts from a full base CV (`skills/context/cv_base.md`) and makes only targeted keyword/phrase additions for the specific role — preserving all existing content rather than regenerating from scratch
- Writes a fresh role-specific summary for every application
- Outputs as a formatted PDF via QuestPDF
- Tailoring rules (which sections to include, condense, or omit per role type) are defined in `skills/tailor_cv.md`

### Cover letter generation
- Tailored per role using candidate anchors and narrative guidelines from `skills/context/background.yaml`
- Writing rules (tone, structure, length, banned phrases) are defined in `skills/write_cover_letter.md`

### Conversational application Q&A and revisions
- `/answer <question>` answers free-text application questions ("What made you want to apply for this role?") in the candidate's voice — grounded in `skills/context/background.yaml`, never generic. If there isn't enough context to answer honestly, the bot asks one clarifying question back instead of guessing, and the conversation continues over Telegram replies.
- `/edit <feedback>`, sent as a reply to any CV, cover letter, or answer the bot has produced, regenerates it with that feedback applied rather than starting over.
- Both are backed by `AgentThread` (`JobSearch.Data/AgentThread.cs`), which tracks a conversation's turn history against the Telegram message id of the bot's latest reply, so a follow-up reply can always find its way back to the right thread — including replies to a CV, which arrives as a PDF document rather than text.
- Writing rules and the ask-vs-answer decision logic are defined in `skills/answer_application_question.md`.

### React dashboard
- Browse classified emails with filters
- View job discoveries with evaluation breakdowns
- Track application pipeline by status
- Health endpoint for uptime monitoring

---

## Skills system

All agent behaviour is defined in markdown skill files in `skills/`. Both the API and the worker load from this directory at runtime — update one, both pick it up on the next deploy.

```
skills/
├── evaluate_posting.md       # Evaluates job postings against criteria
├── tailor_cv.md              # Adapts base CV for a specific role
├── write_cover_letter.md     # Generates tailored cover letters
├── answer_application_question.md  # Answers/asks about application questions
├── classify_email.md         # Classifies job-related emails
└── context/
    ├── background.yaml       # Candidate data, anchors, narrative guidelines
    ├── cv_base.md            # Full base CV — source of truth for CV generation
    └── job_criteria.yaml     # All evaluation criteria, thresholds, disqualifiers
```

---

## Tech stack

| Layer | Technology |
|---|---|
| Backend API | C#, ASP.NET Core 10, Minimal API |
| Worker | C# console app |
| Frontend | React, TypeScript, Vite, Tailwind CSS |
| AI | Claude (claude-opus-4-8) via Anthropic SDK |
| Database | PostgreSQL (EF Core, Npgsql) |
| PDF generation | QuestPDF |
| Auth | Google OAuth 2.0 + secure cookie sessions |
| Deployment | Railway, Docker |
| Email | Gmail API |
| Notifications | Telegram Bot API (webhook) |

---

## Project structure

```
job-search/
├── skills/                   # Shared agent skill files (used by API + worker)
│   └── context/              # Candidate data, CV base, job criteria
├── JobSearch.Api/            # ASP.NET Core API + React SPA host
│   ├── Program.cs            # All routes + Telegram webhook handler
│   └── Services/
│       ├── TelegramService.cs
│       └── PdfRenderer.cs    # Markdown → PDF via QuestPDF
├── JobSearch.Data/           # Shared data layer (EF Core, agents)
│   ├── AppDbContext.cs
│   ├── CvTailorAgent.cs
│   ├── CoverLetterAgent.cs
│   ├── AnswerAgent.cs        # Conversational application Q&A
│   ├── AgentThread.cs        # Tracks multi-turn Q&A / edit threads by Telegram message id
│   ├── PostingEvaluator.cs
│   ├── SkillLoader.cs        # Loads skill files from skills/ directory
│   └── Migrations/
├── JobSearchAgent/           # Worker — Gmail fetch, classification, discovery
├── JobSearch.Web/            # React frontend source
└── package.json              # Root dev script (concurrently)
```

---

## Environment variables

Set these in Railway (production) or `dotnet user-secrets` / `.env` (local).

| Variable | Description |
|---|---|
| `ANTHROPIC_API_KEY` | Anthropic API key |
| `DATABASE_URL` | PostgreSQL connection string |
| `GOOGLE_CLIENT_ID` | Google OAuth client ID |
| `GOOGLE_CLIENT_SECRET` | Google OAuth client secret |
| `TELEGRAM_BOT_TOKEN` | Telegram bot token from BotFather |
| `TELEGRAM_WEBHOOK_SECRET` | Alphanumeric secret for webhook verification |
| `TELEGRAM_CHAT_ID` | Your Telegram chat ID |
| _(WhatsApp rows below — TODO, pilot paused pending Meta Business Verification; safe to leave unset, everything no-ops until they're set)_ | |
| `WHATSAPP_ACCESS_TOKEN` | WhatsApp Cloud API permanent access token (optional — parallel pilot channel) |
| `WHATSAPP_PHONE_NUMBER_ID` | Meta Phone Number ID (optional) |
| `WHATSAPP_APP_SECRET` | Meta app secret, verifies incoming webhook signatures (optional, API only) |
| `WHATSAPP_WEBHOOK_VERIFY_TOKEN` | Arbitrary string chosen at webhook-subscribe time (optional, API only) |
| `WHATSAPP_TO_NUMBER` | Recipient's WhatsApp number, E.164 format (optional) |
| `WHATSAPP_TEMPLATE_NAME` | Approved template name for proactive alerts (optional, defaults to `job_search_alert`) |
| `WHATSAPP_TEMPLATE_LANG` | Template language code (optional, defaults to `en_US`) |
| `ALLOWED_EMAIL` | Google account seeded as the owner's permanent Tier 1 + Tier 2 account at startup (sign-in itself is open to any Google account, which creates its own `Users` row) |
| `GMAIL_CLIENT_ID` | Gmail API OAuth client ID (worker only) |
| `GMAIL_CLIENT_SECRET` | Gmail API OAuth client secret (worker only) |

---

## Local development

### Prerequisites
- .NET 10 SDK
- Node.js 18+
- PostgreSQL (or a Railway dev database)
- Telegram bot registered via BotFather

### Start everything

```bash
npm install
cd JobSearch.Web && npm install && cd ..
npm run dev
```

This starts three processes:

| Label | What it does |
|---|---|
| `[API]` | ASP.NET Core on `http://localhost:5000` |
| `[WEB]` | Vite dev server on `http://localhost:5173` |
| `[AGENT]` | Runs the Gmail worker once, then exits |

### Register the Telegram webhook

After deploying (or when your Railway URL changes):

```
https://api.telegram.org/bot<TOKEN>/setWebhook?url=https://<your-railway-url>/api/v1/telegram/webhook&secret_token=<WEBHOOK_SECRET>
```

Verify with:

```
https://api.telegram.org/bot<TOKEN>/getWebhookInfo
```

---

## Security

- Google OAuth restricts dashboard access to a single configured email address
- Telegram webhook is verified with a secret token on every request
- `__Host-` cookie prefix with `HttpOnly`, `Secure`, `SameSite=Strict` in production
- HSTS, CSP, and standard security headers on all responses
- No secrets in source — all via environment variables or `dotnet user-secrets`
