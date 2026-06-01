# TaskFlow — Demo Guide
### Step-by-Step Walkthrough with Technology Explanations

> **Audience:** Technical and semi-technical stakeholders
> **Duration:** ~30–45 minutes
> **Structure:** Each section = one PowerPoint slide or slide group

---

---

# SLIDE 1 — Title Slide

## "TaskFlow: From Code to Cloud-Native Kubernetes"

**Subtitle:** A production-grade application running on Kubernetes — built from scratch

**What this demo covers:**
- A real full-stack application (React + .NET + MongoDB)
- Running inside a local Kubernetes cluster
- With Docker containers, Helm packaging, and a full observability stack
- Following the same patterns used in real Azure AKS deployments

---

---

# SLIDE 2 — What Did We Build?

## The Application

TaskFlow is a **project and task management tool** — think a simplified Jira or Trello.

**Features demonstrated today:**
- User registration and JWT login
- Create Workspaces and Projects
- Kanban board — drag and drop tasks between columns
- Real-time updates — move a task and all browser tabs update instantly
- Everything running in Kubernetes, packaged with Helm

**Why this project?**
> The goal was never to build the best task manager. The goal was to build something real enough that every Kubernetes pattern — health probes, rolling updates, network policies, observability — had a genuine reason to exist.

---

---

# SLIDE 3 — Architecture Overview

## How the Pieces Fit Together

```
Your Browser
     │  http://app.local:8080
     ▼
 ┌─────────────────────────────────────────────────┐
 │            k3d Kubernetes Cluster               │
 │                                                 │
 │  Traefik (Ingress Controller)                   │
 │    /         → React Frontend (nginx)           │
 │    /graphql  → .NET API                         │
 │    /api      → .NET API                         │
 │    /auth     → .NET API                         │
 │                                                 │
 │  .NET API  ←→  MongoDB                         │
 │                                                 │
 │  Prometheus → Grafana (dashboards)              │
 │  Jaeger (distributed tracing)                   │
 └─────────────────────────────────────────────────┘
```

**Key concept — Kubernetes is the operating system for containers.**
Just as Windows manages processes and memory on your laptop, Kubernetes manages containers across a cluster of machines. It decides where to run them, restarts them when they crash, and routes network traffic between them.

---

---

# SLIDE 4 — Pre-Demo Setup

## Start the Cluster

> Run these commands before the demo starts (takes ~60 seconds)

### Step 1: Start the k3d cluster

```powershell
k3d cluster create --config k3d-config.yaml
```

**What is k3d?**
k3d runs a Kubernetes cluster inside Docker containers on your laptop. It uses the same Kubernetes distribution (k3s) that Azure uses for AKS nodes. The cluster here has:
- 1 control-plane server node
- 2 worker (agent) nodes
- A local Docker image registry at `localhost:5050`

**Why not just use Docker Compose?**
Docker Compose runs containers, but it has no concept of self-healing, rolling updates, resource limits, or network policies. Kubernetes adds all of that — it's the production-grade way to run containers.

---

### Step 2: Fix the cluster connection

```powershell
kubectl config set-cluster k3d-taskflow --server="https://127.0.0.1:65165"
kubectl config set-cluster k3d-taskflow --insecure-skip-tls-verify=true
```

**Why is this needed?**
k3d writes the API server address as `host.docker.internal`, which doesn't always resolve correctly on Windows. We patch it to use `127.0.0.1` (loopback) instead. This is a local development quirk — in real AKS, `az aks get-credentials` handles this automatically.

---

### Step 3: Verify the cluster is healthy

```powershell
kubectl get nodes
```

**Expected output:**
```
NAME                    STATUS   ROLES                  AGE   VERSION
k3d-taskflow-agent-0    Ready    <none>                 30s   v1.31.5+k3s1
k3d-taskflow-agent-1    Ready    <none>                 30s   v1.31.5+k3s1
k3d-taskflow-server-0   Ready    control-plane,master   35s   v1.31.5+k3s1
```

**What you're seeing:**
`kubectl` is the command-line tool for talking to any Kubernetes cluster. The same commands work against your local k3d cluster and a real AKS cluster in Azure — the API is identical.

---

### Step 4: Build and push the images

```powershell
make docker-build
make docker-push
make frontend-build
make frontend-push
```

**What is a Docker image?**
A Docker image is a packaged, self-contained snapshot of an application and all its dependencies. It's immutable — the same image runs identically on any machine that has Docker. Think of it as a USB drive for software.

**What are the two images?**

