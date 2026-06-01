# TaskFlow — Project Summary

## What Is TaskFlow?

TaskFlow is a production-grade, cloud-native task and project management system built as a hands-on training project for learning Kubernetes and AKS. It implements the full lifecycle of a real application: local development with Docker Compose, containerisation with multi-stage Docker builds, Kubernetes deployment with Helm, observability with Prometheus/Grafana/Jaeger, and a React frontend served from its own container inside the cluster.

The project is intentionally designed so that every pattern used locally (k3d) maps directly to its Azure equivalent — the only changes needed to move to real AKS are a handful of values in `values.prod.yaml`.

---

## Domain

A multi-tenant project management platform organised as:

```
Workspace → Project → TaskItem → Comment
                               User
```

Users can create workspaces, organise projects within them, manage tasks on a Kanban board (TODO → IN_PROGRESS → IN_REVIEW → DONE), and see real-time updates when teammates move tasks.

---

## Technology Stack

### Backend

| Technology | Version | Why it was chosen |
|-----------|---------|-------------------|
| **ASP.NET Core** | 10.0 | Native cross-platform web framework for .NET; minimal API style keeps entry point lean; integrates cleanly with all middleware |
| **HotChocolate** | 16 | Most feature-complete GraphQL server for .NET; code-first schema; built-in filtering, sorting, subscriptions, and auth; Banana Cake Pop UI for interactive development |
| **MongoDB** | 7 | Document database well-suited to task management (nested tags, flexible status); wire-compatible with Azure Cosmos DB — the local-to-cloud swap is a single connection string change |
| **BCrypt.Net-Next** | 4.2 | Adaptive hashing algorithm designed for passwords; includes random salt; slow by design to resist brute-force attacks |
| **JWT Bearer Auth** | ASP.NET Core built-in | Stateless authentication — no session storage needed; tokens carry claims (userId, email, role); symmetric HMAC-SHA256 signing appropriate for training |
| **Serilog** | 10 | Structured logging library; `CompactJsonFormatter` produces single-line JSON per log event — essential for Kubernetes log aggregators (Elastic, Loki, Azure Monitor) that parse JSON natively |
| **prometheus-net** | 8.2 | De-facto .NET Prometheus library; `UseHttpMetrics()` auto-instruments every HTTP request; custom counters, gauges, and histograms with minimal code |
| **OpenTelemetry** | 1.15 | Vendor-neutral distributed tracing standard; same OTLP exporter works with Jaeger (local), Azure Application Insights, Grafana Tempo, AWS X-Ray — just change the endpoint URL |

### Frontend

| Technology | Version | Why it was chosen |
|-----------|---------|-------------------|
| **React** | 18 | Industry-standard component library; fine-grained re-rendering aligns with Apollo's cache update model |
| **Vite** | 8 (rolldown) | Fastest dev server available; near-instant HMR; `npm run build` uses Rolldown (Rust-based bundler) for production — significantly faster than webpack |
| **TypeScript** | 5 | End-to-end type safety; `verbatimModuleSyntax` (strict mode) enforces correct `import type` usage, preventing type-only imports from appearing in the compiled bundle |
| **Apollo Client** | 4 | Mature GraphQL client; `InMemoryCache` normalises by `__typename:id` for automatic cache updates; optimistic responses for instant UI feedback; `GraphQLWsLink` for WebSocket subscriptions |
| **GraphQL Code Generator** | Latest | Reads the live GraphQL schema and generates fully-typed React hooks (`useGetProjectsQuery`, `useCreateTaskMutation`, etc.) — eliminates all hand-written query types |
| **Tailwind CSS** | 4 | Utility-first CSS; `@theme` directive registers custom design tokens; combined with custom CSS properties for a cohesive design system without a heavy component library |
| **React Router** | 6 | Standard SPA routing; `<ProtectedRoute>` wraps authenticated pages; `useNavigate` for post-login redirect |
| **Inter font** | Google Fonts | Clean, modern sans-serif typeface designed for readability on screens; industry default for professional UI |

### Infrastructure

