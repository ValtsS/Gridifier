# Gridifier

Real-time PSK Reporter station grid locator tracker. Listens to the PSK Reporter MQTT feed, extracts receiver/sender callsigns and Maidenhead grid locators, and serves them via a REST API.

## Architecture

```
MQTT (pskreporter.info:1883)
       │
       ▼
 PskReporterWorker ──► Channel<Station> ──► DatabaseWriter ──► SQLite (WAL, recovery log)
                       │                       │
                       │                       │  guarded upserts (no-op when already persisted)
                       │                       ▼
                       │                 StationSweeper (periodic quiet flush)
                       │
                       ▼
                  StationCache (source of truth, all in RAM)
                       │
                       ▼
               GridEndpoint (GET /api/v1/grid/{band}/{callsign})
```

- **PskReporterWorker** — one per MQTT subscription: connects, subscribes to its topic (default `pskr/filter/v2/+/...` all bands, or per-band like `pskr/filter/v2/20m/#`), parses JSON messages, tracks per-subscription connect/disconnect status
- **PskMessageHandler** — extracts `rc`/`rl` (receiver) and `sc`/`sl` (sender) pairs along with `b` (band), validates, truncates grids to 4 chars, writes to channel
- **DatabaseWriter** — reads from channel, updates `StationCache` (per-band dictionaries, 8-byte entries: grid encoded as `ushort`, last heard as `uint` unix seconds); persists immediately only for new stations or grid changes, in batches
- **StationCache** — source of truth held entirely in RAM; loaded once from SQLite at startup, then served directly by the API with no DB round-trips
- **DatabaseBackup / DatabaseBackupWorker** — periodic SQLite online-backup snapshots (`gridifier.db.bak`, keeping 1 previous generation) plus a checkpoint+snapshot on graceful shutdown; on startup a `PRAGMA quick_check` runs and the DB is restored from the latest snapshot if corrupt (corrupt file preserved as `*.corrupt-<ts>`, never silently destroyed)
- **StationSweeper** — periodic sweep that flushes only *dirty* stations silent for longer than the sweep interval (repeat same-grid reports are not written eagerly; guarded upsert makes already-persisted rows no-ops); chunked transactions keep DB write locks to milliseconds
- **StatsRefresher** — periodically precomputes active-station count and per-band breakdown so the stats endpoint never scans the cache on request
- **GridEndpoint** — minimal-API `GET /api/v1/grid/{band}/{*callsign}` returns `{ g, t }` (1-letter fields, unix timestamp); writes the JSON body directly (bypasses MVC/serialization), reads only from the cache
- **StatsController** — `GET /api/stats` returns uptime, MQTT status, message rate, dropped-message count, connect/disconnect history, write totals, cache/DB sizes, active-station count, per-band breakdown, and last sweep info; disabled by default (`Stats:Enabled`), serves precomputed values (no cache scans on request)
- **StationRepository** — SQLite reads/writes; upserts are guarded with `WHERE excluded.last_update > stations.last_update`

## Prerequisites

- .NET 10 SDK
- Docker (optional)

## Run locally

```bash
dotnet run --project Gridifier.Api
```

Listens on `http://localhost:5027` (see `Properties/launchSettings.json`).

## API

```http
GET /api/v1/grid/{band}/{callsign}
GET /api/stats
```

### Grid response

`GET /api/v1/grid/{band}/{callsign}` returns the last heard grid for a station:

```json
{ "g": "KP30", "t": 1750000000 }
```

- `g` — 4-char Maidenhead grid locator (uppercase)
- `t` — last heard, unix seconds (UTC)
- `200` with the body above when the station is in the cache
- `400` with a text body (`Invalid band` / `Invalid callsign`) for invalid input
- `404` with an empty body when the callsign is unknown

`{*callsign}` catch-all handles `/` in callsigns like `OH1AA/MM`.

### Examples

```bash
# Query a station
curl http://localhost:5027/api/v1/grid/15m/DL1ABC

curl http://localhost:5027/api/v1/grid/15m/OH1AA/MM

# Server stats (disabled by default)
curl http://localhost:5027/api/stats   # set Stats:Enabled=true to enable
```

## Configuration

`appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Gridifier": "Data Source=gridifier.db"
  },
  "Mqtt": {
    "Host": "mqtt.pskreporter.info",
    "Port": 1883,
    "Subscriptions": [
      { "Topic": "pskr/filter/v2/+/+/+/+/+/+/+/+" }
    ]
  },
  "Cache": {
    "SweepIntervalMinutes": 5
  },
  "Stats": {
    "Enabled": false,
    "RefreshIntervalSeconds": 30,
    "ActiveWindowMinutes": 10
  },
  "Backup": {
    "Enabled": true,
    "IntervalHours": 6
  }
}
```

Override via environment variables:

```bash
set Mqtt__Subscriptions__0__Topic="pskr/filter/v2/20m/#"
set Mqtt__Subscriptions__1__Topic="pskr/filter/v2/40m/#"
set ConnectionStrings__Gridifier="Data Source=/data/gridifier.db"
```

`Mqtt:Subscriptions` is a list of connections, each with its own topic filter (inheriting `Host`/`Port` unless overridden per entry). Leave it empty to subscribe to all bands; use per-band topics like `pskr/filter/v2/20m/#` to split traffic across separate connections.

## Docker

```bash
docker build -t gridifier .
docker run --env-file .gridifier.env -v gridifier-data:/data -p 2080:2080 gridifier
```

`.gridifier.env` sets the runtime config (connection string to `/data/gridifier.db`, MQTT subscriptions, and `ASPNETCORE_URLS=http://+:2080`). The Dockerfile defaults the DB to `/data/gridifier.db` and runs as a non-root user.

## Publish (self-contained, no runtime needed)

```bash
dotnet publish Gridifier.Api -c Release -r win-x64 --self-contained -o C:\Deploy\Gridifier
```

Copy the `C:\Deploy\Gridifier` folder to the target machine. Run with a custom port:

```bash
set ASPNETCORE_URLS=http://0.0.0.0:8080
Gridifier.Api.exe
```

Or as a one-liner:

```bash
set ASPNETCORE_URLS=http://0.0.0.0:8080 && start /B Gridifier.Api.exe
```

## Tests

```bash
dotnet test
```