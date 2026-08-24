# Job Search Agent

An always-on, agentic job search management system. Monitors Gmail for job-related emails, discovers and evaluates job postings, generates tailored CVs and cover letters, and surfaces everything via a React dashboard.

## Architecture

```
Gmail API ──────────────────────┐
                                ▼
                       JobSearchAgent (C# worker)
                       ├── Email classification (Claude)
                       ├── Job discovery & evaluation (Claude)
                       └── PostgreSQL (shared with API)

                       JobSearch.Api (ASP.NET Core)
                       ├── GET /api/v1/... — dashboard data
                       ├── POST /api/v1/cv — tailored CV → PDF
                       ├── POST /api/v1/letter — cover letter
                       ├── POST /api/v1/answer — conversational application Q&A
                       └── POST /api/v1/threads/{id}/edit — revise a previous CV, letter, or answer

                       JobSearch.Web (React SPA, nginx)
                       └── separate deployment, calls JobSearch.Api cross-origin
```

JobSearchAgent and JobSearch.Api share the same PostgreSQL database and the same `skills/`
directory. JobSearch.Web is deployed independently (its own Dockerfile.web/nginx), not
bundled into the API — set `VITE_API_URL` at build time to the API's URL, and `CORS_ORIGINS`
on the API to the frontend's URL.

---

## Features

### Email pipeline
- Fetches and classifies job-related emails from Gmail using Claude
- Categories: application confirmed, interview invite, recruiter outreach, scheduling request, offer, rejection, action needed, not relevant
- Tracks application lifecycle — applied, screening, interviewing, offer, rejected, etc.

### Job posting evaluation
- Discovered and alert-sourced postings are evaluated automatically; a URL, or pasted posting text, can also be submitted directly from the dashboard
- Claude evaluates against configurable criteria defined in `skills/context/job_criteria.yaml` — location, stack, salary, experience level, company type, and hard disqualifiers
- Output: structured breakdown with recommendation (strong/good/weak match or discard) and orange flags

### CV generation
- Starts from a full base CV (`skills/context/cv_base.md`) and makes only targeted keyword/phrase additions for the specific role — preserving all existing content rather than regenerating from scratch
- Writes a fresh role-specific summary for every application
- Outputs as a formatted PDF via QuestPDF
- Tailoring rules (which sections to include, condense, or omit per role type) are defined in `skills/tailor_cv.md`

### Cover letter generation
- Tailored per role using candidate anchors and narrative guidelines from `skills/context/background.yaml`
- Writing rules (tone, structure, length, banned phrases) are defined in `skills/write_cover_letter.md`

### Conversational application Q&A and revisions
- Answers free-text application questions ("What made you want to apply for this role?") in the candidate's voice — grounded in `skills/context/background.yaml`, never generic. If there isn't enough context to answer honestly, it asks one clarifying question back instead of guessing, and the conversation continues in the dashboard.
- A revision request against any CV, cover letter, or answer regenerates it with that feedback applied rather than starting over.
- Both are backed by `AgentThread` (`JobSearch.Data/AgentThread.cs`), which tracks a conversation's turn history by its own id, so a follow-up request always addresses the right thread.
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
| Notifications | Email (SendGrid) |

---

## Project structure

```
job-search/
├── skills/                   # Shared agent skill files (used by API + worker)
│   └── context/              # Candidate data, CV base, job criteria
├── JobSearch.Api/            # ASP.NET Core API + React SPA host
│   ├── Program.cs            # All routes
│   └── Services/
│       └── PdfRenderer.cs    # Markdown → PDF via QuestPDF
├── JobSearch.Data/           # Shared data layer (EF Core, agents)
│   ├── AppDbContext.cs
│   ├── CvTailorAgent.cs
│   ├── CoverLetterAgent.cs
│   ├── AnswerAgent.cs        # Conversational application Q&A
│   ├── AgentThread.cs        # Tracks multi-turn Q&A / edit thread history by thread id
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
| `ALLOWED_EMAIL` | Google account seeded as the owner's permanent Tier 1 + Tier 2 account at startup (sign-in itself is open to any Google account, which creates its own `Users` row) |
| `GMAIL_CLIENT_ID` | Gmail API OAuth client ID (worker only) |
| `GMAIL_CLIENT_SECRET` | Gmail API OAuth client secret (worker only) |
| `GMAIL_REFRESH_TOKEN` | One-time bridge only (worker only) — read once on first run to migrate the owner's refresh token into encrypted `UserSecrets` storage, then ignored on every run after. Safe to unset once you see "Migrated GMAIL_REFRESH_TOKEN..." in the worker's startup log. |

---

## Local development

### Prerequisites
- .NET 10 SDK
- Node.js 18+
- PostgreSQL (or a Railway dev database)

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

---

## Security

- Google OAuth restricts dashboard access to a single configured email address
- Public webhooks (SendGrid inbound, Sentry) are verified with a shared secret on every request
- `__Host-` cookie prefix with `HttpOnly`, `Secure`, `SameSite=Strict` in production
- HSTS, CSP, and standard security headers on all responses
- No secrets in source — all via environment variables or `dotnet user-secrets`
