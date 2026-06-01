# TaskFlow — Training Task List

Each task is self-contained and builds on the previous. Complete them in order.
Reference SPEC.md for full context on architecture decisions.

---

## Phase 0 — Tooling & Cluster Bootstrap

### TASK-001: Install prerequisites
**Goal:** Verify all local tools are installed and working.
**Steps:**
- Install Docker Desktop (WSL2 backend on Windows)
- Install k3d: `choco install k3d` or `winget install k3d`
- Install kubectl: included with Docker Desktop or `choco install kubernetes-cli`
- Install Helm 3: `choco install kubernetes-helm`
- Install .NET 8 SDK
- Verify: `docker version`, `k3d version`, `kubectl version`, `helm version`, `dotnet --version`

**Done when:** All five commands return version numbers without errors.

---

### TASK-002: Create k3d cluster with local registry
**Goal:** A running 3-node k3d cluster with a built-in container registry.
**Steps:**
- Create `k3d-config.yaml` (see SPEC.md)
- Run `k3d cluster create --config k3d-config.yaml`
- Verify: `kubectl get nodes` shows 3 nodes (1 server, 2 agents)
- Verify: `docker ps` shows the k3d containers and the registry container

**Done when:** `kubectl get nodes` shows all nodes in `Ready` state.

**AKS Translation:** In real AKS this is `az aks create`. The local registry maps to Azure Container Registry (ACR).

---

## Phase 1 — .NET API + MongoDB (Local Docker Compose)

### TASK-003: Scaffold the .NET solution
**Goal:** A clean solution structure matching the repo layout in SPEC.md.
**Steps:**
- Create solution: `dotnet new sln -n TaskFlow`
- Create API project: `dotnet new webapi -n TaskFlow.Api --use-minimal-apis`
- Create unit test project: `dotnet new xunit -n TaskFlow.UnitTests`
- Create integration test project: `dotnet new xunit -n TaskFlow.IntegrationTests`
- Add all projects to the solution
- Install packages in `TaskFlow.Api`:
  - `MongoDB.Driver`
  - `Serilog.AspNetCore`
  - `Serilog.Sinks.Console`
  - `Microsoft.Extensions.Diagnostics.HealthChecks`

**Done when:** `dotnet build` succeeds with zero errors or warnings.

---

### TASK-004: Define domain entities
**Goal:** C# records/classes for all domain entities in `Domain/`.
**Entities to create:**
- `Workspace` (Id, Name, OwnerId, CreatedAt)
- `Project` (Id, WorkspaceId, Name, Description, Status enum, OwnerId, CreatedAt)
- `TaskItem` (Id, ProjectId, Title, Description, Status enum, Priority enum, AssigneeId, DueDate, Tags, CreatedAt, UpdatedAt)
- `Comment` (Id, TaskId, Body, AuthorId, CreatedAt)
- `User` (Id, Name, Email, Role enum, CreatedAt)
**Notes:**
- Use `BsonId` and `BsonRepresentation(BsonType.ObjectId)` attributes
- Use `record` types for immutability where appropriate

**Done when:** All entities compile; no logic required yet.

---

### TASK-005: Implement MongoDB repositories
**Goal:** Repository interfaces + implementations for each entity using the MongoDB .NET driver.
**Steps:**
- Create `IRepository<T>` interface with: `GetByIdAsync`, `GetAllAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`
- Create `MongoRepository<T>` base implementation
- Create specific repos: `ProjectRepository`, `TaskRepository`, `UserRepository`, `WorkspaceRepository`
- Bind `MongoDbSettings` from `appsettings.json` (ConnectionString, DatabaseName)
- Register all repos in `Program.cs` as singletons

**Done when:** `dotnet build` succeeds; no DB connection required yet.

---