| Image | Contents | Size |
|-------|----------|------|
| `taskflow:v2` | .NET 10 runtime + compiled API binary | ~152 MB |
| `taskflow-frontend:v1` | nginx + compiled React app (HTML/JS/CSS) | ~25 MB |

**Why two separate images?**
The API and frontend have different runtimes (.NET vs nginx), different scaling needs, and different update frequencies. Separating them means you can redeploy just the frontend after a CSS change without touching the API.

**What is a multi-stage build?**
The Dockerfile has two stages:
1. A large build image (Node.js/SDK) that compiles the code
2. A tiny runtime image that only contains the compiled output

The final image has no compiler, no source code — only what's needed to run. This reduces the image size and attack surface dramatically.

---

### Step 5: Deploy with Helm

```powershell
helm upgrade --install taskflow helm/taskflow `
  -n taskflow-dev --create-namespace `
  -f helm/taskflow/values.yaml
```

**What is Helm?**
Helm is the package manager for Kubernetes — like `apt` for Ubuntu or `npm` for Node.js. Instead of applying 15 separate YAML files manually, Helm bundles them into a **chart** and deploys everything in one command. It also tracks versions and enables one-command rollbacks.

**What gets created?**
```powershell
kubectl get all -n taskflow-dev
```

You'll see: 2 API pods, 1 frontend pod, 1 MongoDB pod, services, and an ingress — all from a single Helm command.

---

---

# SLIDE 5 — Demo Part 1: The Running Cluster

## Show the Cluster State

### Command to run:
```powershell
kubectl get pods -n taskflow-dev
```

**Expected output:**
```
NAME                                 READY   STATUS    RESTARTS   AGE
mongodb-0                            1/1     Running   0          1m
taskflow-f4f87c4f5-9xlhq             1/1     Running   0          1m
taskflow-f4f87c4f5-srvsg             1/1     Running   0          1m
taskflow-frontend-679b84f575-ms58c   1/1     Running   0          1m
```

**Talking points:**
- **4 containers running** across the cluster's worker nodes
- **2 API pods** — Kubernetes automatically load-balances between them
- `1/1 READY` means the pod passed its health check and is serving traffic
- `mongodb-0` is a StatefulSet pod — it has a stable identity and its own disk

---

### Show what Kubernetes created:
```powershell
kubectl get all -n taskflow-dev
```

**Walk through each resource type:**

| Resource | What it is |
|----------|------------|
| `pod` | A running container instance |
| `deployment` | Manages a set of identical pods; handles rolling updates and self-healing |
| `statefulset` | Like a deployment but for stateful workloads (databases) — gives pods stable names and storage |
| `service` | A stable network address for a group of pods; load balances traffic across all matching pods |
| `ingress` | Routes external HTTP traffic to the right service based on hostname and URL path |

---

### Show the ingress:
```powershell
kubectl get ingress -n taskflow-dev
```

**Output:**
```
NAME       CLASS     HOSTS                      ADDRESS
taskflow   traefik   taskflow.local,app.local   172.20.0.3,...
```

**Key point:**
Traefik is the ingress controller — it's the front door of the entire cluster. When a request arrives at `app.local:8080`, Traefik reads the URL path and forwards it to the right service. It's like a smart reverse proxy that reads its rules from Kubernetes itself.

---

---

# SLIDE 6 — Demo Part 2: Health Checks

## Show Kubernetes Health Probes in Action

### Open in browser or run in terminal:

```powershell
Invoke-RestMethod http://taskflow.local:8080/health/live
Invoke-RestMethod http://taskflow.local:8080/health/ready
```

**Expected:** `Healthy` for both

**Why are there three health endpoints?**

This is one of the most important Kubernetes patterns. The API exposes three separate health endpoints with different meanings:

| Endpoint | Question it answers | What Kubernetes does on failure |
|----------|--------------------|---------------------------------|
| `/health/live` | "Is the process alive?" | Kills the container and restarts it |
| `/health/ready` | "Can this pod serve traffic right now?" | Removes pod from load balancer (no restart) |
| `/health/startup` | "Has the app finished initialising?" | Holds off liveness/readiness until it passes |

**Why this matters:**
Imagine MongoDB goes down temporarily. Without separate probes, Kubernetes would restart all API pods — turning a partial outage into a complete outage. With separate probes:
- Readiness fails → traffic stops going to this pod
- Liveness still passes → pod is NOT restarted
- When MongoDB comes back → readiness passes → traffic resumes

The same pod, never restarted, handles a database blip gracefully.

---

---

# SLIDE 7 — Demo Part 3: The Application

## Show the Frontend

