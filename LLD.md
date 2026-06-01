# TaskFlow — Low Level Design (LLD)

## 1. Domain Model

### 1.1 Entity Hierarchy
```
Workspace (1)
  └─ Project (*) [workspaceId FK]
       └─ TaskItem (*) [projectId FK]
            └─ Comment (*) [taskId FK]
User (independent — referenced by ownerId, assigneeId, authorId)
```

### 1.2 Entity Definitions

**Workspace**
```csharp
string Id           // MongoDB ObjectId (BsonType.ObjectId)
string Name
string OwnerId      // User.Id reference
DateTime CreatedAt  // init-only
```

**Project**
```csharp
string Id
string WorkspaceId
string Name
string Description
ProjectStatus Status  // Active | Archived  [BsonRepresentation(BsonType.String)]
string OwnerId
DateTime CreatedAt    // init-only
```

**TaskItem**
```csharp
string Id
string ProjectId
string Title
string Description
TaskItemStatus Status   // TODO | IN_PROGRESS | IN_REVIEW | DONE
TaskPriority Priority   // CRITICAL | HIGH | MEDIUM | LOW
string? AssigneeId      // nullable — unassigned tasks allowed
DateTime? DueDate       // nullable
List<string> Tags       // defaults to [] — never null
DateTime CreatedAt      // init-only
DateTime UpdatedAt      // set server-side on every mutation
```

**Comment**
```csharp
string Id
string TaskId
string Body
string AuthorId
DateTime CreatedAt  // init-only
```

**User**
```csharp
string Id
string Name
string Email
string PasswordHash  // BCrypt hash — [GraphQLIgnore]
UserRole Role        // Admin | Member
DateTime CreatedAt   // init-only
```

### 1.3 Enum Conventions
- All enums stored as strings in MongoDB: `[BsonRepresentation(BsonType.String)]`
- GraphQL schema exposes enums in SCREAMING_SNAKE_CASE (HotChocolate convention): `InProgress` → `IN_PROGRESS`
- REST API serialises enums as strings via `JsonStringEnumConverter` registered globally

---

## 2. Backend Architecture

### 2.1 Project Structure
```
src/TaskFlow.Api/
├── Domain/
│   ├── Enums.cs                    ProjectStatus, TaskItemStatus, TaskPriority, UserRole
│   ├── Workspace.cs
│   ├── Project.cs
│   ├── TaskItem.cs
│   ├── Comment.cs
│   └── User.cs
├── Features/
│   ├── Auth/
│   │   └── AuthEndpoints.cs        POST /auth/register, POST /auth/token
│   ├── Projects/
│   │   └── ProjectEndpoints.cs     CRUD REST endpoints at /api/projects
│   └── Tasks/
│       └── TaskEndpoints.cs        CRUD REST endpoints at /api/tasks
├── GraphQL/
│   ├── Query.cs                    workspaces, projects, tasks, workspace(id), project(id), task(id)
│   ├── Mutation.cs                 createWorkspace, createProject, updateProject, deleteProject,
│   │                               createTask, updateTask, deleteTask, addComment
│   └── Subscription.cs             taskUpdated(projectId), commentAdded(taskId)
├── Infrastructure/
│   ├── MongoDbSettings.cs
│   ├── JwtSettings.cs
│   ├── AppMetrics.cs               Prometheus counters, gauges, histograms
│   ├── CorrelationIdMiddleware.cs  X-Correlation-Id header + LogContext
│   ├── IRepository.cs              Generic CRUD interface
│   ├── MongoRepository.cs          Abstract base — GetByIdAsync, GetAllAsync, InsertAsync, etc.
│   └── Repositories/
│       ├── WorkspaceRepository.cs
│       ├── ProjectRepository.cs    + GetByWorkspaceIdAsync()
│       ├── TaskRepository.cs       + GetByProjectIdAsync()
│       ├── CommentRepository.cs    + GetByTaskIdAsync()
│       └── UserRepository.cs       + GetByEmailAsync()
├── Health/                         (probes registered in Program.cs)
├── appsettings.json
└── Program.cs                      DI composition root + middleware pipeline
```

