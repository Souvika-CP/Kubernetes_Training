# AKS Training Project — TaskFlow

## Vision

A production-grade task and project management system built with C# .NET 8, MongoDB, HotChocolate GraphQL on the backend, and a React + Apollo Client frontend. The project is designed to teach every meaningful AKS concept through hands-on implementation, progressing from local Docker Compose through a fully observable, scalable Kubernetes deployment.

**Learning goal:** By the end you understand how a real team ships and operates a full-stack containerised application on AKS — not just the happy path but health, secrets, scaling, rolling updates, and monitoring.

---

## Domain Model — TaskFlow

A multi-tenant project management service. Simple enough to implement quickly; rich enough to exercise every GraphQL and Kubernetes pattern.

```
Workspace
  └─ Project (name, description, status, ownerId)
       └─ TaskItem (title, description, status, priority, assigneeId, dueDate, tags)
            └─ Comment (body, authorId, createdAt)
User (id, name, email, role)
```

### GraphQL Operations

| Type | Operation |
|------|-----------|
| Query | `workspaces`, `workspace(id)`, `projects(workspaceId)`, `project(id)`, `task(id)`, `me` |
| Mutation | `createWorkspace`, `createProject`, `updateProject`, `deleteProject`, `createTask`, `updateTask`, `deleteTask`, `addComment` |
| Subscription | `taskUpdated(projectId)`, `commentAdded(taskId)` |

---

## Technology Stack

| Layer | Technology | Notes |
|-------|-----------|-------|
| API Framework | ASP.NET Core 8 Web API | Minimal API style |
| GraphQL (server) | HotChocolate 14 | Code-first schema |
| Database | MongoDB 7 | Official .NET driver |
| Auth | JWT Bearer | Simple symmetric key for training |
| **Frontend** | **React 18 + Vite 5 + TypeScript** | SPA served from nginx container |
| **GraphQL (client)** | **Apollo Client 3** | Queries, mutations, subscriptions |
| **UI styling** | **Tailwind CSS 3** | Utility-first; no heavy component lib needed |
| **Type safety** | **GraphQL Code Generator** | Auto-generates TS types from schema |
| **Routing** | **React Router 6** | Client-side SPA routing |
| Containerisation | Docker (multi-stage build) | Separate Dockerfiles for API and frontend |
| Local k8s | k3d (k3s in Docker) | Closest to AKS behaviour |
| Package manager | Helm 3 | Chart covers both API and frontend |
| Ingress | NGINX Ingress Controller | Path-based routing: `/` → UI, `/graphql` → API |
| Monitoring | Prometheus + Grafana | Replaces Azure Monitor |
| Logging | Serilog → stdout JSON | Consumed by k8s log aggregation |
| Tracing | OpenTelemetry + Jaeger | Replaces Azure App Insights |
| Local emulators | Azurite, MongoDB Docker | Replaces Azure Storage |
| Secret management | k8s Secrets + dotnet-user-secrets | Replaces Azure Key Vault |

---

## Local Development Setup

### Prerequisites

```
Docker Desktop (with WSL2 backend)
k3d          — brew install k3d / choco install k3d
kubectl      — included with Docker Desktop or install separately
helm         — choco install kubernetes-helm
.NET 8 SDK
Node.js 20 LTS  — https://nodejs.org (for React frontend)
mongosh      — optional, for DB inspection
```

### Emulator Strategy (No Azure Subscription Needed)

| Azure Service | Local Emulator | Purpose |
|--------------|----------------|---------|
| AKS | k3d (k3s in Docker) | Full k8s cluster on laptop |
| Azure Container Registry | Local Docker registry in k3d | Push/pull images |
| Azure Cosmos DB for MongoDB | `mongo:7` Docker image | Wire-compatible |
| Azure Blob Storage | Azurite | File uploads if added |
| Azure Monitor | Prometheus + Grafana | Metrics & dashboards |
| Azure Application Insights | Jaeger (OTEL) | Distributed tracing |
| Azure Key Vault | k8s Secrets + Sealed Secrets | Secret management |

### k3d Cluster Config (`k3d-config.yaml`)

```yaml
apiVersion: k3d.io/v1alpha5
kind: Simple
metadata:
  name: taskflow
servers: 1
agents: 2
ports:
  - port: 8080:80       # HTTP ingress
    nodeFilters: [loadbalancer]
  - port: 8443:443      # HTTPS ingress
    nodeFilters: [loadbalancer]
registries:
  create:
    name: taskflow-registry
    host: localhost
    hostPort: "5050"
```

---

## Repository Structure

