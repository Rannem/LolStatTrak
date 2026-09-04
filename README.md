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
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Database=lolstattrak;Username=lolstattrak;Password=lolstattrak" --project Api
dotnet user-secrets set "Jwt:SigningKey" "some-long-local-dev-secret" --project Api
dotnet user-secrets set "Discord:ClientId" "..." --project Api
dotnet user-secrets set "Discord:ClientSecret" "..." --project Api
dotnet user-secrets set "RiotApi:ApiKey" "..." --project Api
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

## Deploying to Railway
1. Create a Postgres database service (managed by Railway).
2. Create a `backend` service from `deploy/Dockerfile.backend`, with env vars:
   `ConnectionStrings__Postgres`, `Jwt__SigningKey`, `Discord__ClientId`,
   `Discord__ClientSecret`, `RiotApi__ApiKey`, `Cors__AllowedOrigins__0`.
3. Create a `frontend` service from `deploy/Dockerfile.frontend`, with env vars:
   `BACKEND_INTERNAL_URL` set to the backend service's private Railway hostname
   (e.g. `backend.railway.internal:8080`), and `PORT` (Railway sets this
   automatically).
4. Point your public domain at the `frontend` service only — the backend stays
   on the private network.
