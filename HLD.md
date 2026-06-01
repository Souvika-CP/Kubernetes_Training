# TaskFlow — High Level Design (HLD)

## 1. System Overview

TaskFlow is a cloud-native, multi-tenant project and task management platform. It is designed as a production-grade reference implementation for learning how to build, containerise, and operate a full-stack application on Kubernetes (k3d locally, AKS in production).

The system allows users to organise work into Workspaces → Projects → Tasks, with real-time updates delivered via GraphQL subscriptions. All components run as separate containers inside a Kubernetes cluster, communicating over an internal network.

---

## 2. Architecture Diagram

```
┌───────────────────────────────────────────────────────────────────────────┐
│                         Developer's Machine                               │
│                                                                           │
│   Browser / API Client                                                    │
│       │  HTTP :8080                                                       │
│       ▼                                                                   │
│  ┌──────────────────────────────────────────────────────────────────┐    │
│  │                   k3d Cluster (3 nodes)                          │    │
│  │                                                                  │    │
│  │  ┌─────────────────────────────────────────────────────────┐    │    │
│  │  │  Namespace: taskflow-dev                                │    │    │
│  │  │                                                         │    │    │
│  │  │  Traefik Ingress                                        │    │    │
│  │  │  ├─ app.local/          → taskflow-frontend :80        │    │    │
│  │  │  ├─ app.local/graphql   → taskflow-api :8080           │    │    │
│  │  │  ├─ app.local/api       → taskflow-api :8080           │    │    │
│  │  │  ├─ app.local/auth      → taskflow-api :8080           │    │    │
│  │  │  └─ taskflow.local/     → taskflow-api :8080           │    │    │
│  │  │                                                         │    │    │
│  │  │  ┌─────────────────┐    ┌─────────────────────────┐    │    │    │
│  │  │  │ Frontend (nginx) │    │  API (ASP.NET Core 10)  │    │    │    │
│  │  │  │  React + Apollo  │    │  2 replicas             │    │    │    │
│  │  │  │  1 replica       │    │  HotChocolate GraphQL   │    │    │    │
│  │  │  │  port 80         │    │  JWT Auth               │    │    │    │
│  │  │  └─────────────────┘    │  Prometheus metrics      │    │    │    │
│  │  │                         │  OTEL tracing            │    │    │    │
│  │  │                         │  port 8080               │    │    │    │
│  │  │                         └────────────┬────────────┘    │    │    │
│  │  │                                      │ ClusterIP        │    │    │
│  │  │                                      ▼                  │    │    │
│  │  │                         ┌────────────────────────┐      │    │    │
│  │  │                         │  MongoDB (StatefulSet)  │      │    │    │
│  │  │                         │  mongodb-0             │      │    │    │
│  │  │                         │  1Gi PersistentVolume  │      │    │    │
│  │  │                         │  port 27017            │      │    │    │
│  │  │                         └────────────────────────┘      │    │    │
│  │  └─────────────────────────────────────────────────────────┘    │    │
│  │                                                                  │    │
│  │  ┌─────────────────────────────────────────────────────────┐    │    │
│  │  │  Namespace: monitoring                                  │    │    │
│  │  │  Prometheus  │  Grafana  │  Jaeger                      │    │    │
│  │  └─────────────────────────────────────────────────────────┘    │    │
│  └──────────────────────────────────────────────────────────────────┘    │
│                                                                           │
│  Local Docker Registry: localhost:5050 (taskflow-registry:5000 in-cluster)│
└───────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Component Descriptions

### 3.1 React Frontend (`taskflow-frontend`)
- **Technology:** React 18, Vite 8, TypeScript, Apollo Client v4, Tailwind CSS v4
- **Served by:** nginx:alpine static file server
- **Responsibilities:**
  - Login page with JWT acquisition
  - Dashboard page — workspace/project management (CRUD)
  - Project Board page — Kanban view with drag-and-drop and real-time task updates
- **Communication:** GraphQL over HTTP (queries/mutations) and WebSocket (subscriptions) — all via Traefik to the API
- **Build pattern:** Multi-stage Docker — `node:20-alpine` builds the React app, `nginx:alpine` serves the static `dist/`

### 3.2 .NET API (`taskflow`)
- **Technology:** ASP.NET Core 10, HotChocolate 16 (GraphQL), Serilog, prometheus-net, OpenTelemetry
- **Responsibilities:**
  - GraphQL API: queries, mutations, real-time subscriptions (WebSocket)
  - REST endpoints: `/api/projects`, `/api/tasks`, `/auth/register`, `/auth/token`
  - Health probes: `/health/live`, `/health/ready`, `/health/startup`
  - Metrics scrape endpoint: `/metrics`
- **Scaling:** 2 replicas behind a ClusterIP Service with RollingUpdate strategy (zero-downtime deploys)
- **Build pattern:** Multi-stage Docker — `sdk:10.0-alpine` builds, `aspnet:10.0-alpine` runs

### 3.3 MongoDB (`mongodb`)
- **Technology:** MongoDB 7 as a Kubernetes StatefulSet
- **Responsibilities:** Persistent document storage for all domain entities
- **Storage:** 1Gi PersistentVolumeClaim per pod (local-path provisioner in k3d; Azure Disk on AKS)
- **Access:** Headless ClusterIP service at `mongodb:27017` — reachable by API pods only (NetworkPolicy enforced)

### 3.4 Monitoring Stack (`monitoring` namespace)
| Component | Role |
|-----------|------|
| Prometheus | Scrapes `/metrics` from API pods every 15s; stores time-series data |
| Grafana | Dashboards — `TaskFlow Operations` dashboard pre-provisioned via ConfigMap |
| Jaeger | Distributed traces received via OTLP; accessible at `http://jaeger.local:8080` |
| kube-state-metrics | Kubernetes object metrics (pod counts, deployment status) |
| node-exporter | OS-level metrics (CPU, memory, disk I/O) per k3d node |