### Open in browser:
```
http://app.local:8080
```

**Walk through:**

---

### 7a. Login Page

**URL:** `http://app.local:8080/login`

**Credentials:** `souvika@taskflow.local` / `Test1234!`

**What's happening behind the scenes:**
```
Browser → POST http://app.local:8080/auth/token
        → Traefik → API pod
        → BCrypt verifies password hash from MongoDB
        ← JWT token (signed, 60-minute expiry)
Browser stores token in memory
All subsequent requests include: Authorization: Bearer <token>
```

**What is JWT?**
A JSON Web Token is a self-contained credential. It contains the user's ID, email, and role, cryptographically signed by the server. The API verifies the signature on every request — no database lookup needed. It's like a signed passport: anyone can read it, but only the authority that issued it can create a valid one.

**What is BCrypt?**
Passwords are never stored in plain text. BCrypt is a hashing algorithm designed specifically for passwords — it's intentionally slow (takes ~100ms), making brute-force attacks impractical. Even if an attacker stole the database, cracking one password would take days.

---

### 7b. Dashboard Page

**What to show:**
1. Workspace list on the left sidebar
2. Projects displayed as cards
3. Click the edit (pencil) icon on a project card — edit inline
4. Create a new workspace using the `+ New Workspace` button
5. Create a new project using `+ New Project`

**Technology behind it — GraphQL:**
The frontend doesn't call REST endpoints for this page. It uses **GraphQL** — a query language that lets the client ask for exactly the data it needs.

For example, when the dashboard loads, it sends this single request:
```graphql
query {
  workspaces {
    id
    name
  }
}
```
And separately:
```graphql
query GetProjects($workspaceId: ID!) {
  projects(workspaceId: $workspaceId) {
    id
    name
    description
    status
  }
}
```

**Why GraphQL instead of REST?**
- REST: multiple round trips (`GET /workspaces`, then `GET /projects?workspaceId=x`)
- GraphQL: one request, ask for exactly what you need, nothing more

This is especially valuable as the frontend grows — no over-fetching data the UI doesn't use.

---

### 7c. Project Board (Kanban)

**URL:** Click any project → opens the board

**What to show:**
1. Four columns: To Do, In Progress, In Review, Done
2. Drag a task card from one column to another
3. Open a second browser tab with the same URL
4. In Tab 1 — drag a card
5. Watch Tab 2 update **instantly** without refreshing

**Technology behind drag-and-drop:**
The browser's native HTML5 Drag and Drop API — no external library. When a card is dropped on a new column, the UI optimistically updates (instant visual feedback) and fires a GraphQL mutation in the background.

**Technology behind real-time updates — GraphQL Subscriptions:**
```
Tab 1 (mutation)                    Tab 2 (subscribed)
  │                                      │
  POST /graphql: updateTask              │ WebSocket open
  → API updates MongoDB                  │ listening for "taskUpdated_<projectId>"
  → API publishes event to topic         │
  ← { updateTask: { status: "DONE" } }  │ ← subscription event pushed over WS
                                         │ → Apollo updates InMemoryCache
                                         │ → React re-renders the board
```

**What is a WebSocket?**
HTTP is one-directional: the client asks, the server answers, connection closes. A WebSocket is a persistent two-way connection — the server can push data to the client at any time. This is how real-time features like chat, notifications, and live dashboards work.

**Optimistic updates:**
When you drag a card, Apollo Client writes the new state to the local cache immediately before the server responds. The UI updates in ~0ms. If the server returns an error, Apollo rolls the cache back automatically. This makes the app feel instant even on slow networks.

---

---

# SLIDE 8 — Demo Part 4: Kubernetes Self-Healing

## Show What Happens When a Pod Dies

### Step 1: Watch pods in real time (in a separate terminal)
```powershell
kubectl get pods -n taskflow-dev -w
```

### Step 2: Delete a pod to simulate a crash
```powershell
# Get one of the API pod names
kubectl get pods -n taskflow-dev -l app.kubernetes.io/name=taskflow

# Delete it (replace <pod-name> with actual name)
kubectl delete pod <pod-name> -n taskflow-dev
```

**What you'll see:**
```
taskflow-f4f87c4f5-9xlhq   1/1   Running    → Terminating → (gone)
taskflow-f4f87c4f5-newpod  0/1   Pending    → Running      (new pod)
```

**The API never went down** — the other pod continued serving traffic throughout.

**What is a Deployment?**
A Kubernetes Deployment is a declaration: "I want 2 replicas of this container always running." The Deployment Controller constantly watches the cluster and reconciles actual state to desired state. If a pod disappears (crash, node failure, manual deletion), it creates a replacement automatically.