### TASK-006: Add Docker Compose for local dev
**Goal:** `docker-compose.yml` that starts MongoDB + the API together.
**Services:**
```
mongodb:   mongo:7, port 27017, named volume for data
api:       build from Dockerfile (create a simple one), port 5000
```
**Steps:**
- Write `docker-compose.yml`
- Write a simple `Dockerfile` (single-stage is fine for now — Phase 3 upgrades it)
- Set `MONGODB__CONNECTIONSTRING=mongodb://mongodb:27017` via environment
- Test: `docker compose up`, hit `http://localhost:5000/health`

**Done when:** Both containers start; API returns 200 on `/health`.

---

### TASK-007: Implement REST endpoints (thin layer)
**Goal:** Basic CRUD HTTP endpoints for Projects and Tasks to verify the stack works before adding GraphQL.
**Endpoints:**
- `GET  /api/projects`
- `GET  /api/projects/{id}`
- `POST /api/projects`
- `PUT  /api/projects/{id}`
- `DELETE /api/projects/{id}`
- Same set for `/api/tasks`
**Notes:**
- Use minimal API route handlers
- No auth required yet

**Done when:** All endpoints return correct status codes; data persists in MongoDB.

---

## Phase 2 — GraphQL with HotChocolate

### TASK-008: Install and configure HotChocolate
**Goal:** GraphQL endpoint live at `/graphql` with Banana Cake Pop UI.
**Steps:**
- Install packages: `HotChocolate.AspNetCore`, `HotChocolate.Data.MongoDB`
- Add `builder.Services.AddGraphQLServer()` pipeline in `Program.cs`
- Register `app.MapGraphQL()`
- Verify: browse `http://localhost:5000/graphql` → Banana Cake Pop loads

**Done when:** Banana Cake Pop opens; schema explorer shows root types (even if empty).

---

### TASK-009: Implement GraphQL Query types
**Goal:** All read operations queryable via GraphQL.
**Queries to implement:**
- `workspaces: [Workspace!]!`
- `workspace(id: ID!): Workspace`
- `projects(workspaceId: ID!): [Project!]!`
- `project(id: ID!): Project`
- `task(id: ID!): TaskItem`
**Steps:**
- Create `QueryType` class with resolver methods
- Use `[UseProjection]`, `[UseFiltering]`, `[UseSorting]` decorators
- Wire resolver methods to repository calls

**Done when:** All queries return correct data from MongoDB via Banana Cake Pop.

---

### TASK-010: Implement GraphQL Mutation types
**Goal:** All write operations available as GraphQL mutations.
**Mutations to implement:**
- `createWorkspace(input: CreateWorkspaceInput!): Workspace!`
- `createProject(input: CreateProjectInput!): Project!`
- `updateProject(input: UpdateProjectInput!): Project!`
- `deleteProject(id: ID!): Boolean!`
- `createTask(input: CreateTaskInput!): TaskItem!`
- `updateTask(input: UpdateTaskInput!): TaskItem!`
- `deleteTask(id: ID!): Boolean!`
- `addComment(input: AddCommentInput!): Comment!`
**Steps:**
- Create `MutationType` class
- Create Input record types for each mutation
- Add basic input validation (non-empty strings, valid IDs)

**Done when:** All mutations work end-to-end in Banana Cake Pop.

---

### TASK-011: Implement GraphQL Subscriptions
**Goal:** Real-time notifications over WebSocket.
**Subscriptions:**
- `taskUpdated(projectId: ID!): TaskItem!`
- `commentAdded(taskId: ID!): Comment!`
**Steps:**
- Add in-memory subscription provider: `AddInMemorySubscriptions()`
- Publish events from mutation resolvers using `ITopicEventSender`
- Test: open two Banana Cake Pop tabs — subscribe in one, mutate in other

**Done when:** Updates appear in the subscriber tab within ~1 second.

---

### TASK-012: Add JWT authentication
**Goal:** Protect mutations behind a Bearer token; queries remain public.
**Steps:**
- Add `Microsoft.AspNetCore.Authentication.JwtBearer` package
- Create `POST /auth/token` endpoint that issues a JWT for a hardcoded test user
- Add `[Authorize]` attribute to mutation types
- Configure CORS for the GraphQL endpoint