### 2.2 Dependency Injection Registration (Program.cs)
```csharp
// Config
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// MongoDB — one client, one database (singleton — connection pool)
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(settings.ConnectionString));
builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(settings.DatabaseName));

// Repositories (scoped per request)
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

// GraphQL
builder.Services.AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddSubscriptionType<Subscription>()
    .AddInMemorySubscriptions()
    .AddFiltering()
    .AddSorting();

// Auth
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* symmetric key validation */ });

// Observability
builder.Host.UseSerilog(...);
builder.Services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddAspNetCoreInstrumentation()
        .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")
        .AddOtlpExporter(o => o.Endpoint = new Uri(otelEndpoint)));

// Health checks
builder.Services.AddHealthChecks()
    .AddMongoDb(tags: ["ready", "startup"]);
```

### 2.3 Middleware Pipeline Order
```csharp
app.UseMiddleware<CorrelationIdMiddleware>()  // 1. Attach correlation ID to LogContext
app.UseSerilogRequestLogging()               // 2. One structured log line per request
app.UseHttpMetrics()                         // 3. Prometheus HTTP metrics
app.UseCors()                                // 4. CORS headers (before auth — OPTIONS must pass)
app.UseAuthentication()                      // 5. Parse + validate JWT → sets HttpContext.User
app.UseAuthorization()                       // 6. Check [Authorize] attributes
app.UseWebSockets()                          // 7. Upgrade HTTP → WebSocket (for subscriptions)
app.MapGraphQL()                             // 8. /graphql endpoint
app.MapHealthChecks(...)                     // 9. /health/live, /health/ready, /health/startup
app.MapMetrics()                             // 10. /metrics (Prometheus scrape)
app.MapProjectEndpoints()                    // 11. /api/projects
app.MapTaskEndpoints()                       // 12. /api/tasks
app.MapAuthEndpoints()                       // 13. /auth/register, /auth/token
```

### 2.4 Repository Pattern
```csharp
// Generic interface
public interface IRepository<T>
{
    Task<T?> GetByIdAsync(string id);
    Task<IEnumerable<T>> GetAllAsync();
    Task InsertAsync(T entity);
    Task UpdateAsync(string id, T entity);
    Task DeleteAsync(string id);
}

// Domain-specific extension
public interface IProjectRepository : IRepository<Project>
{
    Task<IEnumerable<Project>> GetByWorkspaceIdAsync(string workspaceId);
}

// Concrete implementation (primary constructor — C# 12)
public class ProjectRepository(IMongoDatabase database)
    : MongoRepository<Project>(database, "projects"), IProjectRepository
{
    public async Task<IEnumerable<Project>> GetByWorkspaceIdAsync(string workspaceId) =>
        await Collection
            .Find(p => p.WorkspaceId == workspaceId)
            .ToListAsync();
}
```

### 2.5 GraphQL Schema (Key Types)

**Query type:**
```graphql
type Query {
  workspaces: [Workspace!]!
  workspace(id: ID!): Workspace
  projects(workspaceId: ID!): [Project!]!
  project(id: ID!): Project
  tasks(projectId: ID!): [TaskItem!]!
  task(id: ID!): TaskItem
  me: User
}
```

**Mutation type:**
```graphql
type Mutation {
  createWorkspace(input: CreateWorkspaceInput!): Workspace!
  createProject(input: CreateProjectInput!): Project!
  updateProject(input: UpdateProjectInput!): Project!
  deleteProject(id: ID!): Boolean!
  createTask(input: CreateTaskInput!): TaskItem!
  updateTask(input: UpdateTaskInput!): TaskItem!
  deleteTask(id: ID!): Boolean!
  addComment(input: AddCommentInput!): Comment!
}
```