**Why 2 replicas?**
With 1 replica, any restart means downtime. With 2, one pod handles traffic while the other restarts. This is the minimum for zero-downtime operations.

---

---

# SLIDE 9 — Demo Part 5: Rolling Deployments

## Show Zero-Downtime Updates

### Watch a Helm upgrade roll out:
```powershell
# Trigger a rolling update by changing the image tag
helm upgrade taskflow helm/taskflow `
  -n taskflow-dev `
  -f helm/taskflow/values.yaml `
  --set image.tag=v2

# Watch it roll out
kubectl rollout status deployment/taskflow -n taskflow-dev
```

**What you'll see:**
```
Waiting for deployment "taskflow" rollout to finish: 1 out of 2 new replicas have been updated...
Waiting for deployment "taskflow" rollout to finish: 1 old replicas are pending termination...
deployment "taskflow" successfully rolled out
```

**How rolling updates work:**
```
Before:   [Pod v1] [Pod v1]
Step 1:   [Pod v1] [Pod v1] [Pod v2 starting...]
Step 2:   [Pod v1] [Pod v1] [Pod v2 READY]
Step 3:   [Pod v1] [Pod v2 READY]  ← old pod removed
Step 4:   [Pod v2] [Pod v2]
```

The key setting is `maxUnavailable: 0` — Kubernetes will never remove an old pod until a new pod has passed its readiness probe. The application serves traffic continuously throughout the entire update.

**One-command rollback:**
```powershell
helm rollback taskflow -n taskflow-dev
```
Helm tracks every deployment as a revision. `helm rollback` redeploys the previous revision's configuration — the same rolling mechanism in reverse. In production this is used when a bad deployment is detected by alerts.

---

---

# SLIDE 10 — Demo Part 6: Observability

## "You Can't Manage What You Can't Measure"

### Start the monitoring stack (if not running):
```powershell
kubectl get pods -n monitoring
```

---

### 10a. Structured Logs

```powershell
kubectl logs -n taskflow-dev -l app.kubernetes.io/name=taskflow --tail=20
```

**What you'll see — structured JSON logs:**
```json
{"@t":"2026-06-01T...","@mt":"HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms",
 "RequestMethod":"POST","RequestPath":"/graphql","StatusCode":200,"Elapsed":12.4,
 "MachineName":"taskflow-abc-xyz","CorrelationId":"a1b2c3d4-..."}
```

**Why JSON logs?**
Every field is a named property, not buried in a string. Log aggregators (Elastic, Loki, Azure Monitor) can filter and query by any field: "show me all requests where StatusCode=500" or "find all requests from CorrelationId=a1b2c3d4". With plain text logs you'd need fragile regex patterns.

**What is a Correlation ID?**
A unique identifier generated for every HTTP request. It's attached to every log line produced during that request. When something goes wrong, you search for the correlation ID and instantly see everything that happened — database calls, errors, timing — for that one request.

---

### 10b. Prometheus Metrics

**Access Prometheus:**
```powershell
kubectl port-forward -n monitoring svc/monitoring-kube-prometheus-prometheus 9090:9090
```
Then open: `http://localhost:9090`

**Run these queries:**

```promql
# Is the TaskFlow API up? (1 = yes, 0 = no)
up{job="taskflow"}

# Requests per second hitting the API
rate(http_requests_received_total{namespace="taskflow-dev"}[1m])

# P95 response latency
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket{namespace="taskflow-dev"}[5m]))

# Custom metric — total tasks created
taskflow_tasks_created_total
```

**What is Prometheus?**
Prometheus is a time-series database for metrics. Every 15 seconds it calls `/metrics` on the API pod and stores the numbers. This gives you historical data: "what was our request rate at 2pm last Tuesday?". Crucially, Prometheus uses a pull model — it fetches metrics from the app, rather than the app pushing to a central server. This makes it resilient: if Prometheus restarts, the apps keep running.

**Three metric types:**
- **Counter** — only goes up (total requests, total errors). You query the rate of change.
- **Gauge** — goes up and down (current active projects, memory usage)
- **Histogram** — records distribution (request duration). Lets you query percentiles: p50, p95, p99.

---

### 10c. Grafana Dashboard

**Open:** `http://grafana.local:8080` → login: `admin` / `admin`

Go to **Dashboards → Browse → TaskFlow API Operations**

**Walk through the panels:**