---

## 4. Data Flow

### 4.1 User Login
```
Browser → POST /auth/token → API → BCrypt verify password → MongoDB
       ← JWT (signed, 60 min expiry)
```

### 4.2 GraphQL Query (e.g., list projects)
```
Apollo Client → HTTP POST /graphql (Authorization: Bearer <jwt>)
             → Traefik → API pod
             → HotChocolate resolves query
             → IProjectRepository.GetByWorkspaceIdAsync()
             → MongoDB (ClusterIP, port 27017)
             ← JSON response
```

### 4.3 GraphQL Mutation with Real-Time Subscription
```
Client A (mutation)                  Client B (subscribed)
  │                                       │
  POST /graphql: updateTask               │ WS connection open
  → API pod                               │ listening on "taskUpdated_<projectId>"
  → MongoDB update                        │
  → ITopicEventSender.SendAsync(          │
      "taskUpdated_<projectId>", task)    │
  ← { data: { updateTask: {...} } }       │ ← subscription event pushed to client
```

### 4.4 Optimistic UI Update (Frontend)
```
User drags task card to new column
→ Apollo writes optimistic result to InMemoryCache immediately (UI updates instantly)
→ mutation fires to server
→ server response replaces optimistic entry in cache
→ subscription event from server (via WebSocket) confirms the change to all other connected clients
```

---

## 5. Networking

### 5.1 Ingress Routing
```
Host: app.local       (unified frontend + API)
  /graphql  → taskflow-api:8080
  /api      → taskflow-api:8080
  /auth     → taskflow-api:8080
  /health   → taskflow-api:8080
  /metrics  → taskflow-api:8080
  /         → taskflow-frontend:80

Host: taskflow.local  (API-only, for direct development/testing)
  /         → taskflow-api:8080
```

### 5.2 NetworkPolicy
- **MongoDB ingress:** Only pods labelled `app: taskflow-api` can connect on port 27017
- **API egress:** API pods can only initiate connections to MongoDB (27017) and kube-dns (53)
- All other pod-to-pod traffic within the namespace is blocked by default once a policy is applied

### 5.3 Port Mapping (Local Development)
| External | Internal | Purpose |
|----------|----------|---------|
| `localhost:8080` | cluster port 80 | HTTP ingress (Traefik) |
| `localhost:8443` | cluster port 443 | HTTPS ingress |
| `localhost:5050` | — | Docker image registry |
| `localhost:65165` | — | Kubernetes API server |

---

## 6. Security

| Layer | Control |
|-------|---------|
| Authentication | JWT Bearer tokens (HMAC-SHA256, 60-minute expiry) |
| Password storage | BCrypt (adaptive hashing, random salt) |
| Container runtime | Non-root user (`app`), read-only root filesystem, all Linux capabilities dropped |
| Secret storage | Kubernetes Secrets (base64) — AKS production uses Azure Key Vault |
| Network isolation | NetworkPolicy: API ↔ MongoDB only; all other ingress/egress blocked |
| RBAC | Dedicated ServiceAccount for API pod with minimal permissions |
| Schema security | `[GraphQLIgnore]` on `User.PasswordHash` — never exposed via GraphQL |

---

## 7. Observability

```
API Pod
  │
  ├─ Structured JSON logs → stdout → kubectl logs / log aggregator
  │   (Serilog + CorrelationIdMiddleware)
  │
  ├─ Prometheus metrics → /metrics
  │   (prometheus-net.AspNetCore + custom AppMetrics)
  │   └─ Prometheus scrapes every 15s (ServiceMonitor CRD)
  │       └─ Grafana dashboard renders panels
  │
  └─ OpenTelemetry traces → OTLP → Jaeger:4317
      (auto-instrumented HTTP + MongoDB spans)
```

---

## 8. Deployment Pipeline

```
Code change
    │
    ▼
make docker-build         docker build -t localhost:5050/taskflow:v2 .
    │
    ▼
make docker-push          docker push + k3d image import
    │
    ▼
make deploy               helm upgrade --install taskflow helm/taskflow \
                            -n taskflow-dev --set image.tag=v2
    │
    ▼
Kubernetes rolling update  replaces pods one at a time
                           maxUnavailable=0, maxSurge=1
                           waits for readiness probe before removing old pod
    │
    ▼
make rollback (if needed)  helm rollback taskflow -n taskflow-dev
```

---

## 9. AKS Translation Map

Every local component has a direct AKS equivalent. The Helm chart is the single point of change.

| Local (k3d) | Real AKS |
|-------------|----------|
| k3d cluster | `az aks create` |
| `localhost:5050` registry | Azure Container Registry (ACR) |
| k3d load balancer | Azure Load Balancer (public IP auto-provisioned) |
| Traefik IngressClass | NGINX Ingress Controller or Azure Application Gateway |
| `local-path` storage provisioner | Azure Disk (managed disks, StorageClass `managed-csi`) |
| k8s Secrets | Azure Key Vault + Secrets Store CSI Driver |
| Prometheus + Grafana | Same charts, or Azure Monitor managed Prometheus |
| Jaeger | Azure Application Insights (OTLP endpoint — same exporter config) |
| In-memory subscriptions | Azure Cache for Redis (swap `AddInMemorySubscriptions` → `AddRedisSubscriptions`) |