| Technology | Version | Why it was chosen |
|-----------|---------|-------------------|
| **Docker** | Desktop | Multi-stage builds reduce final image size dramatically; layer caching speeds up CI by only re-running steps when dependencies change |
| **k3d** | 5.8 | k3s (Kubernetes distribution used inside AKS) running inside Docker containers; starts in <30 seconds; no VM overhead; closest local approximation to real AKS behaviour |
| **kubectl** | 1.29 | Universal Kubernetes CLI; works identically against k3d and real AKS clusters |
| **Helm** | 4 | Kubernetes package manager; single chart with environment overlays (`values.dev.yaml`, `values.prod.yaml`); `helm upgrade --install` enables idempotent CI/CD deployments; `helm rollback` for safe recovery |
| **Traefik** | k3s built-in | Ingress controller included with k3s; path-based routing (`/graphql` → API, `/` → frontend); no extra installation needed in k3d |
| **nginx:alpine** | Latest | Minimal static file server for the React SPA; `try_files $uri /index.html` handles client-side routing; `immutable` cache headers for hashed asset filenames |
| **Prometheus** | via kube-prometheus-stack | Time-series metrics database; `ServiceMonitor` CRD (Prometheus Operator) discovers scrape targets from Kubernetes labels — no manual config edits |
| **Grafana** | via kube-prometheus-stack | Dashboard visualisation; pre-provisioned `TaskFlow Operations` dashboard auto-loaded via a ConfigMap with the `grafana_dashboard: "1"` label |
| **Jaeger** | 1.57 all-in-one | Distributed tracing backend; accepts OTLP directly (port 4317); trace view shows HTTP request → MongoDB query chains |

---

## Best Practices Implemented

### Container Best Practices

**Multi-stage builds** — The API Dockerfile has a build stage (`sdk:10.0-alpine`) and a runtime stage (`aspnet:10.0-alpine`). The final image contains only the published application — no SDK, no source code, no build tooling. This reduces the attack surface and image size.

**Non-root containers** — Both the API and frontend containers run as non-root users. The .NET base image ships with a pre-created `app` user. The nginx image drops privileges to `nginx` user. The Kubernetes `PodSecurityContext` enforces `runAsNonRoot: true` and `runAsUser: 1654`.

**Read-only root filesystem** — `readOnlyRootFilesystem: true` in `containerSecurityContext` means the container cannot write to its own filesystem at runtime. This prevents an attacker who gains code execution from installing tools or modifying files.

**Dropped capabilities** — `capabilities: { drop: [ALL] }` removes all Linux kernel capabilities from the container process. Unless your application explicitly needs capabilities (e.g., binding to port < 1024), all should be dropped.

**Layer caching** — `.csproj` files are copied before source code so `dotnet restore` is cached between builds. `package.json` and `package-lock.json` are copied before source so `npm ci` is cached. Only changed layers re-run.

**`.dockerignore`** — Excludes `node_modules`, `dist`, `bin`, `obj`, `tests`, `k8s`, `helm` from the Docker build context. Without this, Docker sends hundreds of MB to the daemon on every build.

---

### Kubernetes Best Practices

**Liveness, Readiness, and Startup probes** — Three separate health endpoints serve distinct purposes:
- `liveness` (`/health/live`) — no checks; if it fails, the pod is dead and should restart
- `readiness` (`/health/ready`) — checks MongoDB connectivity; if it fails, stop sending traffic but don't restart
- `startup` (`/health/startup`) — gives slow-starting pods 5 minutes (30 × 10s) before liveness kicks in

This split prevents a MongoDB outage from triggering a restart loop (which would make things worse, not better).

**Zero-downtime rolling updates** — `maxUnavailable: 0, maxSurge: 1` means Kubernetes brings a new pod fully Ready before removing an old one. Combined with readiness probes, users never see a request land on a pod that isn't ready.

**Resource requests and limits** — Every container declares `requests` (guaranteed allocation, used by the scheduler) and `limits` (hard ceiling; OOMKill if exceeded). Without requests, the scheduler places pods arbitrarily and nodes become oversubscribed. Without limits, one runaway pod can starve all others on the node.

**Pod Disruption Budget** — `minAvailable: 1` ensures Kubernetes will not evict more pods than necessary during node drain or cluster upgrades. Without a PDB, a maintenance window could evict all replicas simultaneously.

**NetworkPolicy** — Least-privilege networking: MongoDB only accepts connections from API pods. API pods can only initiate connections to MongoDB and DNS. All other pod-to-pod traffic is blocked. This limits blast radius if a pod is compromised.

**StatefulSet for MongoDB** — StatefulSets give each pod a stable identity (`mongodb-0`) and a dedicated PersistentVolumeClaim. Deployments share storage across replicas, which is wrong for a database. The headless service gives MongoDB its own stable DNS entry.

**Dedicated ServiceAccount** — The API pod runs under `taskflow-api` ServiceAccount instead of the `default` account. `automountServiceAccountToken: false` prevents the pod from accessing the Kubernetes API unless explicitly needed. Least-privilege principle applied at the RBAC layer.