```
TaskFlow/
├── src/
│   └── TaskFlow.Api/
│       ├── Domain/          # Entities, value objects
│       ├── Features/        # Vertical slice per aggregate
│       │   ├── Projects/
│       │   ├── Tasks/
│       │   ├── Users/
│       │   └── Workspaces/
│       ├── Infrastructure/  # MongoDB repos, config binding
│       ├── GraphQL/         # HotChocolate types, resolvers, subscriptions
│       ├── Health/          # Custom health checks
│       └── Program.cs
├── frontend/                # React + Apollo frontend
│   ├── src/
│   │   ├── apollo/          # ApolloClient setup, cache policies, auth link
│   │   ├── components/      # Shared UI components (Button, Badge, Modal)
│   │   ├── pages/
│   │   │   ├── Login/
│   │   │   ├── Dashboard/   # Workspace + project list
│   │   │   ├── Board/       # Kanban task board for a project
│   │   │   └── TaskDetail/  # Task detail + comments
│   │   ├── graphql/         # .graphql query/mutation/subscription files
│   │   ├── generated/       # Auto-generated TS types (via codegen)
│   │   └── main.tsx
│   ├── codegen.yml          # GraphQL Code Generator config
│   ├── vite.config.ts       # Vite + proxy config (dev: /graphql → API)
│   ├── tailwind.config.ts
│   ├── Dockerfile           # node:20-alpine build → nginx:alpine serve
│   └── nginx.conf           # SPA fallback routing
├── tests/
│   ├── TaskFlow.UnitTests/
│   └── TaskFlow.IntegrationTests/   # Testcontainers for MongoDB
├── helm/
│   └── taskflow/
│       ├── Chart.yaml
│       ├── values.yaml
│       ├── values.dev.yaml
│       └── templates/
│           ├── api-deployment.yaml
│           ├── api-service.yaml
│           ├── frontend-deployment.yaml  # NEW
│           ├── frontend-service.yaml     # NEW
│           ├── ingress.yaml              # path-based: / → frontend, /graphql → api
│           ├── configmap.yaml
│           ├── secret.yaml
│           ├── hpa.yaml
│           ├── pdb.yaml
│           └── serviceaccount.yaml
├── k8s/
│   ├── namespace.yaml
│   ├── mongodb/             # MongoDB StatefulSet + PVC
│   └── monitoring/          # Prometheus + Grafana manifests
├── docker-compose.yml       # Local dev (API + MongoDB + Jaeger + frontend)
├── docker-compose.test.yml  # Integration test compose
├── Dockerfile               # API Dockerfile
├── k3d-config.yaml
└── Makefile                 # Dev shortcuts (make dev, make deploy, etc.)
```

---

## Production-Ready Patterns Taught

### 1. Container Best Practices
- Multi-stage Dockerfile (build → publish → runtime)
- Non-root user in container
- Minimal base image (`mcr.microsoft.com/dotnet/aspnet:8.0-alpine`)
- `.dockerignore` to keep image lean

### 2. Kubernetes Workload Patterns
- `Deployment` with `RollingUpdate` strategy (maxUnavailable: 0)
- `StatefulSet` for MongoDB with `PersistentVolumeClaim`
- `Pod Disruption Budget` to guarantee availability during node drain
- `initContainer` for DB migration/seed checks

### 3. Configuration & Secrets
- `ConfigMap` for non-sensitive app settings
- `k8s Secret` (base64) for DB connection string, JWT key
- Secrets projected as environment variables (not volume mounts for this training)

### 4. Health & Reliability
- `/health/live` — liveness probe (is the process alive?)
- `/health/ready` — readiness probe (can it serve traffic? checks MongoDB)
- `/health/startup` — startup probe (gives slow pods time to init)
- Graceful shutdown with `IHostApplicationLifetime`

### 5. Resource Management
- `resources.requests` and `resources.limits` on every container
- `Horizontal Pod Autoscaler` targeting 70% CPU
- Namespace `ResourceQuota` and `LimitRange`

### 6. Observability
- Structured JSON logs via Serilog → stdout (kubectl logs / log aggregator)
- Prometheus metrics endpoint (`/metrics`) via `prometheus-net`
- Custom business metrics (tasks created, active projects)
- Grafana dashboard pre-configured
- OpenTelemetry traces → Jaeger

### 7. Networking
- `ClusterIP` service for internal traffic
- NGINX `Ingress` with host-based routing
- `NetworkPolicy` to restrict pod-to-pod traffic (only ingress → API, API → MongoDB)

### 8. RBAC & Security
- Dedicated `ServiceAccount` for the API pod
- Minimal `Role` + `RoleBinding` (read ConfigMaps, no cluster-wide access)
- Pod `securityContext`: `runAsNonRoot`, `readOnlyRootFilesystem`, `allowPrivilegeEscalation: false`

### 9. Helm Packaging
- Single chart with environment overlays (`values.dev.yaml`, `values.prod.yaml`)
- Templated image tag for CI/CD promotion
- `helm test` hook with a smoke-test pod