| Panel | What it shows |
|-------|---------------|
| Request Rate | Requests/second hitting the API right now |
| Error Rate | Percentage of 5xx responses (should be 0%) |
| P95 Latency | 95% of requests complete in under X ms |
| Active Projects | Current project count (gauge — goes up/down) |
| Tasks Created/min | Rate of task creation |
| Pod Count | Number of API pods running |
| HTTP by Status Code | Time series of 2xx, 4xx, 5xx over time |
| Latency Percentiles | P50, P95, P99 over time |

**Key point:**
The dashboard auto-provisioned. There was no manual Grafana configuration. A Kubernetes ConfigMap with a special label (`grafana_dashboard: "1"`) is all it takes — Grafana's sidecar container detects it and loads the dashboard automatically. This is **GitOps** — infrastructure configuration stored in git, applied automatically.

---

### 10d. Jaeger Distributed Tracing

**Access Jaeger:**
```
http://jaeger.local:8080
```

**How to use:**
1. Select Service: `TaskFlow.Api`
2. Click **Find Traces**
3. Click any trace to expand it

**What you'll see:**
```
POST /graphql   [45ms total]
  └─ MongoDB.find  [12ms]   collection: projects
  └─ MongoDB.find  [8ms]    collection: tasks
```

**What is distributed tracing?**
A trace records the complete journey of one request through the system — from the HTTP request arriving, through every function call, to every database query, and back. Each step is a "span" with a start time and duration.

This answers questions that logs and metrics cannot: "Why did this specific request take 2 seconds? Which database query was slow?"

**Why OTLP?**
The API sends traces using OTLP — OpenTelemetry Protocol. This is vendor-neutral: the same exporter sends to Jaeger locally, Azure Application Insights in production, or Grafana Tempo in AWS. One config line change, no code changes.

---

---

# SLIDE 11 — Demo Part 7: Security

## Security Layers Built In

### Show NetworkPolicy in action:

```powershell
# Launch a test pod (simulates an attacker gaining access to a random pod)
kubectl run attacker --image=busybox -n taskflow-dev --restart=Never -- sleep 3600

# Try to connect to MongoDB from the attacker pod
kubectl exec attacker -n taskflow-dev -- nc -zv mongodb 27017 -w 3
```

**Expected output:** Connection times out (blocked)

```powershell
# Cleanup
kubectl delete pod attacker -n taskflow-dev
```

**What is NetworkPolicy?**
Kubernetes NetworkPolicy is a firewall at the pod level. By default, all pods in a namespace can talk to each other. Once a NetworkPolicy is applied, only explicitly allowed traffic passes.

Our rules:
- MongoDB only accepts connections from pods labelled `app: taskflow-api`
- API pods can only connect outbound to MongoDB and DNS
- Any other pod trying to reach MongoDB is silently dropped

**Why does this matter?**
In a real breach scenario, an attacker who gains code execution inside one container is contained. They can't pivot to the database, can't reach other services. This is **defence in depth** — multiple independent layers of security.

---

### Show container security settings:
```powershell
kubectl get pod -n taskflow-dev -l app.kubernetes.io/name=taskflow -o jsonpath='{.items[0].spec.containers[0].securityContext}' | python -m json.tool
```

**What the output means:**

| Setting | Value | Why |
|---------|-------|-----|
| `runAsNonRoot` | true | Container cannot run as root (UID 0) |
| `runAsUser` | 1654 | Specific unprivileged user ID |
| `readOnlyRootFilesystem` | true | Container cannot write to its own filesystem |
| `allowPrivilegeEscalation` | false | Process cannot gain more privileges than it started with |
| `capabilities.drop` | ALL | All Linux kernel capabilities removed |

These settings implement the **principle of least privilege**: the container can do exactly what it needs to do and nothing more. Even if the application code has a vulnerability, the damage it can cause is severely limited.

---

---

# SLIDE 12 — Demo Part 8: Helm Environment Overlays

## Same Chart, Different Environments

```powershell
# Show what changes between dev and prod
helm template taskflow helm/taskflow -f helm/taskflow/values.yaml > dev-output.yaml
helm template taskflow helm/taskflow -f helm/taskflow/values.prod.yaml > prod-output.yaml
```

**Key differences (open both files side by side):**

| Setting | Dev (`values.yaml`) | Prod (`values.prod.yaml`) |
|---------|--------------------|-----------------------------|
| Replicas | 2 | Managed by HPA (3–8) |
| CPU request | 100m | 200m |
| Memory limit | 256Mi | 512Mi |
| Log level | Information | Information |
| HPA | Disabled | Enabled (CPU 70%) |
| PDB minAvailable | 1 | 2 |
| Environment | Production | Production |