**Subscription type:**
```graphql
type Subscription {
  taskUpdated(projectId: ID!): TaskItem!
  commentAdded(taskId: ID!): Comment!
}
```

### 2.6 Subscription Event Flow
```csharp
// In Mutation.cs — publisher
await sender.SendAsync($"taskUpdated_{task.ProjectId}", task);

// In Subscription.cs — subscriber
[Subscribe]
[Topic("taskUpdated_{projectId}")]
public TaskItem OnTaskUpdated(string projectId, [EventMessage] TaskItem task) => task;
```
Topic string must match exactly between publisher and subscriber. In-memory subscriptions are scoped per pod — for multi-pod production use Redis subscriptions.

### 2.7 JWT Token
```
Header:  { "alg": "HS256", "typ": "JWT" }
Payload: { "sub": "<userId>", "email": "<email>", "role": "<role>",
           "jti": "<guid>", "iat": <unix>, "exp": <unix+3600> }
Signature: HMAC-SHA256(header.payload, secretKey)
```

---

## 3. Frontend Architecture

### 3.1 Project Structure
```
frontend/src/
├── apollo/
│   ├── client.ts              ApolloClient setup — HttpLink + GraphQLWsLink + authLink + splitLink
│   └── AuthContext.tsx        React context — token storage (memory), login/logout helpers
├── components/
│   └── ProtectedRoute.tsx     Redirects to /login if no token
├── pages/
│   ├── Login/
│   │   └── LoginPage.tsx      POST /auth/token → store JWT → navigate to /
│   ├── Dashboard/
│   │   └── DashboardPage.tsx  useGetWorkspacesQuery, useGetProjectsQuery,
│   │                          useCreateWorkspaceMutation, useCreateProjectMutation,
│   │                          useUpdateProjectMutation, useDeleteProjectMutation
│   └── Board/
│       └── BoardPage.tsx      useGetProjectQuery, useGetTasksQuery,
│                              useUpdateTaskMutation (optimistic), useCreateTaskMutation,
│                              useDeleteTaskMutation, useOnTaskUpdatedSubscription
├── generated/
│   └── graphql.ts             Auto-generated typed hooks and types (graphql-codegen)
├── App.tsx                    React Router routes
├── main.tsx                   App entry — ApolloProvider + RouterProvider
└── index.css                  Design system (CSS custom properties + Tailwind v4)
```

### 3.2 Apollo Client Setup
```typescript
// authLink — injects JWT into every request
const authLink = new ApolloLink((op, forward) => {
  op.setContext({ headers: { Authorization: `Bearer ${getToken()}` } })
  return forward(op)
})

// WebSocket link for subscriptions
const wsLink = new GraphQLWsLink(createClient({
  url: 'ws://app.local:8080/graphql',
  connectionParams: () => ({ Authorization: `Bearer ${getToken()}` }),
}))

// HTTP link for queries/mutations
const httpLink = new HttpLink({ uri: '/graphql' })

// Split: subscriptions go via WS, everything else via HTTP
const splitLink = split(
  ({ query }) => {
    const def = getMainDefinition(query)
    return def.kind === 'OperationDefinition' && def.operation === 'subscription'
  },
  wsLink,
  httpLink
)

export const client = new ApolloClient({
  link: from([authLink, splitLink]),
  cache: new InMemoryCache(),
})
```

### 3.3 GraphQL Code Generator Config (`codegen.yml`)
```yaml
schema: http://localhost:8080/graphql
documents: src/**/*.graphql
generates:
  src/generated/graphql.ts:
    plugins:
      - typescript-operations
      - typescript-react-apollo
    config:
      withHooks: true
      withComponent: false
      withHOC: false
      apolloReactHooksImportFrom: '@apollo/client/react'
```
Running `npm run codegen` reads the live schema from the API and generates fully-typed React hooks. No hand-written query types.

