# AGENTS.md

Guidance for AI agents working on Gridifier.

## Project overview

Real-time PSK Reporter station grid locator tracker. Listens to the PSK Reporter MQTT feed, extracts receiver/sender callsigns and Maidenhead grid locators, serves them via a REST API.

**Core architecture: RAM is the source of truth; SQLite is a recovery log.**
- All reads come from `StationCache` (per-band `ConcurrentDictionary`, 8-byte entries `(ushort Grid, uint LastHeard)`).
- DB is loaded once at startup (`cache.Seed`) and written to via guarded upserts; never read on the request path.
- `GridEndpoint` (minimal API) reads only from the cache (no DB round-trips).

## Commands

- Build: `dotnet build Gridifier.slnx`
- Test: `dotnet test Gridifier.slnx` (solution file is `.slnx`, not `.sln`)

## Project layout

- `Gridifier.Api` — ASP.NET Core host; `Program.cs` wires everything manually (no `Startup` class); minimal API endpoint + controllers, workers, rate limiting
- `Gridifier.Worker` — class library: `StationCache`, `DatabaseWriter`, `StationSweeper`, `StatsRefresher`, `DatabaseBackupWorker`, `PskReporterWorker`, `PskMessageHandler`, `AppStats`
- `Gridifier.Shared` — models (`Station`, `MqttSettings`), data (`DatabaseInitializer`, `StationRepository`, `DbConnectionFactory`, `DatabaseBackup`), validation (`CallsignValidator`, `BandValidator`, `GridValidator`, `GridCodec`)
- `Gridifier.Tests` — xUnit; repo tests use temp-file SQLite DBs; API tests use `WebApplicationFactory<Program>`

## Data flow

```
MQTT → PskReporterWorker → Channel<Station> → DatabaseWriter → guarded upsert → SQLite (WAL)
                             │                    │
                             ▼                    ▼
                        StationCache   StationSweeper (periodic quiet flush)
                             │
                             ▼
                    GridEndpoint (cache-only reads)
```

## Key conventions and gotchas

- **Grids are 4-char Maidenhead, encoded to `ushort`** via `GridCodec` (32,400 possible codes, bijective, case-insensitive). PskMessageHandler truncates to 4 chars before writing.
- **`Station.LastUpdate` is a `long`** (unix seconds). `uint` in cache entries (fits until 2106).
- **Guarded upserts**: `ON CONFLICT(callsign, band) DO UPDATE ... WHERE excluded.last_update > stations.last_update`. A station with `LastUpdate = 0` will NOT update an existing row — tests must set explicit `LastUpdate` values.
- **Cache `TryUpdate` semantics**: returns `true` only for a new station or a grid change (immediate DB write). Reports with `lastHeard <= existing.LastHeard` are ignored (1-second resolution — same-second grid changes are dropped by design).
- **Cache entries carry a dirty bit** (`Entry(uint LastHeard, ushort Grid, bool Dirty)`, still 8 bytes): dirty means the cache has a `LastHeard` the DB hasn't seen (repeat same-grid report, or a write still in flight). `TryUpdate` marks dirty on every accepted report; `MarkPersisted` clears it only if the written `lastHeard` still matches (a newer concurrent report stays dirty).
- **Persist budget**: repeat same-grid reports are NOT written to DB; the `StationSweeper` flushes ONLY dirty quiet stations (chunked transactions, guarded upserts make already-persisted rows no-ops). `GetQuiet(cutoff)` filters to `Dirty && LastHeard <= cutoff`; a persisted station is not re-selected on the next sweep unless it hears again. Do NOT zero `lastHeard` to achieve this — the API returns it as `t`.
- **DB durability**: `DatabaseBackup` takes online-backup snapshots (`{db}.bak`, 1 previous generation via `.bak.1`) on a timer and on graceful shutdown. Startup runs `PRAGMA quick_check`; if corrupt, the DB is restored from the latest snapshot (corrupt file preserved as `*.corrupt-<ts>`, never silently destroyed). Connections in `DatabaseBackup` use `Pooling=False` so file operations can move/overwrite the DB — do NOT remove that.
- **Band normalization**: lowercase (`BandValidator.Normalize` → `ToLowerInvariant`, regex `^\d+[cm]$` IgnoreCase). Callsigns uppercase. Validators normalize rather than throw.
- **Channel**: bounded `Channel<Station>` (capacity 10k, `DropOldest`), drops counted at the producer in `PskMessageHandler`.
- **Stats are precomputed** — `StatsRefresher` periodically fills `AppStats.ActiveStations`/`StationsByBand`/`CacheSize`/`MessagesPerSecond`; the stats endpoint must NOT scan the cache per request. `/api/stats` is disabled by default (`Stats:Enabled`) and returns 404 when off. `AppStats` tracks per-subscription status (`Subscriptions[i].Connected`, timestamps) plus an aggregate `ConnectedSubscriptions`/`MqttConnected`.
- **`MqttSettings.Subscriptions`** is a list of connections, each with its own topic. Default (empty list) = single all-bands sub `pskr/filter/v2/+/+/+/+/+/+/+`. Each sub inherits `Host`/`Port` unless overridden (`MqttSettings.GetSubscriptions()` resolves this). Per-band topics use the `#` wildcard, e.g. `pskr/filter/v2/20m/#`. One `PskReporterWorker` is registered per subscription (unique client-id per instance); all share the single `Channel<Station>`. Message fields: `rc`/`rl` (receiver), `sc`/`sl` (sender), `b` (band).
- **Do NOT use `AddHostedService<T>` for the per-subscription `PskReporterWorker`s** — it registers via `TryAddEnumerable`, which dedupes by implementation type, so a second identical `PskReporterWorker` registration is silently dropped (only the first sub ever connects). Register them directly as `AddSingleton<IHostedService>(sp => new PskReporterWorker(...))` in the loop. Other workers (single instance each) may keep using `AddHostedService`. Env-var list binding itself works fine (`Mqtt__Subscriptions__0__Topic` etc. all bind) — the symptom of this bug is "only one subscription connects despite N topics configured".

