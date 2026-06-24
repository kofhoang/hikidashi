# hikidashi (引き出し)

A personal knowledge base for durable reference facts — "where we store birthday cards",
"my cleaning checklist". Facts are created, occasionally edited, rarely deleted. Exposed to an
LLM assistant as a **pure MCP server**: you ask in natural language and get stored answers back.

## Architecture

Ports & adapters + vertical slices + functional effects (LanguageExt `Eff<TRt, T>`). Plain CRUD on
a single `facts` table — no event sourcing. **No server-side LLM and no hand-rolled auth**: the
calling model supplies intelligence over MCP, and a managed identity provider handles OAuth.

```
src/Hikidashi.Core    Pure domain: Fact, ports (IFactRepository), Eff handlers
                      (search/get/list/list-unenriched/keywords/add/update/delete), validation.
src/Hikidashi.Data    Postgres adapter (EF Core, Npgsql provider), EF migrations.
src/Hikidashi.Web     Host: MCP server + JWT validation (resource server) + runtime/DI.
tests/                Core unit tests (in-memory) + Data integration tests (Testcontainers).
```

- **Content is stored and returned verbatim** — checklists come back whole, never summarized.
- **Keywords** are generous findability terms supplied by the calling model.
- **Search** is forgiving: substring (`ILIKE`) over keywords *and* content, `any` (OR) by default,
  ranked by how many terms hit, then recency. No `pg_trgm` needed at personal scale.

### Capture & enrichment

`add_fact` normally carries generous keywords from the model. It may also be called with **no
keywords** for a quick capture — the fact is stored verbatim and flagged **un-enriched** (still
findable by content substring search). Later, the model calls `list_unenriched_facts`, reads each
one, and calls `update_fact` with keywords — which flips it enriched.

## MCP tools (`/mcp`, Streamable HTTP)

| Tool | Kind | Purpose |
|---|---|---|
| `search_facts(keywords, match?, limit?)` | read | Forgiving keyword/content search; verbatim content. |
| `list_keywords(prefix?)` | read | Vocabulary discovery — call when a search is weak/empty, then search again. |
| `get_fact(id)` | read | One fact in full (content, keywords, enriched, metadata). |
| `list_facts(limit?, offset?)` | read | Browse, most-recent first. |
| `list_unenriched_facts(limit?)` | read | Quick captures awaiting keywords — enrich via `update_fact`. |
| `add_fact(content, keywords?)` | write | Store verbatim; keywords optional (omit for quick capture). |
| `update_fact(id, content?, keywords?)` | write (idempotent) | Patch; supplying keywords marks it enriched. |
| `delete_fact(id)` | write (destructive) | Permanent delete. |

## Auth

hikidashi is a pure **OAuth resource server**. A managed identity provider — **WorkOS AuthKit** or
**Stytch** (both support MCP Dynamic Client Registration; free tiers cover a single user) — is the
authorization server: it handles the Claude app's self-registration (DCR), the login screen
(including "Sign in with Google"), and token issuance. hikidashi only:

1. **validates** the provider's JWT access tokens on `/mcp` (`AddJwtBearer`, signature via the
   provider's JWKS), and
2. **advertises** OAuth Protected Resource Metadata (via the MCP `AddMcp` scheme) pointing at the
   provider, so the Claude app can discover where to authenticate.

There is no OAuth server code, no DCR endpoint, no signing keys, and **no auth secret** in hikidashi
— only the provider's public issuer URL and this server's resource identifier (audience).

Provider setup (one-time, in the provider dashboard): enable MCP/Dynamic Client Registration, add
Google as a social login, and note the **issuer URL** (`Auth:Authority`). Set this server's resource
identifier as `Auth:Audience` (typically the public `/mcp` URL).

## Run locally

```sh
docker compose up -d                 # Postgres on :5432
cd src/Hikidashi.Web
dotnet run                           # set Auth:Authority / Auth:Audience in appsettings.Development.json
```

The facts schema is applied on startup.

## Configuration

| Setting | Env var | Config key | Notes |
|---|---|---|---|
| Database | `DATABASE_URL` | `ConnectionStrings:DefaultConnection` | required (the only secret) |
| IdP issuer | — | `Auth:Authority` | the provider's issuer URL (public) |
| Resource id | — | `Auth:Audience` | this server's audience, e.g. the public `/mcp` URL (public) |

## Deploy (Scaleway)

- **Serverless Container** from the `Dockerfile` (listens on `:8080`).
- **Serverless SQL Database** (managed Postgres, EU, scales to zero) as `DATABASE_URL`.
- Only `DATABASE_URL` is secret (set it as a container secret env var); `Auth:Authority`/`Auth:Audience`
  are plain config. Secret Manager is not yet integrated with Serverless Containers.
- Cold starts (container + DB from zero) are ~5–10s on the first request after idle — expected.

## Tests

```sh
dotnet test
```

Core tests run in-memory and always execute. Data integration tests use Testcontainers and **skip
automatically when Docker is unavailable** (they exercise the real EF/SQL mapping otherwise).

## Status / things to verify before relying on it

- **Provider + Claude iOS connector (needs a live check).** Pick WorkOS AuthKit or Stytch, enable MCP
  DCR + Google login, and verify the iOS connector-add → discovery → token flow end to end. The
  hikidashi side (JWT validation + resource metadata) is standard, but the provider config and the
  live handshake are unverified here.
- **`Auth:Audience` must match** the audience the provider stamps into access tokens, or JWT
  validation rejects them.
- **Scaleway Serverless SQL connection model.** Verify auth/connection specifics (IAM-token rotation,
  pooling/prepared-statement caveats) and adjust the `NpgsqlDataSource` setup if needed.
- **Data integration tests** were not run here (no Docker in the build environment); run them where
  Docker is available to validate the search SQL against real Postgres.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
