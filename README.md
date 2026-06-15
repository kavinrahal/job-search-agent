# Job Search Agent

An agentic email assistant that monitors your Gmail inbox, classifies job-search related emails using Claude AI, and surfaces them in a real-time dashboard.

## How it works

```
Gmail API → JobSearchAgent (C# console) → SQLite DB → JobSearch.Api (ASP.NET Core) → JobSearch.Web (React)
```

1. **JobSearchAgent** — fetches emails from Gmail and classifies each one with Claude Haiku (parallel, up to 15 concurrent requests). Results are persisted to a local SQLite database.
2. **JobSearch.Api** — a minimal ASP.NET Core API that reads from the same database and exposes two endpoints for the frontend.
3. **JobSearch.Web** — a React + Tailwind dashboard for filtering and browsing classified emails.

### Email categories

- Application confirmed
- Interview invite
- Recruiter outreach
- Scheduling request
- Offer
- Rejection
- Action needed
- Not relevant

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org)
- A [Google Cloud project](https://console.cloud.google.com/) with the Gmail API enabled and an OAuth 2.0 desktop client (`credentials.json`)
- An [Anthropic API key](https://console.anthropic.com/)

---

## Setup

### 1. Clone and install

```bash
git clone <repo-url>
cd job-search
npm install          # installs concurrently at the root
cd JobSearch.Web
npm install          # installs the React dev dependencies
cd ..
```

### 2. Add your Anthropic API key

```bash
cd JobSearchAgent
dotnet user-secrets set ANTHROPIC_API_KEY sk-ant-...
cd ..
```

### 3. Add your Gmail credentials

Place your `credentials.json` (OAuth 2.0 desktop client downloaded from Google Cloud Console) in the repo root. It is git-ignored and will never be committed.

The first time the agent runs it will open a browser window for Gmail authorisation. The resulting `token.json` is saved next to `credentials.json` and is also git-ignored.

---

## Running

### Start the dashboard

```bash
npm run dev
```

This starts three processes concurrently:

| Label | What it does |
|---|---|
| `[API]` | ASP.NET Core API on `http://localhost:5000` |
| `[WEB]` | Vite dev server on `http://localhost:5173` |
| `[AGENT]` | Fetches and classifies any unclassified emails, then exits |

Open **http://localhost:5173** in your browser.

### Sync new emails

Re-run `npm run dev` (or run the agent on its own) to pull in any emails that have arrived since the last run:

```bash
cd JobSearchAgent
dotnet run
```

### Classify a specific date range

```bash
cd JobSearchAgent
dotnet run -- --from 2025-01-01 --to 2025-03-31
```

Or fetch the last N days:

```bash
dotnet run -- --days 7
```

---

## Project structure

```
job-search/
├── JobSearchAgent/          # Console app — Gmail fetch + Claude classification
│   ├── Agents/              # EmailClassifier (parallel Haiku calls)
│   ├── Data/                # EF Core DbContext + entity models
│   ├── Integrations/        # GmailClient
│   ├── Models/              # RawEmail record
│   ├── Storage/             # EmailRepository
│   └── skills/              # System-prompt markdown files
├── JobSearch.Api/           # ASP.NET Core minimal API
│   └── Program.cs           # GET /api/summary, GET /api/emails
├── JobSearch.Web/           # React + TypeScript + Tailwind v4
│   └── src/
│       ├── components/      # SummaryCards, EmailTable
│       ├── api.ts           # fetch wrappers
│       └── types.ts         # TypeScript interfaces
├── .gitignore
└── package.json             # root dev script (concurrently)
```

---

## Security

- `credentials.json`, `token.json`, `.env`, and `*.db` are all git-ignored.
- The Anthropic API key is stored in `dotnet user-secrets` (never in source).
- The SQLite database lives locally only.
