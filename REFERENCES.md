# TaskFlow — Reference Links

All official documentation, download pages, and key resources used to build this project.

---

## Tools — Download & Install

| Tool | Download / Install | Documentation |
|------|--------------------|---------------|
| Docker Desktop | https://www.docker.com/products/docker-desktop | https://docs.docker.com |
| k3d | https://github.com/k3d-io/k3d/releases | https://k3d.io/stable |
| k3s (what k3d runs) | — | https://docs.k3s.io |
| kubectl | https://dl.k8s.io/release/v1.29.0/bin/windows/amd64/kubectl.exe | https://kubernetes.io/docs/reference/kubectl |
| Helm | https://helm.sh/docs/intro/install | https://helm.sh/docs |
| .NET 10 SDK | https://dotnet.microsoft.com/download/dotnet/10.0 | https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview |
| Node.js LTS | https://nodejs.org | https://nodejs.org/en/docs |
| Git | https://git-scm.com/download/win | https://git-scm.com/doc |
| Azure CLI | https://aka.ms/installazurecliwindows | https://learn.microsoft.com/en-us/cli/azure |

---

## Backend — .NET & C#

| Technology | Official Docs | Key Reference |
|-----------|--------------|---------------|
| ASP.NET Core 10 | https://learn.microsoft.com/en-us/aspnet/core | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis |
| ASP.NET Core Minimal APIs | https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api | — |
| ASP.NET Core Health Checks | https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks | — |
| ASP.NET Core JWT Auth | https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn | — |
| HotChocolate (GraphQL) | https://chillicream.com/docs/hotchocolate | https://chillicream.com/docs/hotchocolate/v16 |
| HotChocolate Subscriptions | https://chillicream.com/docs/hotchocolate/v16/defining-a-schema/subscriptions | — |
| HotChocolate Filtering & Sorting | https://chillicream.com/docs/hotchocolate/v16/fetching-data/filtering | — |
| MongoDB .NET Driver | https://www.mongodb.com/docs/drivers/csharp/current | https://www.mongodb.com/docs/drivers/csharp/current/quick-start |
| BCrypt.Net-Next | https://github.com/BcryptNet/bcrypt.net | — |
| Serilog | https://serilog.net | https://github.com/serilog/serilog-aspnetcore |
| Serilog Compact JSON Formatter | https://github.com/serilog/serilog-formatting-compact | — |
| prometheus-net | https://github.com/prometheus-net/prometheus-net | — |
| OpenTelemetry .NET | https://opentelemetry.io/docs/languages/net | https://github.com/open-telemetry/opentelemetry-dotnet |
| OpenTelemetry OTLP Exporter | https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol | — |
| MongoDB OTel DiagnosticSources | https://github.com/jbogard/MongoDB.Driver.Core.Extensions.DiagnosticSources | — |

---

## Frontend — React & TypeScript

| Technology | Official Docs | Key Reference |
|-----------|--------------|---------------|
| React 18 | https://react.dev | https://react.dev/learn |
| React Router v6 | https://reactrouter.com/en/main | https://reactrouter.com/en/main/start/tutorial |
| Vite 8 | https://vite.dev | https://vite.dev/guide |
| TypeScript 5 | https://www.typescriptlang.org/docs | https://www.typescriptlang.org/tsconfig |
| Apollo Client v4 | https://www.apollographql.com/docs/react | https://www.apollographql.com/docs/react/get-started |
| Apollo Client — Subscriptions | https://www.apollographql.com/docs/react/data/subscriptions | — |
| Apollo Client — Optimistic UI | https://www.apollographql.com/docs/react/performance/optimistic-ui | — |
| graphql-ws | https://github.com/enisdenjo/graphql-ws | — |
| GraphQL Code Generator | https://the-guild.dev/graphql/codegen | https://the-guild.dev/graphql/codegen/docs/getting-started |
| Tailwind CSS v4 | https://tailwindcss.com/docs | https://tailwindcss.com/blog/tailwindcss-v4 |
| Inter Font | https://rsms.me/inter | https://fonts.google.com/specimen/Inter |

---

## Docker