## API

- `GET /api/v1/grid/{band}/{*callsign}` → `{ g, t }` (grid, unix seconds) or 400/404. `{*callsign}` catch-all handles `/` in callsigns like `OH1AA/MM`.
- `GET /api/stats` → operational stats incl. connect/disconnect history; disabled by default (`Stats:Enabled`).
- **Grid response bypasses JSON serialization entirely**: `GridEndpoint` (minimal API, `Gridifier.Api/Endpoints`) returns `Results.Text($"{{\"g\":\"{grid}\",\"t\":{lastHeard}}}", "application/json")` — no `JsonSerializer`, no source-gen context. The grid is a fixed-format 4-char code and `t` a `uint`, so the interpolation is safe (no escaping needed). Keep the hot endpoint on the minimal API path; do NOT move it back to an MVC controller. `GridifierJsonContext`/`GridResponse` were removed as dead code.
- **Stats stays on MVC** (`StatsController`): `/api/stats` uses an anonymous type serialized via the default reflection-based JSON (not the hot path; `AddControllers()` is left unconfigured).
- **`.NET 10 gotcha`: `ControllerBase.Json(...)` does not exist** (removed in .NET 10 — it only lives on `Controller`). Use `Ok(model)`/`BadRequest(...)` etc.; `JsonResult(object, object)`'s 2nd arg is Newtonsoft `JsonSerializerSettings`, NOT `JsonSerializerContext` (passing a context throws `InvalidOperationException` at execution).

## Config (`Gridifier.Api/appsettings.json`)

- `Cache:SweepIntervalMinutes` (default 5)
- `Stats:Enabled` (default false), `Stats:RefreshIntervalSeconds` (30), `Stats:ActiveWindowMinutes` (10)
- `Backup:Enabled` (default true), `Backup:IntervalHours` (6)

## Testing notes

- DB/repo tests: each fixture creates a fresh temp-file DB and initializes schema.
- Writer tests use `WaitUntil(...)` polling, not fixed sleeps.
- When adding API tests that seed the cache, seed via `StationCache.Seed` (controller reads only cache). Use distinct callsigns per test — the cache singleton persists across tests in a fixture.
- API tests that toggle `Stats:Enabled` use `WithWebHostBuilder` + `ConfigureAppConfiguration` (the controller reads `IConfiguration` per request, so overrides apply). Each such test needs its own `WebApplicationFactory` instance.