**Namespace isolation** — All TaskFlow resources live in `taskflow-dev`, monitoring in `monitoring`. Namespace ResourceQuota and LimitRange prevent any single workload from consuming all cluster resources.

---

### Application Best Practices

**Structured JSON logging** — Serilog writes `CompactJsonFormatter` output to stdout. Kubernetes captures stdout and forwards it to log aggregators. JSON means every field (method, path, status, duration, correlationId) is individually queryable — no regex parsing needed.

**Correlation IDs** — Every HTTP request gets a unique `X-Correlation-Id`. It's pushed to Serilog's `LogContext` so every log line for that request includes it. When a client sends an ID, it's preserved — enabling end-to-end tracing across multiple services or proxies.

**Single MongoClient singleton** — The MongoDB driver maintains an internal connection pool per `MongoClient`. One client per process is correct. Creating a client per request would exhaust TCP connections under any real load.

**Environment variable configuration** — ASP.NET Core's `__` convention maps `MongoDb__ConnectionString` to `MongoDb.ConnectionString` in the config hierarchy. The same POCO class (`MongoDbSettings`) works in local Docker Compose, k3d, and real AKS — only the source of the value changes (compose env block → Kubernetes Secret).

**Secrets never in source control** — The JWT signing key and MongoDB password live in Kubernetes Secrets (base64-encoded, not in the chart repository). In production these would come from Azure Key Vault via `--set` at deploy time in CI/CD.

**`[GraphQLIgnore]` on sensitive fields** — `User.PasswordHash` is excluded from the GraphQL schema. Any sensitive field on a domain entity used as a GraphQL type must be explicitly excluded or it becomes queryable by any authenticated client.

**Optimistic UI updates** — The Kanban board writes a predicted result to Apollo's InMemoryCache before the server responds. The user sees instant feedback. If the server returns a different result, the cache is corrected automatically. If the mutation fails, the optimistic entry is rolled back.

**Token in memory, not localStorage** — The JWT is stored in a React context (in-memory), not `localStorage` or cookies. `localStorage` is accessible to any JavaScript on the page (XSS attack vector). In-memory storage means the token disappears on page refresh — an acceptable trade-off for a training project, and the correct approach for high-security applications.

**Helm environment overlays** — `values.yaml` contains production defaults. `values.dev.yaml` overrides for local development (lower resources, debug logging, no HPA). `values.prod.yaml` overrides for production (more replicas, HPA, PDB, production JWT key). The same chart, three environments, no YAML duplication.

---

## Local → AKS Migration Path

The project was specifically designed so that every local component has a known AKS equivalent. Migration requires changing `values.prod.yaml` only:

| What changes | values.prod.yaml key | Local value | AKS value |
|-------------|---------------------|-------------|-----------|
| Image registry | `image.repository` | `taskflow-registry:5000/taskflow` | `myregistry.azurecr.io/taskflow` |
| Ingress controller | `ingress.className` | `traefik` | `azure-application-gateway` or `nginx` |
| Domain name | `ingress.host` | `taskflow.local` | `taskflow.yourdomain.com` |
| Secret management | (CI/CD `--set`) | values.yaml plain text | Azure Key Vault reference |
| Storage | (automatic) | local-path provisioner | `managed-csi` (Azure Disk) |
| Subscriptions | (code change) | `AddInMemorySubscriptions()` | `AddRedisSubscriptions()` + Azure Cache for Redis |
| Monitoring | (same chart) | Prometheus + Grafana in-cluster | Azure Monitor managed Prometheus + Grafana |
| Tracing | (endpoint only) | Jaeger OTLP | Azure Application Insights OTLP endpoint |

---

## Project Stats

| Metric | Value |
|--------|-------|
| Phases completed | 11 of 11 |
| Tasks completed | 41 of 41 |
| Backend language | C# / .NET 10 |
| Frontend language | TypeScript / React 18 |
| Kubernetes resources | Deployment ×2, StatefulSet ×1, Service ×3, Ingress ×1, ConfigMap ×1, Secret ×1, PDB ×1, NetworkPolicy ×2, ServiceAccount ×1, HPA ×1, ServiceMonitor ×1 |
| Helm chart templates | 11 |
| Docker images | 2 (API + Frontend) |
| GraphQL operations | 6 queries, 8 mutations, 2 subscriptions |
| Observability signals | Logs (Serilog JSON) + Metrics (Prometheus) + Traces (OTLP/Jaeger) |