**Done when:** Unauthenticated mutations return 401; authenticated mutations succeed.

---

## Phase 3 — Production Dockerfile & Health Checks

### TASK-013: Write production multi-stage Dockerfile
**Goal:** A lean, secure Docker image following production standards.
**Requirements:**
- Stage 1 (`build`): `mcr.microsoft.com/dotnet/sdk:8.0-alpine`
- Stage 2 (`publish`): copy and publish release build
- Stage 3 (`runtime`): `mcr.microsoft.com/dotnet/aspnet:8.0-alpine`
- Non-root user: `RUN addgroup -S app && adduser -S app -G app`
- Copy only the published output
- `EXPOSE 8080`
- `USER app`

**Done when:** `docker build -t taskflow:dev .` produces an image under 150 MB; container runs as non-root.

---

### TASK-014: Implement health check endpoints
**Goal:** Kubernetes-ready health probes.
**Endpoints:**
- `GET /health/live` — always 200 if process is alive (liveness)
- `GET /health/ready` — 200 only when MongoDB is reachable (readiness)
- `GET /health/startup` — used during slow startup
**Steps:**
- Use `Microsoft.Extensions.Diagnostics.HealthChecks`
- Add `AspNetCore.HealthChecks.MongoDb` for MongoDB ping check
- Map each tag (`live`, `ready`, `startup`) to its own endpoint

**Done when:** With MongoDB running, `/health/ready` returns 200. With MongoDB stopped, it returns 503.

---

### TASK-015: Add structured logging with Serilog
**Goal:** JSON-formatted logs to stdout — the format k8s log aggregators expect.
**Steps:**
- Replace default logging with Serilog
- Configure JSON console sink: `new JsonFormatter()`
- Add request logging middleware: `app.UseSerilogRequestLogging()`
- Enrich logs with: `MachineName`, `ThreadId`, `AppVersion`
- Add correlation ID middleware that propagates `X-Correlation-Id` header

**Done when:** `docker logs <container>` shows newline-delimited JSON; each request produces one structured log line.

---

### TASK-016: Add Prometheus metrics
**Goal:** `/metrics` endpoint that Prometheus can scrape.
**Steps:**
- Install `prometheus-net.AspNetCore`
- Map `/metrics` endpoint
- Add custom counters:
  - `taskflow_tasks_created_total` (counter)
  - `taskflow_active_projects` (gauge)
  - `taskflow_graphql_request_duration_seconds` (histogram)
- Increment counters in mutation resolvers

**Done when:** `curl http://localhost:5000/metrics` returns Prometheus text format with custom metrics present.

---

## Phase 4 — First Kubernetes Deploy (Raw Manifests)

### TASK-017: Push image to local k3d registry
**Goal:** Image available inside the k3d cluster.
**Steps:**
- Tag image: `docker tag taskflow:dev localhost:5050/taskflow:v1`
- Push: `docker push localhost:5050/taskflow:v1`
- Verify from inside cluster: `kubectl run test --image=localhost:5050/taskflow:v1 --rm -it --restart=Never -- /bin/sh`

**Done when:** Image pull succeeds from within the cluster.

---

### TASK-018: Write Kubernetes Deployment and Service manifests
**Goal:** API running in k3d via raw `kubectl apply`.
**Files to create (`k8s/api/`):**
- `namespace.yaml` — namespace `taskflow-dev`
- `deployment.yaml` — 2 replicas, image, env from configmap/secret, probes, resource limits
- `service.yaml` — ClusterIP on port 8080
- `configmap.yaml` — non-sensitive settings (log level, MongoDB DB name)
- `secret.yaml` — MongoDB connection string, JWT key (base64 encoded)