### 10. CI/CD Simulation
- `Makefile` targets: `build`, `test`, `docker-build`, `docker-push`, `deploy`, `rollback`
- Demonstrates zero-downtime rolling deployment
- Shows `kubectl rollout undo` for rollback

### 11. React Frontend with Apollo Client
- Vite dev server proxies `/graphql` to the API — no CORS config needed in dev
- Apollo Client `InMemoryCache` with field policies for pagination
- JWT stored in memory (not `localStorage`) — auth link injects `Authorization` header
- `useQuery` + loading/error states, `useMutation` with optimistic cache updates
- `useSubscription` for real-time task updates (WebSocket transport)
- GraphQL Code Generator produces typed hooks — no hand-written query types
- Multi-stage Dockerfile: `node:20-alpine` build → static files → `nginx:alpine` serve
- `nginx.conf` with `try_files $uri /index.html` for SPA routing
- Separate k8s Deployment for frontend; shared Ingress with path rules

---

## Phase Overview

| Phase | Focus | Output |
|-------|-------|--------|
| 0 | Tooling & cluster bootstrap | k3d cluster, local registry running |
| 1 | .NET API + MongoDB (no k8s yet) | Docker Compose stack running locally |
| 2 | GraphQL layer | Full schema queryable via Banana Cake Pop |
| 3 | Containerise properly | Production-quality Dockerfile + health endpoints |
| 4 | First Kubernetes deploy (raw manifests) | API running in k3d via kubectl apply |
| 5 | MongoDB on Kubernetes | StatefulSet + PVC + NetworkPolicy |
| 6 | Helm chart | Full chart, dev overlay, helm install |
| 7 | Observability | Prometheus + Grafana + Jaeger in cluster |
| 8 | Scaling & reliability | HPA + PDB + resource quotas |
| 9 | Security hardening | RBAC + pod securityContext + NetworkPolicy |
| 10 | CI/CD simulation | Makefile pipeline, rolling deploy, rollback |
| 11 | React + Apollo frontend | Visual UI with real-time updates running in k8s |

---

## Key Learning Outcomes

After completing all phases you will be able to:

1. Containerise a .NET application following production standards
2. Write and reason about Kubernetes manifests (Deployment, Service, Ingress, StatefulSet, PVC, HPA, PDB, NetworkPolicy, RBAC)
3. Package a service with Helm and manage environment differences
4. Instrument a service with health checks, structured logs, Prometheus metrics, and OTEL traces
5. Perform a zero-downtime rolling deployment and a safe rollback
6. Understand the translation from local k3d to real AKS (ACR, Azure Load Balancer, Azure Disk, Azure Monitor)
7. Know what changes when you get an Azure subscription (swap emulators → real services, one config change at a time)
8. Build a React + Apollo Client frontend that consumes a GraphQL API with real-time subscriptions, and deploy it as a separate container in the same Kubernetes cluster

---

## Frontend Architecture

### Pages and Apollo Patterns

| Page | Route | Apollo feature demonstrated |
|------|-------|----------------------------|
| Login | `/login` | REST call → JWT stored in memory; Apollo auth link |
| Dashboard | `/` | `useQuery` — list workspaces and projects |
| Project Board | `/projects/:id` | `useQuery` + `useSubscription` — Kanban board with live updates |
| Task Detail | `/tasks/:id` | `useQuery` + `useMutation` — edit form + comments |

### Apollo Client Setup (key patterns)

```typescript
// apollo/client.ts
const authLink = new ApolloLink((op, forward) => {
  op.setContext({ headers: { Authorization: `Bearer ${getToken()}` } });
  return forward(op);
});

const wsLink = new GraphQLWsLink(createClient({
  url: 'ws://app.local/graphql',
  connectionParams: () => ({ Authorization: `Bearer ${getToken()}` }),
}));

// HTTP for queries/mutations, WS for subscriptions
const splitLink = split(
  ({ query }) => isSubscription(query),
  wsLink,
  new HttpLink({ uri: '/graphql' }),
);

export const client = new ApolloClient({
  link: from([authLink, splitLink]),
  cache: new InMemoryCache(),
});
```

### GraphQL Code Generator

Running `npm run codegen` reads the schema from the live API and generates:
- TypeScript types for all schema types
- Typed React hooks (`useGetProjectsQuery`, `useCreateTaskMutation`, etc.)

This means you never write untyped query strings — every operation is fully type-safe.

### Frontend Ingress routing

```
http://app.local/           → taskflow-frontend service (React SPA)
http://app.local/graphql    → taskflow-api service (HotChocolate)
http://app.local/api/       → taskflow-api service (REST endpoints)
http://app.local/health/    → taskflow-api service (health probes)
http://app.local/metrics    → taskflow-api service (Prometheus scrape)
http://app.local/auth/      → taskflow-api service (JWT token endpoint)
```
