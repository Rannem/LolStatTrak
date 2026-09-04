# LolStatTrak

ARAM custom-game companion for a friend group: Discord login, "clubs" with
invite/approval-based membership, a random team + champion roller (respecting
each club's champion ban list), live lobby updates via SignalR, and automatic
post-game stat tracking via the Riot API.

## Stack
- **Frontend**: Angular (standalone components/signals) — `frontend/`
- **Backend**: .NET 10 Web API + SignalR hub — `backend/`
- **Database**: Postgres, schema managed by FluentMigrator (`backend/Infrastructure/Migrations`)
- **Reverse proxy**: Caddy, serving the Angular build and proxying `/api/*` + `/hubs/*`
  to the backend over Railway's private network
- **Hosting**: Railway — two services (Caddy+frontend, backend) + managed Postgres

## Local development

### Backend
```
cd backend
dotnet user-secrets init --project Api
dotnet user-secrets set "DATABASE_URL" "postgres://lolstattrak:lolstattrak@localhost:5432/lolstattrak" --project Api
dotnet user-secrets set "JWT_SIGNING_KEY" "some-long-local-dev-secret" --project Api
dotnet user-secrets set "DISCORD_CLIENT_ID" "..." --project Api
dotnet user-secrets set "DISCORD_CLIENT_SECRET" "..." --project Api
dotnet user-secrets set "RIOT_API_KEY" "..." --project Api
dotnet run --project Api
```
Migrations run automatically on startup.

### Frontend
```
cd frontend
npm install
npm start   # proxies /api and /hubs to http://localhost:5000, see proxy.conf.json
```

### Full stack via Docker Compose (Postgres + backend + Caddy/frontend)
```
docker compose -f deploy/docker-compose.yml up --build
```
Open http://localhost:8080.

## Riot API key
Register the app on the Riot Developer Portal for a **Personal API key**
(describe it as a private-community ARAM stat tracker for a fixed friend
group). Personal keys don't expire daily like the default dev key and are
explicitly allowed for this use case — no rotation mechanism is needed.
A Production key is only relevant if the app is ever opened to the public.

## Deploying to Railway (minimal setup)

1. **Add a Postgres plugin** to the project (Railway → New → Database → PostgreSQL).
   Railway auto-injects `DATABASE_URL` on that service — you don't need to build a
   connection string by hand.
2. **Create the `backend` service** from `deploy/Dockerfile.backend`. Set these variables
   (use Railway's variable-reference picker, `${{ServiceName.VAR}}`, wherever possible so
   values stay in sync automatically instead of being copy-pasted):

   | Variable | Value |
   |---|---|
   | `DATABASE_URL` | reference → `${{Postgres.DATABASE_URL}}` |
   | `JWT_SIGNING_KEY` | any long random string (generate once, e.g. `openssl rand -base64 48`) |
   | `DISCORD_CLIENT_ID` | from the Discord Developer Portal |
   | `DISCORD_CLIENT_SECRET` | from the Discord Developer Portal |
   | `RIOT_API_KEY` | your Riot **Personal** API key |
   | `CORS_ALLOWED_ORIGINS` | reference → `${{frontend.RAILWAY_PUBLIC_DOMAIN}}` (prefixed with `https://`) |
   | `GLOBAL_ADMIN_DISCORD_IDS` | comma-separated Discord user IDs that get **global admin** (see below) |
   | `RIOT_REGION` | optional, regional routing for account lookups (default `europe`; `americas`/`asia`/`sea`) |

3. **Create the `frontend` service** from `deploy/Dockerfile.frontend`. Set:

   | Variable | Value |
   |---|---|
   | `BACKEND_INTERNAL_URL` | reference → `${{backend.RAILWAY_PRIVATE_DOMAIN}}:8080` |

   `PORT` is set automatically by Railway — no action needed.
4. **Expose only the `frontend` service publicly** (Settings → Networking → Generate
   Domain). Leave the `backend` service private — Caddy is the only public entrypoint.
5. In the **Discord Developer Portal**, set the OAuth2 redirect URI to
   `https://<your-frontend-domain>/api/signin-discord`.

That's it — no manual connection strings, and the two Railway variable references above
mean the frontend/backend/CORS URLs stay correct automatically if a domain ever changes.

### Performance notes

- The frontend ships a **service worker** (`frontend/ngsw-config.json`) that caches the
  app shell, Data Dragon champion art and Discord avatars. After a deploy, users already
  on the site keep the previous build until they accept the "new version ready → Reload"
  toast (or reopen the tab). Caddy serves `index.html`, `ngsw.json` and `ngsw-worker.js`
  with `no-cache`; every hashed asset is `immutable` for a year.
- SignalR goes straight to WebSockets (`skipNegotiation`) and falls back to a negotiated
  connection only if the socket can't be opened. If DevTools shows repeated `POST
  /hubs/lobby?id=…` requests, something in front of Caddy is stripping the `Upgrade`
  header.
- The club page loads from a single `GET /api/clubs/{id}/overview` call and is prefetched
  when you hover a club card; `/auth/me` is cached client-side for 60 s so route changes
  don't wait on the network.

## Access control & roles

**Sign-up approval.** Anyone can log in with Discord, but new accounts start as
`Pending` and only see a "waiting for approval" screen. A global admin approves or
rejects them at `/admin`. Approval takes effect on the user's next page load (no
re-login needed). Existing users at the time of the migration are auto-approved.

**Global admins** are configured via `GLOBAL_ADMIN_DISCORD_IDS` (Discord → User Settings →
Advanced → enable Developer Mode → right-click your name → *Copy User ID*). The flag is
re-synced on every login, so adding/removing IDs and redeploying is all that's needed.
Global admins are auto-approved, bypass all club-level permission checks, and get the
`/admin` page: pending sign-ups, all users, all clubs (with delete), and the global audit log.

**Club roles** (per club):

| Action | Member | Mod | Admin | Owner |
|---|---|---|---|---|
| Join lobbies, roll, view stats | ✓ | ✓ | ✓ | ✓ |
| Approve join requests, manage bans | | ✓ | ✓ | ✓ |
| Delete lobbies & recorded matches | | | ✓ | ✓ |
| Kick members, promote/demote Member↔Mod | | | ✓ | ✓ |
| Promote to Admin, view audit log | | | ✓* | ✓ |
| Delete the club | | | | ✓ |

\* Admins can view the audit log; only the owner can grant the Admin role.

Every moderation action (approvals, kicks, role changes, ban edits, lobby/match deletions,
club deletion) is written to `audit_log` and visible in the club's **Audit** tab.

**Riot account linking** is done by each user at `/profile` (enter `GameName#TAG`); it's
resolved to a PUUID via account-v1 and required for that player's match stats to be
tracked. Players without a linked account show a red `no riot id` badge in lobbies.

## Game modes

When starting a lobby you pick a mode, which decides what the randomizer does:

| Mode | What we roll | Notes |
|---|---|---|
| **ARAM** | teams + champions | Howling Abyss custom with blind pick; champions respect the club ban list |
| **ARAM Mayhem** | teams only | The client forces all-random for Mayhem, so champion rolling is disabled |
| **Summoner's Rift 5v5** | teams (+ champions if ticked) | Untick "Also roll champions" for a normal draft/blind |

## How games are found & stats gathered

There is no push from Riot for custom games, so after the game someone in the lobby hits
**Mark as played**. The backend then:

1. Takes every lobby player with a **linked Riot account** (PUUID). Unlinked players are
   invisible to the lookup — they can still play, but they won't get stats.
2. Pulls each linked player's 5 most recent match ids (match-v5) and fetches those matches.
3. Accepts the first match that
   - ended **after the lobby was created**,
   - isn't already recorded, and
   - contains **enough of the lobby's linked players**: at least 2 and at least half of them
     (with only one linked player, the match must additionally be a custom game, `queueId 0`).
4. Stores the match (queue, Riot game mode, duration, raw payload) and a stat line per
   linked participant (champion, team, K/D/A, win). The lobby's team roll is *not* used for
   stats — what actually happened in the Riot match is.

Requirements/caveats:

- **At least 2 lobby players should have linked Riot accounts** for reliable matching
  (more = safer). One linked player works only for custom games.
- **Riot needs a few minutes** after the game ends before it appears in match history. If
  "Mark as played" reports nothing found, use **Sync stats** on the lobby to retry.
- **Custom games must be recorded by Riot** — Riot only saves customs to match history when
  there are enough human players (their documented threshold is 5+ humans; bot-filled or
  tiny customs are dropped and can never be fetched).
- **All players must be on the same regional routing** as `RIOT_REGION` (default `europe`).
- The Riot Personal key has tight rate limits (20/s, 100/2min); one sync uses roughly
  `linked players + candidate matches` requests, which is fine for a friend group.