**Key manifest details:**
```yaml
# deployment.yaml highlights
replicas: 2
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxSurge: 1
    maxUnavailable: 0
livenessProbe:
  httpGet: { path: /health/live, port: 8080 }
  initialDelaySeconds: 5
readinessProbe:
  httpGet: { path: /health/ready, port: 8080 }
  initialDelaySeconds: 10
resources:
  requests: { cpu: "100m", memory: "128Mi" }
  limits:   { cpu: "500m", memory: "256Mi" }
```

**Done when:** `kubectl get pods -n taskflow-dev` shows 2 pods `Running`; `kubectl logs` shows JSON.

---

### TASK-019: Add NGINX Ingress
**Goal:** API reachable at `http://taskflow.local` from your machine.
**Steps:**
- Install NGINX Ingress Controller via Helm:
  `helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx -n ingress-nginx --create-namespace`
- Write `k8s/api/ingress.yaml` routing `taskflow.local` → the API service
- Add `127.0.0.1 taskflow.local` to Windows `hosts` file

**Done when:** `curl http://taskflow.local/health/live` returns 200.

---

## Phase 5 — MongoDB on Kubernetes

### TASK-020: Deploy MongoDB as a StatefulSet
**Goal:** MongoDB running in k3d with persistent storage.
**Files to create (`k8s/mongodb/`):**
- `statefulset.yaml` — single replica, `mongo:7`, PVC template
- `service.yaml` — headless service (`clusterIP: None`) for StatefulSet DNS
- `secret.yaml` — MongoDB root password
- `pvc.yaml` — 1Gi PersistentVolumeClaim (k3d provides local-path provisioner)

**Done when:** `kubectl exec -n taskflow-dev mongodb-0 -- mongosh --eval "db.adminCommand('ping')"` succeeds.

---

### TASK-021: Add NetworkPolicy
**Goal:** Only the API pods can talk to MongoDB; nothing else.
**Steps:**
- Write `k8s/mongodb/networkpolicy.yaml`:
  - Deny all ingress to MongoDB by default
  - Allow ingress only from pods with label `app: taskflow-api`
- Write `k8s/api/networkpolicy.yaml`:
  - Allow egress from API to MongoDB only on port 27017
  - Allow egress to kube-dns on UDP 53

**Done when:** A test pod without the correct label cannot reach MongoDB; the API pod can.

---

## Phase 6 — Helm Chart

### TASK-022: Create Helm chart skeleton
**Goal:** A valid Helm chart for the TaskFlow API.
**Steps:**
- `helm create helm/taskflow` then clean out default templates
- Update `Chart.yaml` with name, version, appVersion
- Define `values.yaml` with all configurable values:
  - `image.repository`, `image.tag`
  - `replicaCount`
  - `resources`
  - `ingress.enabled`, `ingress.host`
  - `mongodb.connectionString` (override via secret)
  - `config.logLevel`

**Done when:** `helm lint helm/taskflow` passes with no errors.

---

### TASK-023: Template all Kubernetes resources into the chart
**Goal:** Every resource from Phase 4–5 templated in Helm.
**Templates to create:**
- `deployment.yaml`, `service.yaml`, `ingress.yaml`
- `configmap.yaml`, `secret.yaml`
- `hpa.yaml`, `pdb.yaml`
- `serviceaccount.yaml`
- `NOTES.txt` (post-install instructions)

**Done when:** `helm install taskflow ./helm/taskflow -n taskflow-dev -f helm/taskflow/values.dev.yaml` deploys successfully.

---

### TASK-024: Add environment overlays
**Goal:** Separate `values.dev.yaml` and `values.prod.yaml` with different replica counts, resources, and log levels.
**Differences:**
- dev: 1 replica, debug logging, relaxed resources
- prod: 3 replicas, info logging, stricter resources, HPA enabled

**Done when:** `helm diff upgrade` shows only expected differences between environments.

---

## Phase 7 — Observability

