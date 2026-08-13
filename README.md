# QueryLayer

QueryLayer is a Backend-as-a-Service platform: describe the backend you want in plain English (or hand-craft a JSON spec), and QueryLayer generates the Postgres schema, a live REST API, auth, and workflow automation for it — no server code to write.

A project is defined by a single **spec**: entities and fields, endpoints, permissions, triggers, and workflows. QueryLayer turns that spec into real Postgres tables and a fully working, authenticated REST API, and keeps the schema in sync whenever the spec changes.

## Features

- **AI spec generation** — describe your backend in natural language and get back a structured spec (entities, fields, relations, endpoints, permissions). An LLM-backed service builds the prompt, validates the result against the spec schema, and repairs invalid output automatically.
- **Generated REST API** — every entity in a spec becomes CRUD endpoints at `api/{projectKey}/...`, matched and executed at request time against the project's own Postgres tables (no code generation/build step).
- **Schema management** — specs are turned into Postgres DDL, migrated safely as they evolve, and validated for drift between the spec and the live database.
- **Auth & permissions** — JWT-based platform auth, per-project API keys (generate/rotate/revoke), and role-based access control evaluated per request.
- **Workflow & trigger engine** — declarative state machines (`workflow` + `workflow_transition`) and event triggers with conditions and actions (create/update records, send webhooks, delay), processed by a background job worker.
- **Developer experience tools** — auto-generated OpenAPI docs and ready-to-run request examples for every endpoint; an in-app API Explorer and database schema viewer.

## Architecture

```
QueryLayer/
├── backend/QueryLayer.API/     # ASP.NET Core 8 Web API
│   └── QueryLayer.API/
│       ├── Controllers/        # Auth, Platform, Schema, AI, Runtime, Workflow, Keys, DX
│       ├── Services/
│       │   ├── Auth/           # JWT, password hashing, API keys, RBAC
│       │   ├── AI/             # Spec generation, validation, repair
│       │   ├── Runtime/        # Endpoint matching, SQL building, request execution
│       │   ├── Workflow/       # Triggers, conditions, actions, job queue/worker
│       │   └── DX/             # OpenAPI generation, request examples
│       └── Data/                # EF Core DbContext & entities
└── frontend/querylayer-web/    # Next.js 16 (App Router) + React 19 + Tailwind CSS 4
    └── src/
        ├── app/                 # Routes: dashboard, projects, explorer, docs, settings, auth
        ├── components/          # AI spec editor/generator, schema viewer, API explorer, etc.
        ├── lib/                 # Auth & theme context providers
        └── services/            # API client (axios)
```

**How a request flows:** a client calls `api/{projectKey}/{entity}` → `RuntimeController` resolves the project from its key → `EndpointMatcher` finds the matching spec endpoint → `RequestBodyValidator` / `SqlQueryBuilder` validate and build the query → `RuntimeExecutor` runs it against the project's Postgres tables, applying RBAC and (for state-changing routes) the workflow engine.

## Tech stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 8 (C#), Entity Framework Core, Npgsql, Serilog |
| Database | PostgreSQL (Supabase in production) |
| Frontend | Next.js 16, React 19, TypeScript, Tailwind CSS 4, Axios |
| Auth | JWT bearer tokens, BCrypt password hashing, per-project API keys |
| AI | LLM-backed spec generation/repair (OpenAI-compatible API) |
| Testing | xUnit, Moq |

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm
- A PostgreSQL database (local, or a hosted instance such as [Supabase](https://supabase.com/))

### Backend

```bash
cd backend/QueryLayer.API

# Set required environment variables (see table below), then:
dotnet restore
dotnet run --project QueryLayer.API
```

The API listens on the port configured by ASP.NET Core (see `launchSettings.json` for local defaults) and exposes Swagger UI at `/swagger` in Development. On startup it automatically creates the auth, platform, and workflow tables if they don't already exist.

Run the test suite:

```bash
dotnet test
```

#### Environment variables

| Variable | Required | Description |
|---|---|---|
| `DATABASE_URL` | Yes | Postgres connection string. Accepts either a standard Npgsql connection string or a `postgres://user:pass@host:port/db` URI (auto-converted, with SSL settings applied for hosted providers like Supabase/Render). |
| `JWT_SECRET` | Yes | Secret used to sign platform auth JWTs. |
| `OPENAI_API_KEY` | Yes, for AI features | API key used by the AI spec generation/editing endpoints. |

### Frontend

```bash
cd frontend/querylayer-web
npm install
npm run dev
```

Open [http://localhost:3000](http://localhost:3000). The app redirects to `/login` or `/dashboard` depending on auth state. Configure the backend API base URL used by the frontend in `src/services/api.ts`.

## Using QueryLayer

1. **Sign up / log in** to the platform.
2. **Create a project** — gives you a project key and a place to hold your spec.
3. **Describe your backend** in plain English and let AI generate a spec, or write/edit the spec directly.
4. **Save the spec** — QueryLayer creates the Postgres tables and the REST API goes live immediately at `api/{projectKey}/...`.
5. **Explore and test** — use the built-in API Explorer, generated OpenAPI docs, and request examples to call your new API; manage API keys from the project's Keys panel.

### Example: a subscription workflow

Specs can describe stateful entities. For example, a subscription that moves through `active → past_due → retrying_payment → cancelled` in response to payment triggers, with a 24-hour delay before retrying and a webhook fired on cancellation — expressed as `workflow`/`workflow_transition` endpoints, `trigger`s, and `condition`s in the spec (see `Subscription.txt` for a worked example prompt).

## API surface (backend)

| Route prefix | Purpose |
|---|---|
| `POST /auth/signup`, `/auth/login` | Platform user authentication |
| `POST /platform/signup`, `/platform/login`, `/platform/projects*` | Platform/project management |
| `PUT /projects/{id}/spec`, `GET /projects/{id}/schema/tables`, `POST /projects/{id}/spec/preview`, `GET /projects/{id}/schema/validate` | Spec & schema management |
| `POST /projects/{id}/generate-spec`, `/projects/{id}/edit-spec` | AI spec generation & editing |
| `GET /projects/{id}/openapi`, `/projects/{id}/examples` | Generated docs & request examples |
| `GET/POST/PUT/PATCH/DELETE /api/{projectKey}/{**path}` | Generated runtime API for a project |
| `POST /api/{projectKey}/{entity}/{id}/{transition}` | Workflow state transitions |
| `/projects/{id}/keys*` | API key management |
| `GET /api/health` | Health check |

## Deployment

The backend is configured to run behind Render (reading `DATABASE_URL`, resolving Supabase Postgres hosts to IPv4, and enforcing SSL) and can be deployed as a standard containerized/managed .NET service. The frontend is a standard Next.js app deployable to Vercel or any Node host.
