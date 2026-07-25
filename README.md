# Gridifier

Real-time PSK Reporter station grid locator tracker. Listens to the PSK Reporter MQTT feed, extracts receiver/sender callsigns and Maidenhead grid locators, and serves them via a REST API.

## Architecture

```
MQTT (pskreporter.info:1883)
       │
       ▼
 PskReporterWorker ──► Channel<Station> ──► DatabaseWriter ──► SQLite (WAL)
                                              │                      │
                                              ▼                      ▼
                                         StationCache         StationRepository
                                              │                      │
                                              ▼                      ▼
                                        StatsController ◄── AppStats
                                              │
                                              ▼
                                        StationsController (GET /api/stations/{callsign})
```

- **PskReporterWorker** — connects to MQTT, subscribes to `pskr/filter/v2/6m/...`, parses JSON messages
- **PskMessageHandler** — extracts `rc`/`rl` (receiver) and `sc`/`sl` (sender) pairs, validates, truncates grids to 4 chars, writes to channel
- **DatabaseWriter** — reads from channel in batches (100 items / 1s), deduplicates via `StationCache` (same grid = skip for 1h), bulk-upserts to SQLite
- **StationCache** — in-memory LRU cache (10k entries), PriorityQueue-based eviction, used by API for low-latency reads
- **StationsController** — `GET /api/stations/{callsign}` returns `{ grid, lastUpdate }`, checks cache first, falls back to DB
- **StatsController** — `GET /api/stats` returns connection status, message rates, cache/DB stats

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
GET /api/stations/{callsign}
GET /api/stats
```

### Examples

```bash
# Query a station
curl http://localhost:5027/api/stations/DL1ABC

# Server stats
curl http://localhost:5027/api/stats
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
    "Topic": "pskr/filter/v2/6m/+/+/+/+/+/+/+"
  }
}
```

Override via environment variables:

```bash
set Mqtt__Host=other.broker.com
set ConnectionStrings__Gridifier="Data Source=/data/gridifier.db"
```

## Docker

```bash
docker build -t gridifier .
docker run -v gridifier-data:/data -p 8080:80 gridifier
```

## Publish (self-contained, no runtime needed)

```bash
dotnet publish Gridifier.Api -c Release -r win-x64 --self-contained -o C:\Deploy\Gridifier
```

## Tests

```bash
dotnet test
```