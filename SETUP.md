# TaskFlow — Complete Setup Guide

> **Who this is for:** Developers learning Kubernetes, GraphQL, MongoDB, and cloud-native patterns for the first time. This guide takes you from a fresh Windows machine to a fully running production-grade application.

---

## What You Will Have Running by the End

| Component | Technology | Purpose |
|-----------|------------|---------|
| Local Kubernetes cluster | k3d | Simulates Azure AKS on your laptop |
| Backend API | ASP.NET Core 10 + HotChocolate | GraphQL API with JWT auth |
| Frontend | React 18 + Vite + Apollo Client | Kanban board with real-time updates |
| Database | MongoDB 7 | Document database |
| Package manager | Helm | Deploys all Kubernetes resources in one command |
| Metrics | Prometheus + Grafana | Dashboards and alerting |
| Tracing | Jaeger | Distributed request tracing |

---

## Prerequisites — Software to Install

Install all tools below **before** attempting to run the project. Each section explains what the tool is and why it is needed.

---

### 1. Docker Desktop

**What it is:** Docker runs applications in isolated containers. Every component in this project (the API, the database, the frontend) runs as a Docker container. k3d (the local Kubernetes cluster) also runs entirely inside Docker.

**Install:**
1. Go to [https://www.docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop)
2. Download **Docker Desktop for Windows**
3. Run the installer — accept all defaults
4. Restart your machine when prompted
5. Open Docker Desktop and wait for it to fully start (green icon in system tray)

**Verify:**
```powershell
docker --version
# Expected: Docker version 27.x.x or higher

docker run hello-world
# Expected: "Hello from Docker!" message
```

**Minimum requirements:**
- Windows 10/11 with WSL 2 enabled (the installer handles this)
- 4 GB RAM dedicated to Docker (set in Docker Desktop → Settings → Resources)
- 8 GB RAM recommended for running the full stack

---

### 2. .NET 10 SDK

**What it is:** The build tools and runtime for C#. Required to build and run the TaskFlow API locally (outside of Docker) and to run the test projects.

**Install:**
1. Go to [https://dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Download the **.NET 10 SDK** (x64) for Windows
3. Run the installer

**Verify:**
```powershell
dotnet --version
# Expected: 10.0.x
```

**Note:** The SDK includes the runtime. You do not need to install the runtime separately.

---

### 3. Node.js (LTS)

**What it is:** JavaScript runtime required to build the React frontend, run the development server, and execute the GraphQL code generator.

**Install:**
1. Go to [https://nodejs.org](https://nodejs.org)
2. Download the **LTS** version (Long Term Support — more stable)
3. Run the installer — accept all defaults
4. Ensure "Add to PATH" is checked during installation

**Verify:**
```powershell
node --version
# Expected: v22.x.x or higher (LTS)

npm --version
# Expected: 10.x.x or higher
```

---

### 4. k3d

**What it is:** k3d runs a full Kubernetes cluster inside Docker containers on your machine. It uses k3s — the same lightweight Kubernetes distribution used inside Azure AKS nodes. This means everything you learn here applies directly to real AKS.

**Why not use Docker Compose?** Docker Compose runs containers but has no concept of self-healing, rolling updates, resource limits, or network policies. Kubernetes adds all of that — it is the production way to run containers.

**Install (choose one method):**

**Option A — Chocolatey (recommended if you have it):**
```powershell
choco install k3d
```

**Option B — Direct download:**
1. Go to [https://github.com/k3d-io/k3d/releases](https://github.com/k3d-io/k3d/releases)
2. Download `k3d-windows-amd64.exe`
3. Rename it to `k3d.exe`
4. Move it to `C:\ProgramData\chocolatey\bin\` or any folder on your PATH

**Verify:**
```powershell
k3d version
# Expected: k3d version v5.x.x
```

---

### 5. kubectl

**What it is:** The command-line tool for talking to any Kubernetes cluster. The same `kubectl` commands work against your local k3d cluster and a real AKS cluster in Azure — the Kubernetes API is identical everywhere.

**Install:**
```powershell
# Via Chocolatey
choco install kubernetes-cli

# Or download directly from Microsoft
# https://dl.k8s.io/release/v1.29.0/bin/windows/amd64/kubectl.exe
# Place kubectl.exe on your PATH
```

**Verify:**
```powershell
kubectl version --client
# Expected: Client Version: v1.29.x or higher
```

---

### 6. Helm

**What it is:** The package manager for Kubernetes — like npm for Node.js or NuGet for .NET. Instead of applying 15+ YAML files manually with `kubectl apply`, Helm bundles them into a **chart** and deploys everything in one command. It also tracks versions and enables one-command rollbacks.

**Install:**
```powershell
# Via Chocolatey
choco install kubernetes-helm

# Or via Scoop
scoop install helm
```

**Verify:**
```powershell
helm version
# Expected: version.BuildInfo{Version:"v4.x.x", ...}
```

---

### 7. Git

**What it is:** Version control. Required to clone this repository.

**Install:**
1. Go to [https://git-scm.com/download/win](https://git-scm.com/download/win)
2. Run the installer — accept defaults
3. Ensure "Git from the command line" is selected

**Verify:**
```powershell
git --version
# Expected: git version 2.x.x
```

---

### 8. Make (for Makefile shortcuts)

**What it is:** A build automation tool. The project includes a `Makefile` with shortcut commands (`make deploy`, `make release`, etc.) that wrap longer commands.

**Install:**
```powershell
choco install make
```

**Verify:**
```powershell
make --version
# Expected: GNU Make 4.x
```

> **Note:** If you prefer not to install Make, you can run the underlying commands directly. Every `make <target>` has its full command documented in the `Makefile`.

---

### 9. Optional — Azure CLI (for AKS deployment)

**What it is:** Command-line tool for managing Azure resources. Required only if you want to deploy to real Azure AKS (not needed for local k3d development).

**Install:**
```powershell
# Via Chocolatey
choco install azure-cli

# Or download from Microsoft
# https://aka.ms/installazurecliwindows
```

**Verify:**
```powershell
az --version
# Expected: azure-cli 2.x.x
```

---

## Version Reference Table

| Tool | Minimum Version | Recommended | Check command |
|------|----------------|-------------|---------------|
| Docker Desktop | 4.x | Latest | `docker --version` |
| .NET SDK | 10.0 | 10.0 | `dotnet --version` |
| Node.js | 20.x LTS | 22.x LTS | `node --version` |
| k3d | 5.x | 5.8.x | `k3d version` |
| kubectl | 1.29 | 1.31 | `kubectl version --client` |
| Helm | 4.x | 4.1.x | `helm version` |
| Git | 2.x | Latest | `git --version` |

---

## Getting the Code

```powershell
git clone https://github.com/Souvika-CP/Kubernetes_Training.git
cd Kubernetes_Training
```

---

## Project Structure

```
Kubernetes_Training/
│
├── src/
│   └── TaskFlow.Api/           .NET 10 API (GraphQL + REST + Auth)
│
├── tests/
│   ├── TaskFlow.UnitTests/     Unit tests
│   └── TaskFlow.IntegrationTests/  Integration tests
│
├── frontend/                   React 18 + Vite + Apollo Client (TypeScript)
│
├── helm/
│   └── taskflow/               Helm chart — packages all Kubernetes resources
│       ├── values.yaml         Default configuration
│       ├── values.dev.yaml     Dev overrides
│       └── values.prod.yaml    Production overrides
│
├── k8s/                        Raw Kubernetes YAML (reference — Helm is used for deployment)
│   ├── api/
│   ├── mongodb/
│   └── monitoring/
│
├── docker-compose.yml          Local development (without Kubernetes)
├── Dockerfile                  API container build
├── Makefile                    Shortcut commands
├── HLD.md                      High Level Design document
├── LLD.md                      Low Level Design document
├── PROJECT_SUMMARY.md          Technology choices and best practices
├── DEMO_GUIDE.md               Step-by-step demo walkthrough
└── SETUP.md                    This file
```

---

## NuGet Packages (Backend)

The .NET API uses the following packages. They are restored automatically by `dotnet restore` — you do not install them manually.

| Package | Version | Purpose |
|---------|---------|---------|
| `MongoDB.Driver` | 3.9.0 | Official MongoDB .NET driver |
| `HotChocolate.AspNetCore` | 16.0.9 | GraphQL server — schema, queries, mutations, subscriptions |
| `HotChocolate.Data` | 16.0.9 | `[UseFiltering]` and `[UseSorting]` on GraphQL fields |
| `HotChocolate.Authorization` | 16.0.9 | JWT auth integration for GraphQL |
| `BCrypt.Net-Next` | 4.2.0 | Password hashing (BCrypt algorithm) |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.x | JWT token validation middleware |
| `Serilog.AspNetCore` | 10.0.0 | Structured logging |
| `Serilog.Sinks.Console` | 6.1.1 | Write logs to stdout (Kubernetes captures this) |
| `Serilog.Enrichers.Environment` | 3.0.1 | Add machine name to every log line |
| `Serilog.Enrichers.Thread` | 4.0.0 | Add thread ID to every log line |
| `prometheus-net.AspNetCore` | 8.2.1 | Prometheus metrics endpoint + HTTP instrumentation |
| `AspNetCore.HealthChecks.MongoDb` | 9.0.0 | MongoDB health check for `/health/ready` |
| `OpenTelemetry.Extensions.Hosting` | 1.15.3 | Wires OpenTelemetry into ASP.NET Core |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.15.2 | Auto-instruments HTTP requests with traces |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.15.3 | Sends traces to Jaeger via OTLP/gRPC |
| `MongoDB.Driver.Core.Extensions.DiagnosticSources` | 3.0.0 | Emits trace spans for every MongoDB command |

---

## npm Packages (Frontend)

The frontend uses the following packages. They are installed automatically by `npm install` — you do not install them manually.

### Runtime dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `react` | 18.x | UI component library |
| `react-dom` | 18.x | React DOM renderer |
| `react-router-dom` | 6.x | Client-side routing |
| `@apollo/client` | 4.x | GraphQL client — caching, queries, mutations |
| `graphql` | 16.x | GraphQL core library (required by Apollo) |
| `graphql-ws` | 5.x | WebSocket transport for GraphQL subscriptions |

### Development dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `vite` | 8.x | Build tool and dev server |
| `typescript` | 5.x | TypeScript compiler |
| `@vitejs/plugin-react` | Latest | Vite plugin for React (fast refresh) |
| `tailwindcss` | 4.x | Utility-first CSS framework |
| `@graphql-codegen/cli` | Latest | Generates TypeScript types and React hooks from the GraphQL schema |
| `@graphql-codegen/typescript-operations` | Latest | Generates typed operation types |
| `@graphql-codegen/typescript-react-apollo` | Latest | Generates typed Apollo React hooks |

---

## Running Locally — Step by Step

### Option A: Docker Compose (simplest — no Kubernetes)

Use this to verify the API and database work before deploying to Kubernetes.

```powershell
# From the project root
docker compose up --build
```

This starts:
- MongoDB at `localhost:27018`
- API at `localhost:5000`

Test it:
```powershell
Invoke-RestMethod http://localhost:5000/health/ready
# Expected: Healthy
```

Stop it:
```powershell
docker compose down
```

---

### Option B: Full Kubernetes Stack (recommended for learning)

#### Step 1 — Create the cluster

```powershell
k3d cluster create --config k3d-config.yaml
```

This creates a 3-node Kubernetes cluster (1 server + 2 agents) and a local Docker registry at `localhost:5050`. Takes about 30–60 seconds.

Fix the API server connection (required on Windows):
```powershell
kubectl config set-cluster k3d-taskflow --server="https://127.0.0.1:65165"
kubectl config set-cluster k3d-taskflow --insecure-skip-tls-verify=true
```

Verify:
```powershell
kubectl get nodes
# Expected: 3 nodes in Ready state
```

---

#### Step 2 — Build and push the API image

```powershell
make docker-build
make docker-push
```

Or manually:
```powershell
docker build -t localhost:5050/taskflow:v2 .
docker push localhost:5050/taskflow:v2
docker tag localhost:5050/taskflow:v2 taskflow-registry:5000/taskflow:v2
k3d image import taskflow-registry:5000/taskflow:v2 -c taskflow
```

**What is happening here?**
- `docker build` compiles the .NET app inside a container and creates an image
- `docker push` stores the image in the local registry at `localhost:5050`
- `k3d image import` makes the image available inside the cluster nodes (they use the internal address `taskflow-registry:5000`)

---

#### Step 3 — Build and push the frontend image

```powershell
make frontend-build
make frontend-push
```

Or manually:
```powershell
docker build -t localhost:5050/taskflow-frontend:v1 ./frontend
docker push localhost:5050/taskflow-frontend:v1
docker tag localhost:5050/taskflow-frontend:v1 taskflow-registry:5000/taskflow-frontend:v1
k3d image import taskflow-registry:5000/taskflow-frontend:v1 -c taskflow
```

---

#### Step 4 — Deploy with Helm

```powershell
make deploy
```

Or manually:
```powershell
helm upgrade --install taskflow helm/taskflow `
  -n taskflow-dev --create-namespace `
  -f helm/taskflow/values.yaml `
  --set image.tag=v2
```

Verify everything is running:
```powershell
kubectl get pods -n taskflow-dev
# Expected: mongodb-0, taskflow-xxx (x2), taskflow-frontend-xxx — all 1/1 Running
```

---

#### Step 5 — Add hostnames to your hosts file

This tells your browser that `taskflow.local` and `app.local` resolve to your local machine.

Open **Notepad as Administrator** → File → Open → `C:\Windows\System32\drivers\etc\hosts` → add these lines at the bottom:

```
127.0.0.1 taskflow.local
127.0.0.1 app.local
127.0.0.1 grafana.local
127.0.0.1 jaeger.local
```

Or run in **Administrator PowerShell**:
```powershell
Add-Content C:\Windows\System32\drivers\etc\hosts "`n127.0.0.1 taskflow.local"
Add-Content C:\Windows\System32\drivers\etc\hosts "`n127.0.0.1 app.local"
Add-Content C:\Windows\System32\drivers\etc\hosts "`n127.0.0.1 grafana.local"
Add-Content C:\Windows\System32\drivers\etc\hosts "`n127.0.0.1 jaeger.local"
```

---

#### Step 6 — Register test users and log in

Open your browser: **`http://app.local:8080`**

Register two test users (run once — skip if already done):
```powershell
# Primary user
Invoke-RestMethod http://taskflow.local:8080/auth/register `
  -Method Post -ContentType "application/json" `
  -Body '{"name":"Souvika","email":"souvika@taskflow.local","password":"Test1234!"}'

# Second user for multi-tenancy demo
Invoke-RestMethod http://taskflow.local:8080/auth/register `
  -Method Post -ContentType "application/json" `
  -Body '{"name":"Alice","email":"alice@taskflow.local","password":"Test1234!"}'
```

Then log in at `http://app.local:8080` with either set of credentials.

**Test Credentials:**

| User | Email | Password | Role |
|------|-------|----------|------|
| Primary | `souvika@taskflow.local` | `Test1234!` | Workspace owner |
| Second | `alice@taskflow.local` | `Test1234!` | Invited member |

---

## Running the Frontend in Dev Mode (Hot Reload)

For active frontend development, run Vite's dev server instead of using the containerised nginx version:

```powershell
cd frontend
npm install          # first time only
npm run dev
```

Open: `http://localhost:5173`

The dev server proxies API calls to `http://taskflow.local:8080` (configured in `vite.config.ts`). Changes to `.tsx`/`.css` files update the browser instantly without a page reload.

---

## Regenerating GraphQL Types

When the API schema changes, regenerate the typed React hooks:

```powershell
cd frontend
npm run codegen
```

This reads the live GraphQL schema from `http://localhost:8080/graphql` and regenerates `src/generated/graphql.ts` — all query/mutation/subscription hooks with full TypeScript types.

**The API must be running** (either via Docker Compose or Kubernetes) for codegen to work.

---

## Running Tests

```powershell
# Run all tests
dotnet test TaskFlow.slnx

# Run with verbose output
dotnet test TaskFlow.slnx --verbosity normal

# Run a specific project
dotnet test tests/TaskFlow.UnitTests/
```

---

## Makefile Quick Reference

All common operations are available as `make` targets:

```powershell
make help           # Show all available targets

# Local development
make dev            # Start Docker Compose stack (hot reload)
make dev-down       # Stop Docker Compose stack

# .NET
make build          # dotnet build
make test           # dotnet test

# Docker
make docker-build   # Build API image (tag: v1)
make docker-push    # Push API image to local registry

make frontend-build # Build frontend image
make frontend-push  # Push frontend image

# Kubernetes / Helm
make deploy         # helm upgrade --install
make rollback       # Helm rollback to previous revision
make status         # kubectl get all -n taskflow-dev
make logs           # Tail API pod logs
make clean          # Uninstall Helm release + delete namespace
make kubeconfig     # Fix kubectl connection after cluster restart

# Shortcuts
make release        # docker-build + docker-push + deploy
make frontend-release # frontend-build + frontend-push + deploy
```

---

## Cluster Management

```powershell
# Create cluster
k3d cluster create --config k3d-config.yaml

# Stop cluster (preserves all data and pods — fast restart)
k3d cluster stop taskflow

# Start cluster again
k3d cluster start taskflow
# Then run: make kubeconfig  (to fix the connection)

# Delete cluster entirely (removes all data)
k3d cluster delete taskflow

# List clusters
k3d cluster list
```

---

## Application URLs

Once the cluster is running and deployed:

| URL | What it is | Credentials |
|-----|------------|-------------|
| `http://app.local:8080` | React frontend | Register or use existing account |
| `http://taskflow.local:8080/graphql` | GraphQL IDE (Banana Cake Pop) | Bearer token in Headers tab |
| `http://taskflow.local:8080/health/live` | Liveness health check | None |
| `http://taskflow.local:8080/health/ready` | Readiness health check | None |
| `http://taskflow.local:8080/metrics` | Prometheus metrics (raw text) | None |
| `http://grafana.local:8080` | Grafana dashboards | admin / admin |
| `http://jaeger.local:8080` | Jaeger distributed tracing | None |

---

## Troubleshooting

### kubectl cannot connect to cluster after restart

```powershell
make kubeconfig
# Or manually:
kubectl config set-cluster k3d-taskflow --server="https://127.0.0.1:65165"
kubectl config set-cluster k3d-taskflow --insecure-skip-tls-verify=true
```

**Why this happens:** k3d writes `host.docker.internal` as the API server address, which doesn't always resolve correctly on Windows. The fix patches it to `127.0.0.1`.

---

### Pods stuck in ImagePullBackOff

```powershell
kubectl describe pod <pod-name> -n taskflow-dev
```

Look at the Events section. This usually means the image wasn't pushed to the registry or wasn't imported into the cluster.

Fix:
```powershell
make docker-push    # re-push the image
# Then wait ~30 seconds for the pod to retry
```

---

### Frontend shows blank white page

This is usually a JavaScript error. Open browser DevTools (F12) → Console tab. Common cause: the API is not reachable. Check:
```powershell
Invoke-RestMethod http://app.local:8080/health/ready
```

---

### Port 8080 already in use

Another application is using port 8080. Either stop that application, or modify `k3d-config.yaml` to use a different port (e.g. `8090:80`) and update your hosts file accordingly.

---

### helm upgrade fails with "cluster unreachable"

Run `make kubeconfig` first to refresh the connection, then retry `make deploy`.

---

### MongoDB connection error in API logs

```powershell
kubectl logs -n taskflow-dev -l app.kubernetes.io/name=taskflow --tail=30
```

If you see MongoDB connection refused, the MongoDB pod may still be starting. Wait 30 seconds and check:
```powershell
kubectl get pods -n taskflow-dev
# mongodb-0 should show 1/1 Running
```

---

## Learning Path

This project covers the following concepts in roughly this order. Use `TASKS.md` and `PROJECT_NOTES.md` for the full explanation of each.

| Phase | Topic | Key concepts |
|-------|-------|-------------|
| 1 | Environment setup | Docker, k3d, kubectl, Helm |
| 2 | .NET API | Minimal APIs, MongoDB driver, repository pattern |
| 3 | Docker | Multi-stage builds, layer caching, non-root containers |
| 4 | Kubernetes basics | Pods, Deployments, Services, Namespaces |
| 5 | Kubernetes production | Health probes, rolling updates, resource limits, NetworkPolicy |
| 6 | Helm | Charts, templates, values, environment overlays, rollbacks |
| 7 | Observability | Prometheus metrics, Grafana dashboards, structured logging, distributed tracing |
| 8 | GraphQL | Schema, queries, mutations, subscriptions, filtering, sorting |
| 9 | Authentication | JWT, BCrypt, middleware pipeline order |
| 10 | Security hardening | Non-root containers, read-only filesystem, dropped capabilities, NetworkPolicy |
| 11 | Frontend | React, Apollo Client v4, real-time subscriptions, optimistic updates, code generation |

Each concept builds on the previous one. `PROJECT_NOTES.md` contains the detailed explanation for every decision made along the way.