**The single chart principle:**
One Helm chart. One set of templates. Different values files for different environments. This means dev and prod are guaranteed to run the same configuration — there's no risk of "but it worked on dev!" because a setting existed in prod that didn't exist in dev.

---

---

# SLIDE 13 — AKS: What Would Change?

## Moving from Local k3d to Real Azure AKS

The entire application is designed so that moving to AKS requires changing **only `values.prod.yaml`** — no code changes, no template changes.

| Component | Local (k3d) | Azure AKS |
|-----------|-------------|-----------|
| Cluster creation | `k3d cluster create` | `az aks create` |
| Image registry | `localhost:5050` | Azure Container Registry |
| Ingress controller | Traefik (built-in) | NGINX or Azure App Gateway |
| Domain | `taskflow.local` (hosts file) | `taskflow.yourdomain.com` (DNS) |
| Storage | local-path provisioner | Azure Managed Disks |
| Secrets | k8s Secrets (base64) | Azure Key Vault + CSI driver |
| Monitoring | Self-hosted Prometheus/Grafana | Azure Monitor + managed Grafana |
| Tracing | Jaeger | Azure Application Insights |
| Subscriptions | In-memory (single pod) | Azure Cache for Redis |

**The `kubectl` and `helm` commands are identical.** AKS is just a different endpoint. Once you have `az aks get-credentials`, your terminal is talking to Azure instead of your laptop.

---

---

# SLIDE 14 — Summary

## What We Built and Why It Matters

### The Stack
- **React + Apollo Client** — modern, type-safe frontend with real-time capabilities
- **ASP.NET Core + HotChocolate** — GraphQL API with subscriptions, JWT auth, health probes
- **MongoDB** — flexible document database, Kubernetes-native StatefulSet deployment
- **Docker** — immutable, multi-stage container builds
- **Kubernetes (k3d)** — self-healing, rolling updates, network policies, resource management
- **Helm** — repeatable, version-controlled deployments with environment overlays
- **Prometheus + Grafana** — metrics collection and visualisation, auto-provisioned dashboards
- **Jaeger + OpenTelemetry** — distributed tracing, vendor-neutral
- **Serilog** — structured JSON logging for log aggregators

### The Patterns
- Zero-downtime rolling updates
- Three-tier health probes (live/ready/startup)
- Least-privilege containers (non-root, read-only, dropped capabilities)
- Network-level isolation (NetworkPolicy)
- Real-time UI (WebSocket subscriptions + optimistic updates)
- GitOps-style observability (ConfigMap-provisioned dashboards)
- Environment overlays (same chart, dev/prod values)

### The Takeaway
> Every pattern in this project exists because something goes wrong in production without it. The health probes, the NetworkPolicy, the rolling update strategy, the structured logs — these aren't nice-to-haves. They're what separates an application that works on a demo from one that works at 2am when no one is watching.

---

---

# APPENDIX — Quick Reference Commands

## Cluster Management
```powershell
# Start cluster
k3d cluster create --config k3d-config.yaml

# Fix kubectl connection after restart
kubectl config set-cluster k3d-taskflow --server="https://127.0.0.1:65165"
kubectl config set-cluster k3d-taskflow --insecure-skip-tls-verify=true

# Stop cluster (preserves data)
k3d cluster stop taskflow

# Start cluster again
k3d cluster start taskflow

# Delete cluster entirely
k3d cluster delete taskflow
```

## Build and Deploy
```powershell
# Build and push API image
make docker-build
make docker-push

# Build and push frontend image
make frontend-build
make frontend-push

# Deploy/upgrade Helm chart
make deploy

# One command: build + push + deploy
make release

# Roll back to previous version
make rollback
```

## Kubernetes Inspection
```powershell
# See all resources in the namespace
kubectl get all -n taskflow-dev

# Watch pods in real time
kubectl get pods -n taskflow-dev -w

# Tail API logs
kubectl logs -n taskflow-dev -l app.kubernetes.io/name=taskflow --follow --tail=50

# Describe a pod (events, resource usage, probe status)
kubectl describe pod <pod-name> -n taskflow-dev

# Check ingress routing
kubectl get ingress -n taskflow-dev
```

## Helm
```powershell
# See all Helm releases
helm list -n taskflow-dev

# See revision history
helm history taskflow -n taskflow-dev

# Roll back to revision 2
helm rollback taskflow 2 -n taskflow-dev

# Dry run (preview what would change)
helm upgrade taskflow helm/taskflow -n taskflow-dev -f helm/taskflow/values.yaml --dry-run
```