| Topic | Reference |
|-------|-----------|
| Multi-stage builds | https://docs.docker.com/build/building/multi-stage |
| Dockerfile best practices | https://docs.docker.com/build/building/best-practices |
| .dockerignore | https://docs.docker.com/build/concepts/context/#dockerignore-files |
| nginx:alpine image | https://hub.docker.com/_/nginx |
| node:alpine image | https://hub.docker.com/_/node |
| mcr.microsoft.com/dotnet/aspnet | https://mcr.microsoft.com/en-us/artifact/mar/dotnet/aspnet |
| Docker Compose | https://docs.docker.com/compose |

---

## Kubernetes

| Topic | Reference |
|-------|-----------|
| Kubernetes concepts (overview) | https://kubernetes.io/docs/concepts |
| Deployments | https://kubernetes.io/docs/concepts/workloads/controllers/deployment |
| StatefulSets | https://kubernetes.io/docs/concepts/workloads/controllers/statefulset |
| Services | https://kubernetes.io/docs/concepts/services-networking/service |
| Ingress | https://kubernetes.io/docs/concepts/services-networking/ingress |
| ConfigMaps | https://kubernetes.io/docs/concepts/configuration/configmap |
| Secrets | https://kubernetes.io/docs/concepts/configuration/secret |
| Health probes (liveness/readiness/startup) | https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes |
| ResourceQuota | https://kubernetes.io/docs/concepts/policy/resource-quotas |
| LimitRange | https://kubernetes.io/docs/concepts/policy/limit-range |
| PodDisruptionBudget | https://kubernetes.io/docs/concepts/workloads/pods/disruptions |
| HorizontalPodAutoscaler | https://kubernetes.io/docs/tasks/run-application/horizontal-pod-autoscale |
| NetworkPolicy | https://kubernetes.io/docs/concepts/services-networking/network-policies |
| PersistentVolumes | https://kubernetes.io/docs/concepts/storage/persistent-volumes |
| Pod Security Context | https://kubernetes.io/docs/tasks/configure-pod-container/security-context |
| ServiceAccount | https://kubernetes.io/docs/concepts/security/service-accounts |
| RBAC | https://kubernetes.io/docs/reference/access-authn-authz/rbac |

---

## Helm

| Topic | Reference |
|-------|-----------|
| Helm documentation | https://helm.sh/docs |
| Chart structure | https://helm.sh/docs/topics/charts |
| Template guide | https://helm.sh/docs/chart_template_guide |
| Values files | https://helm.sh/docs/chart_template_guide/values_files |
| Built-in objects | https://helm.sh/docs/chart_template_guide/builtin_objects |
| Helm rollback | https://helm.sh/docs/helm/helm_rollback |

---

## Observability

| Technology | Official Docs | Key Reference |
|-----------|--------------|---------------|
| Prometheus | https://prometheus.io/docs/introduction/overview | https://prometheus.io/docs/querying/basics |
| PromQL | https://prometheus.io/docs/prometheus/latest/querying/basics | — |
| Grafana | https://grafana.com/docs/grafana/latest | — |
| Grafana Dashboard provisioning | https://grafana.com/docs/grafana/latest/administration/provisioning/#dashboards | — |
| kube-prometheus-stack (Helm chart) | https://github.com/prometheus-community/helm-charts/tree/main/charts/kube-prometheus-stack | — |
| Prometheus Operator | https://prometheus-operator.dev/docs/getting-started/introduction | — |
| ServiceMonitor CRD | https://prometheus-operator.dev/docs/api-reference/api/#monitoring.coreos.com/v1.ServiceMonitor | — |
| Jaeger | https://www.jaegertracing.io/docs | — |
| OpenTelemetry | https://opentelemetry.io/docs | — |
| OTLP (OpenTelemetry Protocol) | https://opentelemetry.io/docs/specs/otlp | — |

---

## Azure / AKS

