# Local observability stack

Build and run `modeller-api` the same way Vercel does (same `src/Modeller.Api/Dockerfile`), with
its OpenTelemetry output going to a local collector, Prometheus, and Grafana — so a change can be
validated, and its telemetry watched, before it is deployed.

## Run it

From the repo root:

```
docker compose up --build
```

- API: http://localhost:8080 (`/healthz/live`, `/v1/workspace/supported-views`)
- Grafana: http://localhost:3000 (anonymous access enabled, Prometheus datasource pre-provisioned)
- Prometheus: http://localhost:9090
- Collector's raw Prometheus-format metrics: http://localhost:8889/metrics

To point a locally-running `apps/website` (`npm run dev`, port 3200) at this API, set
`NEXT_PUBLIC_MODELLER_API_URL=http://localhost:8080`. The compose file sets
`ASPNETCORE_ENVIRONMENT=Development` so `appsettings.Development.json`'s
`Cors:AllowedOrigins` (`http://localhost:3200`) applies.

## What to look for

- **`modeller_api_process_starts_total`** (Prometheus/Grafana Explore) — increments once per
  process start. This is the direct signature of the cold-start-reconnect-loop incident this stack
  was built to catch: a count that keeps climbing every few minutes with no matching request
  volume means something is forcing repeated cold boots, not real traffic.
- **`http_server_request_duration_seconds`** (or the collector's equivalent OTLP metric name) —
  from the existing `AddAspNetCoreInstrumentation()` in `Program.cs`. Request volume and latency,
  for comparison against the process-start count above.
- Traces have no backend wired up here (only Grafana/Prometheus were asked for) — they're still
  visible in `docker compose logs otel-collector` for a quick sanity check that spans are arriving.

## Validating the idle-disconnect fix

1. Start this stack, then run `apps/website` locally against it.
2. Open an Initiative session page, confirm live updates work.
3. Switch away from the tab (or minimize the window) for more than 60 seconds.
4. Confirm no further `negotiate`/hub traffic appears in `docker compose logs modeller-api` until
   the tab is brought back to the foreground — before the fix, this traffic (and a full container
   restart under Vercel) repeated every ~5 minutes indefinitely.

## Tearing down

```
docker compose down
```