### TASK-025: Deploy Prometheus and Grafana
**Goal:** Metrics pipeline running in the cluster.
**Steps:**
- Add Prometheus community Helm repo
- Install kube-prometheus-stack: `helm install monitoring prometheus-community/kube-prometheus-stack -n monitoring --create-namespace`
- Add `ServiceMonitor` resource for TaskFlow API so Prometheus discovers `/metrics`

**Done when:** Grafana accessible at `http://grafana.local`; TaskFlow custom metrics visible in Prometheus UI.

---

### TASK-026: Build Grafana dashboard
**Goal:** A dashboard showing key TaskFlow metrics.
**Panels:**
- Request rate (req/s)
- Error rate (5xx %)
- P95 response latency
- Tasks created per minute
- Active projects (gauge)
- Pod count (from k8s metrics)
- MongoDB connection pool saturation
**Export dashboard JSON to `k8s/monitoring/taskflow-dashboard.json`**

**Done when:** Dashboard visible in Grafana with live data.

---

### TASK-027: Deploy Jaeger for distributed tracing
**Goal:** Request traces visible in Jaeger UI.
**Steps:**
- Deploy Jaeger all-in-one: `kubectl apply -f k8s/monitoring/jaeger.yaml`
- Install `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Exporter.Jaeger` packages
- Configure OTEL in `Program.cs` — export to Jaeger service address
- Add `X-Correlation-Id` propagation to traces

**Done when:** A GraphQL query in Banana Cake Pop produces a visible trace in Jaeger with MongoDB spans.

---

## Phase 8 — Scaling & Reliability

### TASK-028: Configure Horizontal Pod Autoscaler
**Goal:** API scales automatically under load.
**Steps:**
- Ensure `metrics-server` is enabled in k3d
- Write `hpa.yaml`:
  ```yaml
  minReplicas: 2
  maxReplicas: 10
  metrics:
    - type: Resource
      resource: { name: cpu, targetAverageUtilization: 70 }
  ```
- Generate load with `k6` or `hey` and watch `kubectl get hpa -w`

**Done when:** Pod count increases automatically under load; scales back down after load stops.

---

### TASK-029: Add Pod Disruption Budget
**Goal:** At least 1 pod always available during node maintenance.
**Steps:**
- Write `pdb.yaml` with `minAvailable: 1`
- Simulate node drain: `kubectl drain <node> --ignore-daemonsets --delete-emptydir-data`
- Observe PDB preventing all pods from being evicted simultaneously

**Done when:** During node drain, at least 1 API pod remains `Running` at all times.

---

### TASK-030: Set Namespace ResourceQuota and LimitRange
**Goal:** Prevent any single namespace from consuming all cluster resources.
**Steps:**
- Write `k8s/namespace-quotas.yaml`:
  - ResourceQuota: max 4 CPU, 2Gi memory across all pods
  - LimitRange: default request/limit for containers that don't specify

**Done when:** Attempting to deploy a pod requesting 10 CPU is rejected with a clear quota error.

---

## Phase 9 — Security Hardening

### TASK-031: Add Pod SecurityContext
**Goal:** Containers run as non-root with minimal capabilities.
**Add to deployment.yaml:**
```yaml
securityContext:
  runAsNonRoot: true
  runAsUser: 1000
  fsGroup: 2000
containers:
  - securityContext:
      allowPrivilegeEscalation: false
      readOnlyRootFilesystem: true
      capabilities:
        drop: ["ALL"]
```
**Note:** `readOnlyRootFilesystem: true` requires mounting a writable `emptyDir` for `/tmp`.

**Done when:** `kubectl get pod <name> -o yaml | grep -A 10 securityContext` shows all settings applied.

---