## URLs (once cluster is running)
| URL | What it shows |
|-----|---------------|
| `http://app.local:8080` | React frontend |
| `http://taskflow.local:8080/graphql` | GraphQL IDE (Banana Cake Pop) |
| `http://taskflow.local:8080/health/ready` | Health check |
| `http://taskflow.local:8080/metrics` | Prometheus metrics (raw) |
| `http://grafana.local:8080` | Grafana dashboards (admin/admin) |
| `http://jaeger.local:8080` | Jaeger tracing UI |

## Test Credentials
| | Value |
|-|-------|
| Email | `souvika@taskflow.local` |
| Password | `Test1234!` |

---

---

# APPENDIX B — MongoDB in Production: Should It Be Inside Kubernetes?

## The Short Answer

**In this training project, MongoDB runs as a Kubernetes StatefulSet inside the cluster. In real production, it almost always runs outside Kubernetes as a managed service.**

This appendix explains why, and shows exactly what changes in the code to make the switch.

---

## What We Did (Training) vs What Production Looks Like

```
TRAINING (this project)                    PRODUCTION (real AKS)

┌─────────────────────────────┐            ┌──────────────────────────┐
│   Kubernetes Cluster        │            │   Kubernetes Cluster     │
│                             │            │                          │
│   API pods ──────────────►  │            │   API pods               │
│                  mongodb-0  │            │        │                 │
│                  (StatefulSet│            └────────│─────────────────┘
│                  + 1Gi PVC) │                     │ connection string
└─────────────────────────────┘                     ▼
                                           ┌──────────────────────────┐
                                           │  Azure Cosmos DB         │
                                           │  or MongoDB Atlas        │
                                           │  (fully managed)         │
                                           └──────────────────────────┘
```

---

## Why Not Run MongoDB Inside Kubernetes in Production?

| Problem | What happens |
|---------|-------------|
| **You own operations** | Backups, failover, replication, patching, security — all your responsibility |
| **StatefulSets are hard** | If a node dies, the PersistentVolumeClaim may get stuck on that node — Kubernetes won't remount it automatically |
| **No built-in HA** | Our single `mongodb-0` pod is a single point of failure. A proper replica set needs 3 pods + a MongoDB Operator to manage elections |
| **No point-in-time recovery** | You'd need to build your own backup solution from scratch |
| **Storage is cluster-tied** | Delete the cluster carelessly and you lose your data |
| **Engineering cost** | The time spent operating a self-hosted database almost always exceeds the cost of a managed service |

> **Rule of thumb used in industry:**
> Stateless workloads (APIs, frontends) belong in Kubernetes.
> Stateful workloads (databases, queues, caches) belong outside it — in managed services.

---

## The Three Production Options

### Option 1 — Azure Cosmos DB for MongoDB (Recommended for AKS)

Microsoft's fully managed database with a MongoDB-compatible API. Native integration with Azure networking, RBAC, and Key Vault.

**Pros:** No infrastructure to manage, automatic backups, built-in geo-replication, private endpoints, SLA-backed uptime
**Cons:** Slightly different behaviour from native MongoDB on some advanced features (transactions, aggregation edge cases)

**Pricing:** Pay per Request Unit (RU) — scales to zero when idle, or provisioned throughput for predictable workloads

---

### Option 2 — MongoDB Atlas

MongoDB's own managed cloud service, available on Azure, AWS, and GCP.

