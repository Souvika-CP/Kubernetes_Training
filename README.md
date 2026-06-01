# TaskFlow

A production-grade, cloud-native task management application built as a hands-on training project for learning **Kubernetes**, **GraphQL**, **MongoDB**, and **Azure AKS**.

## Stack

| Layer | Technology |
|-------|------------|
| Frontend | React 18 + TypeScript + Apollo Client v4 + Vite |
| Backend | ASP.NET Core 10 + HotChocolate GraphQL |
| Database | MongoDB 7 (Kubernetes StatefulSet) |
| Container runtime | Docker (multi-stage builds) |
| Orchestration | Kubernetes via k3d (local) / Azure AKS (production) |
| Packaging | Helm |
| Observability | Prometheus + Grafana + Jaeger + Serilog |

## Quick Start

```powershell
# 1. Clone
git clone https://github.com/Souvika-CP/Kubernetes_Training.git
cd Kubernetes_Training

# 2. Create cluster
k3d cluster create --config k3d-config.yaml
kubectl config set-cluster k3d-taskflow --server="https://127.0.0.1:65165"
kubectl config set-cluster k3d-taskflow --insecure-skip-tls-verify=true

# 3. Build and deploy
make docker-build && make docker-push
make frontend-build && make frontend-push
make deploy

# 4. Open http://app.local:8080
```

## Documentation

| File | Contents |
|------|----------|
| [SETUP.md](SETUP.md) | **Start here** — all dependencies and step-by-step installation |
| [HLD.md](HLD.md) | High Level Design — architecture, data flow, component descriptions |
| [LLD.md](LLD.md) | Low Level Design — entity model, API contracts, Kubernetes specs |
| [PROJECT_SUMMARY.md](PROJECT_SUMMARY.md) | Technology choices, reasoning, and best practices |
| [DEMO_GUIDE.md](DEMO_GUIDE.md) | Step-by-step demo walkthrough with talking points |
| [PROJECT_NOTES.md](PROJECT_NOTES.md) | Deep-dive explanations for every task and decision |
| [SPEC.md](SPEC.md) | Original project specification |
| [TASKS.md](TASKS.md) | Full task list across all 11 phases |

## Learning Path

This project covers 11 phases across 41 tasks — from installing Docker to deploying a real-time GraphQL application with full observability on Kubernetes. See [SETUP.md](SETUP.md) for the complete learning path.
