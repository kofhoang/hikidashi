# hikidashi — setup guide

End-to-end setup: identity provider → database → deploy → connect the Claude app. Budget ~45–60 min.

You'll wire three things together. Two values must match exactly across them:

- **Authority** = your WorkOS AuthKit domain, e.g. `https://hikidashi-12345.authkit.app`
- **Audience** = your deployed MCP URL, e.g. `https://<container-endpoint>/mcp`

`hikidashi` reads these as env vars `Auth__Authority` and `Auth__Audience` (the `__` maps to the
`Auth:Authority` / `Auth:Audience` config keys), plus `DATABASE_URL`.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (only if running/testing locally)
- [Docker](https://docs.docker.com/get-docker/) (local Postgres, and building the image)
- A [Scaleway](https://console.scaleway.com) account
- A [WorkOS](https://dashboard.workos.com) account (free tier is fine for one user)
- The Claude **iOS app**

---

## Part A — Run locally first (optional but recommended)

Confirms the build/tests before you touch any cloud.

```sh
git clone git@github.com:kofhoang/hikidashi.git
cd hikidashi
docker compose up -d            # local Postgres on :5432
dotnet test                     # 14 Core tests + 6 Postgres tests (Docker → they run for real)
```

To run the server locally, set `Auth:Authority`/`Auth:Audience` in
`src/Hikidashi.Web/appsettings.Development.json` (after Part B) and `dotnet run` in
`src/Hikidashi.Web`. Note: the `/mcp` endpoint requires a valid token, so the *full* MCP flow is
only testable once the IdP (Part B) and a public URL (Part D) exist — local run is mainly for
build/test and poking the OAuth metadata endpoints.

---

## Part B — Identity provider (WorkOS AuthKit)

WorkOS is the OAuth authorization server: it runs Dynamic Client Registration, the login screen, and
token issuance. hikidashi never sees a password or a secret from it.

1. Create a WorkOS account and a **Project** (one project = one environment; use Production when ready).
2. **Enable AuthKit.** In the dashboard, turn on AuthKit (the hosted auth UI).
3. **Find your AuthKit Domain** — on the AuthKit/Connect configuration page it looks like
   `https://hikidashi-12345.authkit.app`. **This is your `Auth__Authority`.** Save it.
4. **Enable client registration for MCP.** Under **Connect → Configuration**, enable **Client ID
   Metadata Documents (CIMD)** and also **Dynamic Client Registration (DCR)**. Enable *both* — newer
   MCP clients use CIMD, older ones use DCR; you want whichever the Claude app does.
5. **Add Google sign-in.** Under the authentication/social-connection settings, enable **Google
   OAuth** (AuthKit walks you through Google credentials, or uses WorkOS-managed ones). This is how
   you'll log in.
6. **Resource Indicator** — you'll add your MCP URL here in **Part D step 6**, once you know it. (It
   must equal `Auth__Audience`.)

Reference: [WorkOS — MCP / AuthKit](https://workos.com/docs/authkit/mcp).

---

## Part C — Database (Scaleway Serverless SQL)

1. Scaleway console → **Serverless SQL Databases** → **Create database**. Pick an **EU region**
   (e.g. `fr-par`), name it `hikidashi`. Note the **hostname** and **database name**.
2. Create the credentials it authenticates with — an **IAM application + API key**:
   - **IAM → Applications → Create application** (e.g. `hikidashi-app`).
   - Give it a policy allowing access to Serverless SQL (the `ServerlessSQLDatabaseReadWrite` /
     database permission set) in your project.
   - **Create an API key** for that application. Copy the **Access key** (the username) and the
     **Secret key** (the password) — the secret is shown once.
3. Build the **`DATABASE_URL`** in **Npgsql key-value format** (Npgsql does *not* accept the
   `postgres://...` URI form):

   ```
   Host=<hostname>;Port=5432;Database=hikidashi;Username=<ACCESS_KEY>;Password=<SECRET_KEY>;SSL Mode=VerifyFull
   ```

   `SSL Mode` is required by Scaleway. If you hit a certificate error, use
   `SSL Mode=Require;Trust Server Certificate=true` instead.

> No extensions or manual tables needed — hikidashi creates its `facts` schema on first start.

Reference: [Scaleway — connect to Serverless SQL](https://www.scaleway.com/en/docs/serverless-sql-databases/how-to/connect-to-a-database/).

---

## Part D — Build, push, and deploy the container (Scaleway)

### 1. Build the image
```sh
docker build -t hikidashi .
```

### 2. Push it to Scaleway Container Registry
Console → **Container Registry → Create namespace** (e.g. `hikidashi`, region `fr-par`). Then:
```sh
docker login rg.fr-par.scw.cloud -u nologin -p <YOUR_SCW_SECRET_KEY>
docker tag hikidashi rg.fr-par.scw.cloud/hikidashi/hikidashi:latest
docker push rg.fr-par.scw.cloud/hikidashi/hikidashi:latest
```
(Use the same region for registry, DB, and container.)

### 3. Create the Serverless Container
Console → **Serverless Containers → Create namespace**, then **Deploy container** from the registry
image you just pushed. Settings:
- **Port:** `8080` (matches the Dockerfile).
- **Scaling:** min 0 (scale to zero) is fine.
- **Resources:** the smallest tier is plenty.

### 4. Set environment variables and secrets
On the container's **deployment** settings:
- **Secret** — `DATABASE_URL` = the connection string from Part C.
- **Environment variable** — `Auth__Authority` = your AuthKit domain (Part B step 3).
- **Environment variable** — `Auth__Audience` = leave as a placeholder for now; you'll set it in step 6.

Deploy.

### 5. Get the public URL
After deploy, Scaleway shows the container's **endpoint**, e.g.
`https://hikidashixxxx.functions.fnc.fr-par.scw.cloud`. Your MCP URL is that **+ `/mcp`**:
```
https://hikidashixxxx.functions.fnc.fr-par.scw.cloud/mcp
```

### 6. Close the loop (the two values must match)
- In **Scaleway**, set `Auth__Audience` to that full `/mcp` URL and redeploy.
- In **WorkOS** (Connect → Configuration), add the same `/mcp` URL as a **Resource Indicator**.

Now: hikidashi advertises `Audience` = that URL, WorkOS stamps the same value into tokens, and JWT
validation matches. If these three don't agree exactly, auth fails with 401.

Reference: [Scaleway — deploy a container](https://www.scaleway.com/en/docs/serverless-containers/how-to/manage-a-container/).

---

## Part E — Connect the Claude iOS app

1. Claude app → **Settings → Connectors → Add custom connector** (label may read "Add connector" /
   "Developer").
2. Enter your MCP URL: `https://<endpoint>/mcp`.
3. The app discovers WorkOS, registers itself, and opens a login webview → **Sign in with Google**.
4. Done — the connector shows hikidashi's tools.

Test it: ask Claude *"remember that we keep birthday cards in the hallway drawer"* (it calls
`add_fact`), then in a new message *"where are the birthday cards?"* (it calls `search_facts`).

---

## Part F — Verify & troubleshoot

| Symptom | Likely cause / fix |
|---|---|
| Connector can't authenticate / no login appears | DCR **and** CIMD not enabled in WorkOS (Part B step 4). |
| `401` after logging in | `Auth__Audience`, the WorkOS Resource Indicator, and the actual `/mcp` URL don't match exactly (Part D step 6). |
| First request hangs ~5–10s | Cold start (container + DB from zero) — expected; retry. |
| DB connection / certificate error | Check `DATABASE_URL`; try `SSL Mode=Require;Trust Server Certificate=true`. |
| Errors mentioning prepared statements / `DISCARD ALL` | Scaleway's pooler — append `;No Reset On Close=true;Max Auto Prepare=0` to `DATABASE_URL`. |
| Tokens rejected after a while | Confirm the IAM API key (DB password) hasn't been rotated/revoked. |

> ⚠️ The Claude-iOS ↔ WorkOS handshake (DCR/CIMD + token flow) is the one path not yet exercised
> end-to-end. If Part E fails, the WorkOS dashboard's logs and the container logs (Scaleway →
> Cockpit/logs) will show which leg broke — share those and they're quick to fix.

---

## Configuration reference

| Env var | Value | Secret? |
|---|---|---|
| `DATABASE_URL` | Npgsql connection string (Part C) | yes |
| `Auth__Authority` | WorkOS AuthKit domain | no |
| `Auth__Audience` | your `https://<endpoint>/mcp` URL | no |