**Pros:** 100% MongoDB-compatible (it's the real thing), excellent tooling, free tier available, can run on Azure in the same region as your AKS cluster
**Cons:** Third-party service (not Azure-native), network peering setup required for private connectivity

**Pricing:** From ~$60/month for M10 cluster (3-node replica set, 2GB RAM)

---

### Option 3 — MongoDB Community Operator (Still in Kubernetes)

If you genuinely must run MongoDB in-cluster, use the [MongoDB Community Operator](https://github.com/mongodb/mongodb-kubernetes-operator). It manages replica sets, rolling upgrades, and TLS automatically.

**Pros:** Stays in Kubernetes, no external dependency
**Cons:** Significant operational overhead — you still own backups, storage, and recovery. Only choose this if you have a dedicated platform team.

---

## What Changes in the Code

This is the important part. **Almost nothing changes** — only the connection string in `values.prod.yaml`.

The `MongoDB.Driver` in .NET does not care whether it connects to a pod in the same cluster, Cosmos DB, or Atlas. It reads the connection string. The repository pattern we built (`IProjectRepository`, `ITaskRepository`, etc.) means the database location is completely abstracted away from application code.

---

### Change 1 — `helm/taskflow/values.prod.yaml`

```yaml
# BEFORE (in-cluster StatefulSet)
mongodb:
  connectionString: "mongodb://admin:mongopassword@mongodb:27017/taskflow?authSource=admin"
  databaseName: taskflow

# AFTER — Azure Cosmos DB for MongoDB
mongodb:
  connectionString: "mongodb://taskflow:YOUR_KEY@taskflow.mongo.cosmos.azure.com:10255/taskflow?ssl=true&replicaSet=globaldb&retrywrites=false"
  databaseName: taskflow

# AFTER — MongoDB Atlas
mongodb:
  connectionString: "mongodb+srv://taskflowuser:YOUR_PASSWORD@cluster0.abc123.mongodb.net/taskflow?retryWrites=true&w=majority"
  databaseName: taskflow
```

> The connection string is injected into the pod as a Kubernetes Secret environment variable (`MongoDb__ConnectionString`). In real AKS this secret value comes from Azure Key Vault at deploy time — it is never committed to git.

---

### Change 2 — Remove the MongoDB StatefulSet from Helm

When using a managed service, you no longer deploy MongoDB into the cluster at all. Comment out or remove these files:

```
helm/taskflow/templates/mongodb-statefulset.yaml  ← delete or disable
helm/taskflow/templates/mongodb-service.yaml      ← delete or disable
k8s/mongodb/                                       ← no longer applied
```

Or guard them with a values flag:

```yaml
# values.prod.yaml
mongodb:
  deploy: false   # don't create StatefulSet — use external managed service
```

```yaml
# helm/taskflow/templates/mongodb-statefulset.yaml
{{- if .Values.mongodb.deploy }}
apiVersion: apps/v1
kind: StatefulSet
...
{{- end }}
```

---

### Change 3 — Remove the MongoDB NetworkPolicy restriction

The current NetworkPolicy blocks everything except the API pods from reaching `mongodb:27017`. When MongoDB is external, this rule no longer applies — the egress policy on API pods should instead allow outbound HTTPS/TCP to the managed service endpoint.

```yaml
# Updated API egress policy for external MongoDB
egress:
  - ports:
      - protocol: TCP
        port: 10255   # Cosmos DB for MongoDB port
      - protocol: TCP
        port: 27017   # Atlas port (if using VNet peering)
      - protocol: TCP
        port: 443     # HTTPS for Cosmos DB management
  - ports:
      - protocol: UDP
        port: 53      # DNS — always required
```

---

### Change 4 — Health Check Connection String

The readiness and startup probes check MongoDB connectivity. No code change needed — `AspNetCore.HealthChecks.MongoDb` reads the same `MongoDb__ConnectionString` environment variable. It will automatically check the external service instead.

```csharp
// Program.cs — unchanged
builder.Services.AddHealthChecks()
    .AddMongoDb(
        sp => sp.GetRequiredService<IMongoClient>(),
        name: "mongodb",
        tags: ["ready", "startup"]);
```

---

## Side-by-Side Summary

| | In-Cluster StatefulSet (this project) | Managed Service (production) |
|--|--------------------------------------|------------------------------|
| **Setup** | Automatic via Helm | Create service in Azure Portal / Atlas UI |
| **Code changes** | None | None |
| **Config changes** | None | Connection string in `values.prod.yaml` |
| **Backups** | You build it | Automatic (daily + PITR) |
| **High availability** | Single pod (SPOF) | Multi-region, automatic failover |
| **Scaling** | Manual (`replicas:`) | Automatic (throughput-based) |
| **Security** | NetworkPolicy + k8s Secret | Private endpoint + Azure AD / Key Vault |
| **Cost** | Storage + node CPU/RAM | ~$60–200/month depending on size |
| **Who operates it** | You | The cloud provider |

---

## Why We Used StatefulSet in This Project

Running MongoDB as a StatefulSet was a deliberate training choice. It taught:

- How StatefulSets differ from Deployments (stable identity, ordered scaling, per-pod PVCs)
- How PersistentVolumeClaims and storage provisioners work
- How headless services give databases stable DNS entries
- How NetworkPolicy enforces pod-level firewall rules

All of these concepts apply to any stateful workload in Kubernetes — not just MongoDB. The patterns transfer directly to running Redis, RabbitMQ, Elasticsearch, or any database in-cluster when that genuinely makes sense.

The architecture was also designed from day one so the switch to a managed service requires changing **one line** — the connection string. That's the right way to build it.