### 3.4 Optimistic Updates
```typescript
// BoardPage.tsx — task drag and drop
updateTask({
  variables: { input: { id, status: newStatus, ... } },
  optimisticResponse: {
    updateTask: { id, status: newStatus, ... }
  } as any,
})
// Apollo writes the optimistic result to InMemoryCache immediately (keyed by TaskItem:id)
// → UI updates before the server responds
// → Server response replaces the optimistic entry
```

### 3.5 Real-Time Subscription (BoardPage)
```typescript
useOnTaskUpdatedSubscription({
  variables: { projectId: id! },
  onData: ({ client, data }) => {
    const task = data.data?.taskUpdated
    if (!task) return
    client.cache.writeFragment({
      id: `TaskItem:${task.id}`,
      fragment: TaskFragmentDoc,
      data: task,
    })
  }
})
// Writes directly to InMemoryCache — no re-fetch needed
// All subscribed clients receive the update via WebSocket
```

### 3.6 CSS Design System (`index.css`)
```css
:root {
  /* Colours */
  --bg: #F8FAFC;       --surface: #FFFFFF;     --surface-2: #F1F5F9;
  --text-1: #0F172A;   --text-2: #475569;      --text-3: #94A3B8;
  --brand: #6366F1;    --brand-dark: #4F46E5;  --brand-light: #EEF2FF;

  /* Spacing / shape */
  --radius-sm: 6px;  --radius: 10px;  --radius-lg: 14px;  --radius-xl: 18px;

  /* Motion */
  --ease: cubic-bezier(0.16, 1, 0.3, 1);  --duration: 160ms;
}

/* Component classes: .card, .btn, .btn-primary, .btn-danger, .btn-ghost */
/* .input, .input-sm, .badge, .nav-glass, .nav-item, .skeleton */
```

---

## 4. Kubernetes Resources

### 4.1 Resource Inventory
```
Namespace: taskflow-dev
├── Deployments
│   ├── taskflow              (API — 2 replicas, RollingUpdate)
│   └── taskflow-frontend     (nginx — 1 replica)
├── StatefulSet
│   └── mongodb               (1 pod, 1Gi PVC)
├── Services
│   ├── taskflow              ClusterIP, port 8080
│   ├── taskflow-frontend     ClusterIP, port 80
│   └── mongodb               Headless (clusterIP: None), port 27017
├── Ingress
│   └── taskflow              traefik, hosts: taskflow.local + app.local
├── ConfigMap
│   └── taskflow              ASPNETCORE_*, MongoDb__DatabaseName, Jwt__*, Otel__*
├── Secret
│   └── taskflow-secret       MongoDb__ConnectionString, Jwt__Key
├── ServiceAccount
│   └── taskflow-api
├── PodDisruptionBudget
│   └── taskflow              minAvailable: 1 (prod: 2)
├── HorizontalPodAutoscaler   (disabled in dev; enabled in prod — CPU 70%)
├── NetworkPolicies
│   ├── mongodb-access-policy  only taskflow-api pods can reach MongoDB:27017
│   └── api-egress-policy      API pods can only call MongoDB + DNS
└── ResourceQuota + LimitRange (namespace-quotas.yaml)
```

### 4.2 API Deployment (key spec)
```yaml
spec:
  replicas: 2
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0          # zero-downtime deploy
  template:
    spec:
      serviceAccountName: taskflow-api
      automountServiceAccountToken: false
      securityContext:
        runAsNonRoot: true
        runAsUser: 1654
        fsGroup: 1654
      containers:
        - name: taskflow
          image: taskflow-registry:5000/taskflow:v2
          ports: [{ containerPort: 8080 }]
          envFrom:
            - configMapRef: { name: taskflow }
            - secretRef:    { name: taskflow-secret }
          resources:
            requests: { cpu: 100m, memory: 128Mi }
            limits:   { cpu: 500m, memory: 256Mi }
          securityContext:
            allowPrivilegeEscalation: false
            readOnlyRootFilesystem: true
            capabilities: { drop: [ALL] }
          livenessProbe:
            httpGet: { path: /health/live, port: 8080 }
            initialDelaySeconds: 10
            periodSeconds: 15
          readinessProbe:
            httpGet: { path: /health/ready, port: 8080 }
            initialDelaySeconds: 10
            periodSeconds: 10
          startupProbe:
            httpGet: { path: /health/startup, port: 8080 }
            failureThreshold: 30
            periodSeconds: 10
```