### TASK-032: Configure RBAC for the API ServiceAccount
**Goal:** API pod can only read ConfigMaps in its own namespace — nothing else.
**Steps:**
- Ensure `serviceaccount.yaml` creates a dedicated SA (`taskflow-api`)
- Write `role.yaml`: allow `get`, `list`, `watch` on `configmaps` in `taskflow-dev`
- Write `rolebinding.yaml`: bind role to the SA
- Set `automountServiceAccountToken: false` (we don't need k8s API access)

**Done when:** `kubectl auth can-i get pods --as=system:serviceaccount:taskflow-dev:taskflow-api` returns `no`.

---

## Phase 10 — CI/CD Simulation

### TASK-033: Write a Makefile for the dev workflow
**Goal:** Single entrypoint for all common operations.
**Targets:**
```
make dev          # docker compose up
make build        # dotnet build
make test         # dotnet test
make docker-build # multi-stage docker build + tag
make docker-push  # push to local k3d registry
make deploy       # helm upgrade --install
make rollback     # helm rollback taskflow 0
make status       # kubectl get all -n taskflow-dev
make logs         # kubectl logs -l app=taskflow-api -n taskflow-dev --follow
make clean        # helm uninstall + delete namespace
```

**Done when:** `make docker-build docker-push deploy` deploys a new version end-to-end.

---

### TASK-034: Simulate a zero-downtime rolling deployment
**Goal:** Understand how Kubernetes handles a version change with no downtime.
**Steps:**
- Run a background load generator against the API
- Change the image tag in `values.dev.yaml` to a new build
- Run `make deploy` (triggers Helm upgrade → rolling update)
- Watch: `kubectl rollout status deployment/taskflow-api -n taskflow-dev`
- Monitor: the load generator should report 0 errors throughout

**Done when:** New version deployed; load test shows 0 failed requests.

---

### TASK-035: Simulate a rollback
**Goal:** Know how to safely recover from a bad deployment.
**Steps:**
- Deploy a deliberately broken image (add a crash on startup)
- Observe: rolling update stalls, old pods stay running (maxUnavailable: 0)
- Run `helm rollback taskflow 0 -n taskflow-dev`
- Verify: traffic never interrupted, previous version restored

**Done when:** After rollback, `/health/ready` returns 200 and all pods are `Running`.

---

## Phase 11 — React Frontend with Apollo Client

### TASK-036: Scaffold React + Vite + TypeScript project
**Goal:** A running React app in `frontend/` with all dependencies installed.
**Steps:**
- `npm create vite@latest frontend -- --template react-ts`
- Install Apollo Client: `npm install @apollo/client graphql`
- Install subscriptions transport: `npm install graphql-ws`
- Install routing: `npm install react-router-dom`
- Install Tailwind CSS: `npm install -D tailwindcss postcss autoprefixer && npx tailwindcss init -p`
- Install codegen: `npm install -D @graphql-codegen/cli @graphql-codegen/typescript @graphql-codegen/typescript-operations @graphql-codegen/typescript-react-apollo`
- Configure Vite dev proxy in `vite.config.ts`:
  ```ts
  server: { proxy: { '/graphql': 'http://localhost:5000' } }
  ```
- Add `frontend` service to `docker-compose.yml` (dev mode with hot reload)

**Done when:** `npm run dev` starts Vite on port 5173; the dev proxy forwards to the API.

---

### TASK-037: Configure Apollo Client and GraphQL Code Generator
**Goal:** Typed Apollo hooks auto-generated from the live schema.
**Steps:**
- Create `src/apollo/client.ts` with:
  - `HttpLink` for queries/mutations pointing to `/graphql`
  - `GraphQLWsLink` for subscriptions (WebSocket)
  - `split` link to route subscriptions via WS, everything else via HTTP
  - `authLink` that reads a JWT from memory and injects the `Authorization` header
- Wrap `<App>` in `<ApolloProvider client={client}>` in `main.tsx`
- Write `codegen.yml` pointing at `http://localhost:5000/graphql`
- Add `"codegen": "graphql-codegen --config codegen.yml"` to `package.json`
- Run `npm run codegen` — verify `src/generated/` contains typed hooks

**Done when:** `npm run codegen` succeeds; `src/generated/graphql.ts` exists with types matching the schema.

---

### TASK-038: Build Login page
**Goal:** A login form that obtains a JWT and stores it for Apollo to use.
**Steps:**
- Create `src/pages/Login/LoginPage.tsx` with email + password form
- On submit: call `POST /auth/token` (plain `fetch`, no Apollo — it's REST)
- Store the token in a React context (`src/apollo/AuthContext.tsx`) — NOT in `localStorage`
- After login: redirect to `/`
- Add a protected route wrapper that redirects to `/login` when no token

**Learning note:** Storing JWT in memory (not localStorage) avoids XSS token theft — a real security pattern.

**Done when:** Login form exchanges credentials for a token; authenticated routes are accessible; unauthenticated routes redirect to `/login`.

---

### TASK-039: Build Dashboard page
**Goal:** Visualise workspaces and projects using `useQuery`.
**Steps:**
- Create `src/pages/Dashboard/DashboardPage.tsx`
- Use the generated `useGetWorkspacesQuery` hook to list workspaces
- For the selected workspace, use `useGetProjectsQuery(workspaceId)` to list projects
- Show loading skeleton while data loads; show error state on failure
- Each project card links to `/projects/:id`
- Add a "New Project" button that calls `useCreateProjectMutation` with an inline form

**Done when:** Dashboard shows real data from MongoDB; creating a project appears immediately (Apollo cache update).

---

### TASK-040: Build Project Board page with real-time subscriptions
**Goal:** A Kanban board that updates live when any client changes a task.
**Steps:**
- Create `src/pages/Board/BoardPage.tsx`
- Use `useGetProjectQuery(id)` to load the project and its tasks
- Render tasks in columns by `status` (Todo / In Progress / Done)
- Use `useMoveTaskMutation` with an **optimistic update** so the card moves instantly before the server confirms
- Use `useTaskUpdatedSubscription(projectId)` to receive live updates — when another user moves a card, it animates into the new column
- Add a "New Task" inline form at the bottom of each column

**Key Apollo patterns learned:**
- `optimisticResponse` on mutations
- `cache.modify` to update the cache after a mutation
- `useSubscription` updating the same cache entries as the query

**Done when:** Opening the board in two browser tabs — moving a card in one tab instantly reflects in the other.

---

### TASK-041: Containerise frontend and deploy to Kubernetes
**Goal:** Frontend running as a separate pod in k3d, reachable at `http://app.local/`.
**Steps:**
- Write `frontend/Dockerfile`:
  - Stage 1 (`build`): `node:20-alpine`, copy, `npm ci`, `npm run build`
  - Stage 2 (`serve`): `nginx:alpine`, copy `dist/` to `/usr/share/nginx/html`
  - Copy `nginx.conf` (include `try_files $uri /index.html` for SPA routing)
- Write `frontend/nginx.conf` with SPA fallback
- Build and push: `docker build -t localhost:5050/taskflow-frontend:v1 ./frontend && docker push localhost:5050/taskflow-frontend:v1`
- Add to Helm chart:
  - `templates/frontend-deployment.yaml` (1 replica, readinessProbe on `/`)
  - `templates/frontend-service.yaml` (ClusterIP)
  - Update `templates/ingress.yaml` to add path rules:
    - `/` → frontend service
    - `/graphql`, `/api/`, `/auth/`, `/health/`, `/metrics` → API service
- Add `127.0.0.1 app.local` to Windows `hosts` file

**Done when:** `http://app.local/` loads the React app; `http://app.local/graphql` reaches the API; page refresh doesn't 404.

---

## Bonus Tasks (Optional)

### TASK-042: Add Sealed Secrets for secret management
Replaces plain k8s Secrets with encrypted SealedSecrets that are safe to commit.
Install `kubeseal`, seal your DB connection string, commit the sealed manifest.

### TASK-043: Add integration tests with Testcontainers
Use `Testcontainers.MongoDb` to spin up a real MongoDB instance per test run.
No mocking — tests hit a real database in Docker.

### TASK-044: Add rate limiting to the GraphQL endpoint
Use ASP.NET Core 8 built-in rate limiting middleware.
Apply a per-IP sliding window limit of 100 req/min.

### TASK-045: Translate to real AKS
Document the exact commands to recreate this setup with a real Azure subscription:
- `az acr create` → push images
- `az aks create` → cluster with managed identity
- `az aks update --attach-acr` → grant cluster pull access
- Swap Azurite → Azure Blob, local secrets → Azure Key Vault + CSI driver

---

## Task Status Summary

| Task | Phase | Description | Status |
|------|-------|-------------|--------|
| 001 | 0 | Install prerequisites | ⬜ Todo |
| 002 | 0 | Create k3d cluster | ⬜ Todo |
| 003 | 1 | Scaffold .NET solution | ⬜ Todo |
| 004 | 1 | Define domain entities | ⬜ Todo |
| 005 | 1 | MongoDB repositories | ⬜ Todo |
| 006 | 1 | Docker Compose for local dev | ⬜ Todo |
| 007 | 1 | REST endpoints (thin layer) | ⬜ Todo |
| 008 | 2 | Install HotChocolate | ⬜ Todo |
| 009 | 2 | GraphQL Query types | ⬜ Todo |
| 010 | 2 | GraphQL Mutation types | ⬜ Todo |
| 011 | 2 | GraphQL Subscriptions | ⬜ Todo |
| 012 | 2 | JWT authentication | ⬜ Todo |
| 013 | 3 | Multi-stage Dockerfile (API) | ⬜ Todo |
| 014 | 3 | Health check endpoints | ⬜ Todo |
| 015 | 3 | Structured logging (Serilog) | ⬜ Todo |
| 016 | 3 | Prometheus metrics | ⬜ Todo |
| 017 | 4 | Push image to k3d registry | ⬜ Todo |
| 018 | 4 | Deployment + Service manifests | ⬜ Todo |
| 019 | 4 | NGINX Ingress | ⬜ Todo |
| 020 | 5 | MongoDB StatefulSet | ⬜ Todo |
| 021 | 5 | NetworkPolicy | ⬜ Todo |
| 022 | 6 | Helm chart skeleton | ⬜ Todo |
| 023 | 6 | Template all resources | ⬜ Todo |
| 024 | 6 | Environment overlays | ⬜ Todo |
| 025 | 7 | Deploy Prometheus + Grafana | ⬜ Todo |
| 026 | 7 | Grafana dashboard | ⬜ Todo |
| 027 | 7 | Jaeger distributed tracing | ⬜ Todo |
| 028 | 8 | Horizontal Pod Autoscaler | ⬜ Todo |
| 029 | 8 | Pod Disruption Budget | ⬜ Todo |
| 030 | 8 | ResourceQuota + LimitRange | ⬜ Todo |
| 031 | 9 | Pod SecurityContext | ⬜ Todo |
| 032 | 9 | RBAC for ServiceAccount | ⬜ Todo |
| 033 | 10 | Makefile for dev workflow | ⬜ Todo |
| 034 | 10 | Zero-downtime rolling deploy | ⬜ Todo |
| 035 | 10 | Rollback simulation | ⬜ Todo |
| 036 | 11 | Scaffold React + Vite + TS frontend | ⬜ Todo |
| 037 | 11 | Apollo Client + GraphQL Code Generator | ⬜ Todo |
| 038 | 11 | Login page (JWT in memory) | ⬜ Todo |
| 039 | 11 | Dashboard page (useQuery) | ⬜ Todo |
| 040 | 11 | Kanban board (subscriptions + optimistic UI) | ⬜ Todo |
| 041 | 11 | Containerise frontend + deploy to k8s | ⬜ Todo |
