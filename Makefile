SHELL := bash
export PATH := /c/ProgramData/chocolatey/bin:$(PATH)

# ─── Variables ────────────────────────────────────────────────────────────────
REGISTRY_HOST    := localhost:5050
REGISTRY_CLUSTER := taskflow-registry:5000
IMAGE_NAME       := taskflow
IMAGE_TAG        ?= v1
NAMESPACE        := taskflow-dev
HELM_RELEASE     := taskflow
HELM_CHART       := helm/taskflow
HELM_VALUES      := helm/taskflow/values.yaml
HELM_SECRETS     := helm/taskflow/values.local.yaml
SOLUTION         := TaskFlow.slnx
K3D_CLUSTER      := taskflow

IMAGE_LOCAL   := $(REGISTRY_HOST)/$(IMAGE_NAME):$(IMAGE_TAG)
IMAGE_CLUSTER := $(REGISTRY_CLUSTER)/$(IMAGE_NAME):$(IMAGE_TAG)

FRONTEND_NAME    := taskflow-frontend
FRONTEND_TAG     ?= v1
FRONTEND_LOCAL   := $(REGISTRY_HOST)/$(FRONTEND_NAME):$(FRONTEND_TAG)
FRONTEND_CLUSTER := $(REGISTRY_CLUSTER)/$(FRONTEND_NAME):$(FRONTEND_TAG)

# ─── Help ─────────────────────────────────────────────────────────────────────
.PHONY: help
help:
	@echo "TaskFlow - available targets:"
	@echo ""
	@echo "  Local dev:"
	@echo "    dev           Start local stack (Docker Compose + hot reload)"
	@echo "    dev-down      Stop local stack"
	@echo ""
	@echo "  .NET:"
	@echo "    build         dotnet build"
	@echo "    test          dotnet test"
	@echo ""
	@echo "  Docker:"
	@echo "    docker-build  Build and tag the image (IMAGE_TAG=$(IMAGE_TAG))"
	@echo "    docker-push   Push image to local k3d registry"
	@echo ""
	@echo "  Kubernetes / Helm:"
	@echo "    deploy        helm upgrade --install to k3d"
	@echo "    rollback      Roll back to the previous Helm revision"
	@echo "    status        kubectl get all -n $(NAMESPACE)"
	@echo "    logs          Tail API pod logs (Ctrl+C to stop)"
	@echo "    clean         Uninstall Helm release and delete namespace"
	@echo "    kubeconfig    Fix k3d kubeconfig (run after cluster restart)"
	@echo ""
	@echo "  Shortcuts:"
	@echo "    release       docker-build + docker-push + deploy"

# ─── Local dev ────────────────────────────────────────────────────────────────
.PHONY: dev
dev:
	docker compose up --build

.PHONY: dev-down
dev-down:
	docker compose down

# ─── .NET ─────────────────────────────────────────────────────────────────────
.PHONY: build
build:
	dotnet build $(SOLUTION)

.PHONY: test
test:
	dotnet test $(SOLUTION) --verbosity normal

# ─── Docker ───────────────────────────────────────────────────────────────────
.PHONY: docker-build
docker-build:
	docker build -t $(IMAGE_LOCAL) .

.PHONY: docker-push
docker-push:
	docker push $(IMAGE_LOCAL)
	docker tag $(IMAGE_LOCAL) $(IMAGE_CLUSTER)
	k3d image import $(IMAGE_CLUSTER) -c $(K3D_CLUSTER)

.PHONY: frontend-build
frontend-build:
	docker build -t $(FRONTEND_LOCAL) ./frontend

.PHONY: frontend-push
frontend-push:
	docker push $(FRONTEND_LOCAL)
	docker tag $(FRONTEND_LOCAL) $(FRONTEND_CLUSTER)
	k3d image import $(FRONTEND_CLUSTER) -c $(K3D_CLUSTER)

.PHONY: frontend-release
frontend-release: frontend-build frontend-push deploy

# ─── Kubernetes helpers ───────────────────────────────────────────────────────
# Run after cluster restart — dynamically finds the API server port from Docker
.PHONY: kubeconfig
kubeconfig:
	$(eval PORT := $(shell docker port k3d-taskflow-serverlb 6443 2>/dev/null | cut -d: -f2))
	kubectl config set-cluster k3d-taskflow --server="https://127.0.0.1:$(PORT)"
	kubectl config set-cluster k3d-taskflow --insecure-skip-tls-verify=true

# ─── Helm ─────────────────────────────────────────────────────────────────────
.PHONY: deploy
deploy: kubeconfig
	helm upgrade --install $(HELM_RELEASE) $(HELM_CHART) \
		-n $(NAMESPACE) --create-namespace \
		-f $(HELM_VALUES) \
		-f $(HELM_SECRETS) \
		--set image.tag=$(IMAGE_TAG)

.PHONY: rollback
rollback: kubeconfig
	helm rollback $(HELM_RELEASE) -n $(NAMESPACE)

.PHONY: status
status: kubeconfig
	kubectl get all -n $(NAMESPACE)

.PHONY: logs
logs: kubeconfig
	kubectl logs -n $(NAMESPACE) -l app=taskflow-api --follow --tail=100

.PHONY: clean
clean: kubeconfig
	helm uninstall $(HELM_RELEASE) -n $(NAMESPACE) || true
	kubectl delete namespace $(NAMESPACE) || true

# ─── Shortcuts ────────────────────────────────────────────────────────────────
.PHONY: release
release: docker-build docker-push deploy

.DEFAULT_GOAL := help