### 4.3 MongoDB StatefulSet (key spec)
```yaml
spec:
  serviceName: mongodb           # headless service for stable DNS
  replicas: 1
  template:
    spec:
      containers:
        - name: mongodb
          image: mongo:7
          env:
            - name: MONGO_INITDB_ROOT_USERNAME  value: admin
            - name: MONGO_INITDB_ROOT_PASSWORD  valueFrom: secretKeyRef
          volumeMounts:
            - name: mongodb-data  mountPath: /data/db
  volumeClaimTemplates:
    - metadata: { name: mongodb-data }
      spec:
        accessModes: [ReadWriteOnce]
        resources: { requests: { storage: 1Gi } }
```

### 4.4 Helm Chart Structure
```
helm/taskflow/
├── Chart.yaml              name: taskflow, version: 0.1.0, appVersion: 1.0.0
├── values.yaml             all configurable defaults
├── values.dev.yaml         dev overrides (low resources, no HPA, logLevel Debug)
├── values.prod.yaml        prod overrides (more replicas, HPA enabled, PDB minAvailable 2)
└── templates/
    ├── _helpers.tpl         fullname, labels, selectorLabels helpers
    ├── configmap.yaml
    ├── secret.yaml          base64-encodes plain-text values from values.yaml
    ├── deployment.yaml
    ├── service.yaml
    ├── frontend-deployment.yaml
    ├── frontend-service.yaml
    ├── ingress.yaml         conditional: taskflow.local always; app.local when frontend.enabled
    ├── hpa.yaml             {{ if .Values.autoscaling.enabled }}
    ├── pdb.yaml             {{ if .Values.pdb.enabled }}
    ├── serviceaccount.yaml  {{ if .Values.serviceAccount.create }}
    └── NOTES.txt
```

### 4.5 Helm Values Key Hierarchy
```yaml
replicaCount: 2
image:
  repository: taskflow-registry:5000/taskflow
  tag: "v2"
  pullPolicy: IfNotPresent
service:
  type: ClusterIP
  port: 8080
ingress:
  enabled: true
  className: traefik
  host: taskflow.local
  appHost: app.local
frontend:
  enabled: true
  replicaCount: 1
  image:
    repository: taskflow-registry:5000/taskflow-frontend
    tag: "v1"
  service:
    port: 80
config:
  logLevel: Information
  environment: Production
  jwtIssuer: taskflow-api
  jwtAudience: taskflow-clients
  jwtExpiryMinutes: "60"
  jwtKey: "<secret>"
otel:
  endpoint: "http://jaeger.monitoring.svc.cluster.local:4317"
mongodb:
  connectionString: "mongodb://admin:<pass>@mongodb:27017/taskflow?authSource=admin"
  databaseName: taskflow
autoscaling:
  enabled: false
  minReplicas: 2
  maxReplicas: 5
  targetCPUUtilizationPercentage: 70
pdb:
  enabled: true
  minAvailable: 1
podSecurityContext:
  runAsNonRoot: true
  runAsUser: 1654
containerSecurityContext:
  allowPrivilegeEscalation: false
  readOnlyRootFilesystem: true
  capabilities: { drop: [ALL] }
```

---

## 5. Observability Details