| Topic | Reference |
|-------|-----------|
| AKS overview | https://learn.microsoft.com/en-us/azure/aks/intro-kubernetes |
| AKS quickstart | https://learn.microsoft.com/en-us/azure/aks/learn/quick-kubernetes-deploy-cli |
| Azure Container Registry | https://learn.microsoft.com/en-us/azure/container-registry/container-registry-intro |
| Azure Key Vault | https://learn.microsoft.com/en-us/azure/key-vault/general/overview |
| Secrets Store CSI Driver (Key Vault) | https://learn.microsoft.com/en-us/azure/aks/csi-secrets-store-driver |
| Azure Workload Identity | https://learn.microsoft.com/en-us/azure/aks/workload-identity-overview |
| Azure Cosmos DB for MongoDB | https://learn.microsoft.com/en-us/azure/cosmos-db/mongodb/introduction |
| Azure Monitor managed Prometheus | https://learn.microsoft.com/en-us/azure/azure-monitor/essentials/prometheus-metrics-overview |
| Azure Application Insights (OTel) | https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable |
| AKS Cluster Autoscaler | https://learn.microsoft.com/en-us/azure/aks/cluster-autoscaler |
| AKS Network Policy | https://learn.microsoft.com/en-us/azure/aks/use-network-policies |
| AKS Storage (managed-csi) | https://learn.microsoft.com/en-us/azure/aks/azure-csi-disk-storage-provision |
| NGINX Ingress on AKS | https://learn.microsoft.com/en-us/azure/aks/ingress-basic |

---

## MongoDB (External / Managed)

| Topic | Reference |
|-------|-----------|
| MongoDB Atlas | https://www.mongodb.com/atlas |
| MongoDB Atlas on Azure | https://www.mongodb.com/atlas/database/microsoft-azure |
| MongoDB Community Operator (in-cluster HA) | https://github.com/mongodb/mongodb-kubernetes-operator |
| MongoDB connection string format | https://www.mongodb.com/docs/manual/reference/connection-string |
| MongoDB index best practices | https://www.mongodb.com/docs/manual/core/indexes |

---

## Security References

| Topic | Reference |
|-------|-----------|
| OWASP Top 10 | https://owasp.org/www-project-top-ten |
| JWT (RFC 7519) | https://www.rfc-editor.org/rfc/rfc7519 |
| BCrypt algorithm | https://en.wikipedia.org/wiki/Bcrypt |
| Kubernetes Pod Security Standards | https://kubernetes.io/docs/concepts/security/pod-security-standards |
| NSA Kubernetes Hardening Guide | https://media.defense.gov/2022/Aug/29/2003066362/-1/-1/0/CTR_KUBERNETES_HARDENING_GUIDANCE_1.2_20220829.PDF |

---

## Testing

| Technology | Official Docs |
|-----------|--------------|
| xUnit | https://xunit.net |
| FluentAssertions | https://fluentassertions.com/introduction |
| NSubstitute | https://nsubstitute.github.io |
| Testcontainers for .NET | https://dotnet.testcontainers.org |
| Testcontainers MongoDB module | https://dotnet.testcontainers.org/modules/mongodb |
| Microsoft.AspNetCore.Mvc.Testing | https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests |

---

## GraphQL Specification & Learning

| Topic | Reference |
|-------|-----------|
| GraphQL specification | https://spec.graphql.org |
| GraphQL official learning | https://graphql.org/learn |
| GraphQL vs REST (comparison) | https://graphql.org/faq/graphql-vs-rest |
| graphql-ws protocol | https://github.com/enisdenjo/graphql-ws/blob/master/PROTOCOL.md |
| Banana Cake Pop (GraphQL IDE) | https://chillicream.com/products/bananacakepop |

---

## Further Learning

| Topic | Reference |
|-------|-----------|
| Kubernetes the Hard Way | https://github.com/kelseyhightower/kubernetes-the-hard-way |
| CKAD (Certified Kubernetes App Developer) | https://www.cncf.io/certification/ckad |
| CKA (Certified Kubernetes Administrator) | https://www.cncf.io/certification/cka |
| CNCF Cloud Native landscape | https://landscape.cncf.io |
| 12-Factor App methodology | https://12factor.net |
| Google SRE Book (free online) | https://sre.google/sre-book/table-of-contents |