### 5.1 Custom Prometheus Metrics (`AppMetrics.cs`)
```csharp
// Counter — total tasks created since pod start
public static readonly Counter TasksCreated =
    Metrics.CreateCounter("taskflow_tasks_created_total", "Total number of tasks created");

// Gauge — current project count (incremented on create, decremented on delete)
public static readonly Gauge ActiveProjects =
    Metrics.CreateGauge("taskflow_active_projects", "Number of active projects");

// Histogram — GraphQL mutation execution time
public static readonly Histogram GraphQlRequestDuration =
    Metrics.CreateHistogram("taskflow_graphql_request_duration_seconds",
        "GraphQL request execution duration",
        new HistogramConfiguration { Buckets = Histogram.LinearBuckets(0.01, 0.05, 10) });
```

### 5.2 Structured Log Fields (per request)
```json
{
  "@t": "2026-05-29T...",
  "@mt": "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms",
  "RequestMethod": "POST",
  "RequestPath": "/graphql",
  "StatusCode": 200,
  "Elapsed": 12.4,
  "MachineName": "taskflow-f4f87c4f5-9xlhq",
  "ThreadId": 8,
  "AppVersion": "1.0.0",
  "CorrelationId": "a1b2c3d4-..."
}
```

### 5.3 OpenTelemetry Trace Spans (per request)
```
POST /graphql   [120ms]
  └─ MongoDB.find  [15ms]   — collection: projects, filter: {workspaceId: "..."}
  └─ MongoDB.find  [8ms]    — collection: tasks, filter: {projectId: "..."}
```

### 5.4 ServiceMonitor (Prometheus Operator CRD)
```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: taskflow-api
  namespace: taskflow-dev
spec:
  selector:
    matchLabels:
      app.kubernetes.io/name: taskflow
  endpoints:
    - port: http
      path: /metrics
      interval: 15s
```

---

## 6. Docker Build Details

### 6.1 API Dockerfile (3-stage)
```
Stage 1 (build)   — sdk:10.0-alpine
  COPY .slnx + .csproj → dotnet restore (cached layer)
  COPY src/ → dotnet publish -c Release -o /out

Stage 2 omitted (publish done in stage 1)

Stage 3 (runtime) — aspnet:10.0-alpine
  COPY --from=build --chown=app:app /out .
  USER app
  EXPOSE 8080
  ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]
```

### 6.2 Frontend Dockerfile (2-stage)
```
Stage 1 (build)   — node:20-alpine
  COPY package.json + package-lock.json → npm ci --ignore-scripts (cached layer)
  COPY . → npm run build (tsc -b && vite build)

Stage 2 (serve)   — nginx:alpine
  COPY --from=build /app/dist /usr/share/nginx/html
  COPY nginx.conf /etc/nginx/conf.d/default.conf
  EXPOSE 80
```

### 6.3 nginx.conf (SPA routing)
```nginx
server {
  listen 80;
  root /usr/share/nginx/html;
  index index.html;

  location /assets/ {
    expires 1y;
    add_header Cache-Control "public, immutable";
    access_log off;
  }

  location / {
    try_files $uri $uri/ /index.html;  # SPA fallback — all unknown routes return index.html
    add_header Cache-Control "no-cache";
  }
}
```

---

## 7. Configuration Binding

ASP.NET Core maps environment variables to nested config sections using `__` (double underscore):

| Environment variable | maps to appsettings.json |
|---------------------|--------------------------|
| `MongoDb__ConnectionString` | `MongoDb.ConnectionString` |
| `MongoDb__DatabaseName` | `MongoDb.DatabaseName` |
| `Jwt__Key` | `Jwt.Key` |
| `Jwt__Issuer` | `Jwt.Issuer` |
| `Otel__Endpoint` | `Otel.Endpoint` |

This is the bridge between Kubernetes ConfigMaps/Secrets and the ASP.NET Core configuration system. The same POCO classes (`MongoDbSettings`, `JwtSettings`) work identically in Docker Compose (env vars in compose file) and Kubernetes (envFrom: configMapRef + secretRef).
