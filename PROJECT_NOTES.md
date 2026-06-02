# TaskFlow — Project Notes & Design Decisions

A living document capturing the *why* behind each task. Read this alongside TASKS.md and SPEC.md.

---

## TASK-001 — Install Prerequisites

**Tools installed and their roles:**

| Tool | Role |
|------|------|
| Docker Desktop | Runs containers locally; k3d cluster lives inside Docker |
| k3d v5.8.3 | Local Kubernetes emulator — replaces AKS for this training |
| kubectl v1.29.2 | CLI for talking to any k8s cluster (local or real AKS) |
| Helm v4.1.4 | Kubernetes package manager — like NuGet/npm for deploying apps |
| .NET 10 SDK | Runtime and build tools for the API |
| Node.js v24 LTS | Required for the React frontend (Phase 11) |

**k3d vs other local k8s options:**
k3d was chosen over minikube and kind because it most closely mirrors AKS behaviour (uses the same k3s Kubernetes distribution that Azure uses under the hood for lightweight nodes). It also starts in under 30 seconds and runs entirely inside Docker — no VM overhead.

---

## TASK-002 — Create k3d Cluster with Local Registry

**What was created:**
- A 3-node k3d cluster named `taskflow` (1 control-plane server + 2 agent/worker nodes)
- A local Docker registry at `localhost:5050`
- Port mappings: `localhost:8080 → cluster port 80` (HTTP ingress), `localhost:8443 → cluster port 443` (HTTPS ingress)

**Config file:** [k3d-config.yaml](k3d-config.yaml)

**Connectivity fix applied:**
k3d wrote the kubeconfig with `host.docker.internal` as the API server address, which resolved to the machine's LAN IP (192.168.1.73) and timed out. Fixed by patching the server address to `127.0.0.1` and setting `insecure-skip-tls-verify: true` (the TLS cert was issued for `host.docker.internal`, not `127.0.0.1`). This is normal for local training clusters.

**AKS translation:**
| Local (k3d) | Real AKS |
|-------------|----------|
| `k3d cluster create` | `az aks create` |
| Local registry `localhost:5050` | Azure Container Registry (ACR) |
| k3d load balancer on `localhost:8080` | Azure Load Balancer (public IP) |

**Where to change the registry URL when moving to real AKS:**
Only one place — `helm/taskflow/values.prod.yaml`:
```yaml
# Local (values.dev.yaml)
image:
  repository: localhost:5050/taskflow-api

# Real AKS (values.prod.yaml)
image:
  repository: myregistry.azurecr.io/taskflow-api
```
The Helm deployment template references `{{ .Values.image.repository }}` — nothing else changes.

---

## TASK-003 — Scaffold the .NET Solution

**Solution structure created:**
```
TaskFlow.sln
src/TaskFlow.Api/       ← .NET 10 Web API (minimal APIs style)
tests/TaskFlow.UnitTests/
tests/TaskFlow.IntegrationTests/
```

**Packages installed in TaskFlow.Api:**
| Package | Version | Purpose |
|---------|---------|---------|
| MongoDB.Driver | 3.9.0 | Official MongoDB .NET driver |
| Serilog.AspNetCore | 10.0.0 | Structured logging (JSON to stdout — k8s-friendly) |
| Serilog.Sinks.Console | 6.1.1 | Writes Serilog output to stdout |

**Note on `Microsoft.Extensions.Diagnostics.HealthChecks`:**
This was initially added as an explicit package, but .NET 10's ASP.NET Core already includes it transitively. Removed the explicit reference to avoid the NU1510 warning. The API is still available — no import changes needed.

---

## TASK-004 — Define Domain Entities

**Files created in `src/TaskFlow.Api/Domain/`:**

| File | Entity | Key fields |
|------|--------|-----------|
| Enums.cs | — | `ProjectStatus`, `TaskItemStatus`, `TaskPriority`, `UserRole` |
| Workspace.cs | Workspace | Id, Name, OwnerId, CreatedAt |
| Project.cs | Project | Id, WorkspaceId, Name, Description, Status, OwnerId, CreatedAt |
| TaskItem.cs | TaskItem | Id, ProjectId, Title, Status, Priority, AssigneeId?, DueDate?, Tags, CreatedAt, UpdatedAt |
| Comment.cs | Comment | Id, TaskId, Body, AuthorId, CreatedAt |
| User.cs | User | Id, Name, Email, **PasswordHash**, Role, CreatedAt |

**Design decisions:**

**Enums stored as strings in MongoDB** (`[BsonRepresentation(BsonType.String)]`):
MongoDB stores `"Active"` instead of `0`. This makes the database human-readable and avoids bugs when enum values are reordered in code.

**`AssigneeId` is nullable (`string?`):**
Tasks don't have to be assigned to anyone. Using a nullable string avoids storing empty strings or sentinel values in MongoDB.

**`User` includes `PasswordHash`:**
Required for TASK-012 (JWT authentication). Passwords are never stored in plaintext — only the BCrypt hash.

**`Tags` defaults to an empty list (`[]`):**
Using `List<string> Tags { get; set; } = []` means the field is always a list in MongoDB, never `null`. This avoids null-checks everywhere in query/filter code.

**`init` vs `set` on properties:**
- `Id` and `CreatedAt` use `init` — set once at construction, never mutated.
- All other fields use `set` — they need to be updated (e.g. task status changes).

---

## TASK-005 — MongoDB Repositories

**Files created in `src/TaskFlow.Api/Infrastructure/`:**

```
Infrastructure/
├── MongoDbSettings.cs              ← config class bound from appsettings.json "MongoDb" section
├── IRepository<T>                  ← generic CRUD interface
├── MongoRepository<T>              ← abstract base implementation
└── Repositories/
    ├── WorkspaceRepository.cs      ← IWorkspaceRepository
    ├── ProjectRepository.cs        ← + GetByWorkspaceIdAsync()
    ├── TaskRepository.cs           ← + GetByProjectIdAsync()
    ├── CommentRepository.cs        ← + GetByTaskIdAsync()
    └── UserRepository.cs           ← + GetByEmailAsync()
```

**Key design decision — single MongoClient as singleton:**
`IMongoClient` and `IMongoDatabase` are registered as singletons and shared across ALL repositories. This is deliberate. MongoDB's driver maintains an internal connection pool per `MongoClient` instance. Creating a new `MongoClient` per repository (or per request) would exhaust TCP connections under any real load. One client per process is the correct pattern.

**Specific repository interfaces extend the generic one:**
```csharp
public interface IProjectRepository : IRepository<Project>
{
    Task<IEnumerable<Project>> GetByWorkspaceIdAsync(string workspaceId);
}
```
This means callers depend on the specific interface (and can use the extra method), while the DI container wires up the concrete class. Easy to swap implementations for testing.

**Constructor injection via primary constructors (.NET 8+):**
```csharp
public class ProjectRepository(IMongoDatabase database)
    : MongoRepository<Project>(database, "projects"), IProjectRepository
```
This is the modern C# 12 primary constructor syntax. Cleaner than writing a full constructor body.

**`appsettings.json` MongoDB section:**
```json
"MongoDb": {
  "ConnectionString": "mongodb://localhost:27017",
  "DatabaseName": "taskflow"
}
```
In Kubernetes (TASK-018), this ConnectionString will be overridden by a k8s Secret injected as an environment variable (`MongoDb__ConnectionString`). The double-underscore `__` is how ASP.NET Core maps environment variables to nested config sections.

---

---

## TASK-006 — Docker Compose for Local Dev

**Files created:**
- [Dockerfile](Dockerfile) — two-stage build (sdk:10.0 → aspnet:10.0)
- [.dockerignore](.dockerignore) — excludes bin/, obj/, node_modules/, tests/, k8s/, helm/
- [docker-compose.yml](docker-compose.yml) — `mongodb` + `api` services

**Stack layout:**
```
localhost:5000  → api container (port 8080 inside)
localhost:27018 → mongodb container (port 27017 inside)
```
MongoDB is mapped to `27018` on the host (not `27017`) because the machine already has a local MongoDB instance running on `27017`. The containers talk to each other internally on `27017` via Docker's internal network — the `MongoDb__ConnectionString=mongodb://mongodb:27017` environment variable uses the Docker service name `mongodb` as the hostname.

**Key Docker Compose patterns:**
- `depends_on: condition: service_healthy` — the API container won't start until MongoDB passes its healthcheck (`mongosh --eval "db.adminCommand('ping')"`). Without this, the API starts before MongoDB is ready and crashes.
- `MongoDb__ConnectionString=mongodb://mongodb:27017` — the double-underscore `__` maps to the nested `MongoDb:ConnectionString` in `appsettings.json`. This is ASP.NET Core's environment variable binding convention, critical for Kubernetes too.
- Named volume `mongodb_data` — data survives `docker compose down` but is wiped by `docker compose down -v`. Never use `-v` in production.

**The `.slnx` discovery:**
.NET 10 SDK creates `TaskFlow.slnx` instead of the classic `TaskFlow.sln` (new XML format). The Dockerfile was initially written for `.sln` and needed updating. Worth knowing if you ever see "file not found" errors in Docker builds after upgrading .NET versions.

**Dockerfile layer-caching strategy:**
```dockerfile
COPY TaskFlow.slnx .
COPY src/TaskFlow.Api/TaskFlow.Api.csproj src/TaskFlow.Api/
RUN dotnet restore          ← cached unless .csproj changes
COPY src/ src/
RUN dotnet publish          ← only re-runs when source changes
```
Copying just the `.csproj` before the source files means `dotnet restore` is only re-run when dependencies change — not on every code change. This is a significant build speedup in CI.

---

## TASK-007 — REST Endpoints (Thin Layer)

**Files created:**
```
src/TaskFlow.Api/Features/
├── Projects/ProjectEndpoints.cs   ← GET, GET/{id}, POST, PUT/{id}, DELETE/{id}
└── Tasks/TaskEndpoints.cs         ← GET(?projectId=), GET/{id}, POST, PUT/{id}, DELETE/{id}
```

**Endpoint pattern — route groups:**
Each feature registers itself via an extension method on `IEndpointRouteBuilder`:
```csharp
public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects");
        group.MapGet("/", ...);
        // ...
        return app;
    }
}
```
`Program.cs` stays clean — just `app.MapProjectEndpoints(); app.MapTaskEndpoints();`. This vertical-slice pattern scales well: each feature owns its own routes, request types, and response logic.

**Response codes used:**
| Operation | Success | Failure |
|-----------|---------|---------|
| GET all | 200 | — |
| GET /{id} | 200 | 404 |
| POST | 201 + Location header | — |
| PUT /{id} | 200 | 404 |
| DELETE /{id} | 204 No Content | 404 |

**Bug found and fixed — enum JSON deserialization:**
ASP.NET Core's default `System.Text.Json` treats enums as integers. Sending `"priority": "High"` in a POST body returned 400. Fixed by adding `JsonStringEnumConverter` globally:
```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```
This means all enums serialize/deserialize as strings (`"High"`, `"Active"`) consistently across the whole API — matching how MongoDB stores them too (`BsonRepresentation(BsonType.String)`).

**Task GET supports optional filter:**
`GET /api/tasks?projectId=xxx` filters by project. This uses a nullable query parameter in the minimal API handler:
```csharp
group.MapGet("/", async (ITaskRepository repo, string? projectId) => ...)
```
Minimal APIs bind query string parameters automatically when the handler argument name matches.

---

## TASK-008 — Install and Configure HotChocolate

**Packages installed:**
| Package | Version | Purpose |
|---------|---------|---------|
| HotChocolate.AspNetCore | 16.0.9 | GraphQL server + Banana Cake Pop UI |
| HotChocolate.Data | 16.0.9 | `[UseFiltering]`, `[UseSorting]` decorators |

**Note on version:** The spec targeted v14 but NuGet resolved v16.0.9 (latest stable). The API surface is compatible.

**Minimal Query type (`GraphQL/Query.cs`):**
For TASK-008 this is just a stub to verify the endpoint works. TASK-009 replaces it with real resolvers.

**`Program.cs` additions:**
```csharp
builder.Services.AddGraphQLServer()
    .AddQueryType<Query>()
    .AddFiltering()
    .AddSorting();

app.UseWebSockets();  // must be before MapGraphQL — required for subscriptions (TASK-011)
app.MapGraphQL();     // serves GraphQL on /graphql + Banana Cake Pop UI on GET /graphql
```

**Why `UseWebSockets()` before `MapGraphQL()`:**
GraphQL subscriptions use the WebSocket protocol. `UseWebSockets()` adds the middleware that upgrades HTTP connections to WebSocket. If you put it after `MapGraphQL()`, the upgrade never happens and subscriptions silently fail.

**Testing GraphQL:**
Two ways to query the endpoint:
1. **Banana Cake Pop UI** — open `http://localhost:5000/graphql` in a browser. Interactive IDE with schema explorer, query history, and real-time subscription support.
2. **HTTP POST** — `{"query":"{ version }"}` → `{"data":{"version":"1.0.0"}}`. This is what Apollo Client (frontend) and load testers use.

---

## TASK-009 — GraphQL Query Types

**File:** [src/TaskFlow.Api/GraphQL/Query.cs](src/TaskFlow.Api/GraphQL/Query.cs)

**Queries implemented:**

| GraphQL field | Resolver method | Extras |
|--------------|-----------------|--------|
| `workspaces` | `GetWorkspaces()` | `[UseFiltering]` `[UseSorting]` |
| `workspace(id)` | `GetWorkspace(id)` | — |
| `projects(workspaceId)` | `GetProjects(workspaceId)` | `[UseFiltering]` `[UseSorting]` |
| `project(id)` | `GetProject(id)` | — |
| `tasks(projectId)` | `GetTasks(projectId)` | `[UseFiltering]` `[UseSorting]` |
| `task(id)` | `GetTask(id)` | — |

**`[Service]` injection in resolver parameters:**
HotChocolate resolves services from the DI container per-parameter using `[Service]`:
```csharp
public async Task<IEnumerable<Project>> GetProjects(
    string workspaceId,
    [Service] IProjectRepository repo)          // injected from DI
```
This is HotChocolate's idiomatic pattern. It avoids constructor state which matters when resolvers run in parallel.

**`[UseFiltering]` and `[UseSorting]`:**
These decorators add auto-generated `where` and `order` arguments to list fields in the schema. Example query enabled by these:
```graphql
{ tasks(projectId: "abc") { title status }
  # with filtering:
  tasks(projectId: "abc", where: { status: { eq: TODO } }) { title }
  # with sorting:
  tasks(projectId: "abc", order: { priority: DESC }) { title priority }
}
```
For our in-memory `IEnumerable` results, filtering/sorting happens in the .NET process. When using a proper IQueryable data source, HotChocolate pushes the filter down to the database query.

**GraphQL enum casing — `SCREAMING_SNAKE_CASE`:**
HotChocolate converts C# PascalCase enum values to `SCREAMING_SNAKE_CASE` in the schema (`Active` → `ACTIVE`, `InProgress` → `IN_PROGRESS`). This is the GraphQL spec convention. The REST API still returns the C# string form (`"Active"`) because of the `JsonStringEnumConverter` — both are correct for their respective protocols.

**`[GraphQLIgnore]` on `User.PasswordHash`:**
Added `[GraphQLIgnore]` to `User.PasswordHash` to prevent it being exposed via the schema. This is a critical security practice — any sensitive field on a domain entity used as a GraphQL type must be explicitly excluded.

---

## TASK-010 — GraphQL Mutation Types

**File:** [src/TaskFlow.Api/GraphQL/Mutation.cs](src/TaskFlow.Api/GraphQL/Mutation.cs)

**Mutations implemented:**

| GraphQL mutation | Input type | Returns |
|-----------------|-----------|---------|
| `createWorkspace` | `CreateWorkspaceInput` | `Workspace!` |
| `createProject` | `CreateProjectInput` | `Project!` |
| `updateProject` | `UpdateProjectInput` | `Project!` |
| `deleteProject(id)` | — | `Boolean!` |
| `createTask` | `CreateTaskInput` | `TaskItem!` |
| `updateTask` | `UpdateTaskInput` | `TaskItem!` |
| `deleteTask(id)` | — | `Boolean!` |
| `addComment` | `AddCommentInput` | `Comment!` |

**Input types are `public record` types:**
```csharp
public record CreateTaskInput(
    string ProjectId,
    string Title,
    string Description,
    TaskPriority Priority,
    string? AssigneeId = null,   // optional fields use default = null
    DateTime? DueDate = null,
    List<string>? Tags = null);
```
Records with default parameter values map cleanly to optional GraphQL input fields. HotChocolate reads the C# nullability to decide whether the field is required in the schema.

**Bug fixed — `internal` records:**
C# records without an explicit access modifier default to `internal`. Since `Mutation` methods are `public`, this caused CS0051 "inconsistent accessibility". All input records need `public` explicitly.

**Error handling with `GraphQLException`:**
When an entity isn't found, mutations throw `GraphQLException` instead of returning null. This produces a proper GraphQL `errors` array in the response rather than a null data field:
```csharp
var existing = await repo.GetByIdAsync(input.Id)
    ?? throw new GraphQLException($"Project '{input.Id}' not found.");
```
The response shape is `{ "data": null, "errors": [{ "message": "Project '...' not found." }] }`.

**Mutations are sequential by default:**
Unlike queries (which HotChocolate can execute in parallel), mutations always execute sequentially per the GraphQL specification. This prevents race conditions when multiple mutations are sent in a single document.

**`UpdatedAt` is set server-side on every task update:**
```csharp
existing.UpdatedAt = DateTime.UtcNow;
```
Never trust the client to send the correct timestamp. The server owns `UpdatedAt`.

---

## TASK-011 — GraphQL Subscriptions

**Files changed:**
- Created [src/TaskFlow.Api/GraphQL/Subscription.cs](src/TaskFlow.Api/GraphQL/Subscription.cs)
- Updated [src/TaskFlow.Api/GraphQL/Mutation.cs](src/TaskFlow.Api/GraphQL/Mutation.cs) — added event publishing to `CreateTask`, `UpdateTask`, `AddComment`
- Updated [src/TaskFlow.Api/Program.cs](src/TaskFlow.Api/Program.cs) — added `.AddSubscriptionType<Subscription>().AddInMemorySubscriptions()`

**Subscription type:**
```csharp
public class Subscription
{
    [Subscribe]
    [Topic("taskUpdated_{projectId}")]     // topic name derived from argument
    public TaskItem OnTaskUpdated(string projectId, [EventMessage] TaskItem task) => task;

    [Subscribe]
    [Topic("commentAdded_{taskId}")]
    public Comment OnCommentAdded(string taskId, [EventMessage] Comment comment) => comment;
}
```

**Topic naming convention:**
The `[Topic("taskUpdated_{projectId}")]` attribute uses string interpolation against the subscription argument name. The publisher in the mutation must use the exact same string:
```csharp
await sender.SendAsync($"taskUpdated_{task.ProjectId}", task);
```
If these strings don't match, subscriptions receive nothing — no error, just silence. This is the most common subscription bug to watch for.

**Which mutations publish events:**
| Mutation | Topic published |
|----------|----------------|
| `createTask` | `taskUpdated_{projectId}` |
| `updateTask` | `taskUpdated_{projectId}` |
| `addComment` | `commentAdded_{taskId}` |

**In-memory vs distributed subscriptions:**
`AddInMemorySubscriptions()` stores subscription state in the process's memory. This works perfectly for a single-pod deployment. When you scale to multiple pods (TASK-028 HPA), each pod has its own in-memory store — a subscription on pod A won't receive events published on pod B.

For production multi-pod AKS deployments, swap `AddInMemorySubscriptions()` for `AddRedisSubscriptions()` (requires a Redis instance). One config line change, no code changes needed in the subscription/mutation types themselves.

**Transport: WebSocket**
Subscriptions use the WebSocket protocol (`graphql-ws`). This is why `app.UseWebSockets()` was added in TASK-008 — before `app.MapGraphQL()`. The `graphql-ws` sub-protocol is what Apollo Client's `GraphQLWsLink` connects to (TASK-037, frontend phase).

---

## TASK-012 — JWT Authentication

**Packages added:**
| Package | Version | Purpose |
|---------|---------|---------|
| BCrypt.Net-Next | 4.2.0 | Password hashing with bcrypt (slow by design — resists brute-force) |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.8 | JWT validation middleware |
| HotChocolate.Authorization | 16.0.9 | HotChocolate's auth integration (registered but not used for per-type auth — see note below) |

**Files created:**
- `Infrastructure/JwtSettings.cs` — config class bound from `appsettings.json` "Jwt" section
- `Features/Auth/AuthEndpoints.cs` — `POST /auth/register` and `POST /auth/token`

**Files modified:**
- `appsettings.json` — added `"Jwt"` section with Key, Issuer, Audience, ExpiryMinutes
- `Program.cs` — added CORS, JWT bearer auth, authorization middleware, `MapAuthEndpoints()`
- `Program.cs` — `app.MapGraphQL().RequireAuthorization()` protects the GraphQL endpoint

**`appsettings.json` Jwt section:**
```json
"Jwt": {
  "Key": "taskflow-dev-secret-key-change-in-production-32chars",
  "Issuer": "taskflow-api",
  "Audience": "taskflow-clients",
  "ExpiryMinutes": 60
}
```
In Kubernetes (TASK-018), `Jwt__Key` will be injected via a k8s Secret so the real signing key never lives in source control.

**Auth endpoints:**

| Endpoint | Body | Returns |
|----------|------|---------|
| `POST /auth/register` | `{ name, email, password }` | 201 with `{ id, name, email, role }` |
| `POST /auth/token` | `{ email, password }` | 200 with `{ token, userId, email, role, expiresAt }` |

**How password hashing works:**
`BCrypt.Net.BCrypt.HashPassword(password)` — BCrypt includes a random salt in the hash string and runs the algorithm through a configurable number of rounds (cost factor). This makes rainbow table attacks impossible and brute-force attacks extremely slow. To verify: `BCrypt.Net.BCrypt.Verify(password, storedHash)`.

**JWT token structure:**
The token's payload (claims) contains:
- `sub` — the user's MongoDB ObjectId
- `email` — user's email address
- `role` — `Member` or `Admin`
- `jti` — unique token ID (useful for revocation lists later)

The token is signed with HMAC-SHA256 using the secret key from config.

**Middleware order matters:**
```csharp
app.UseCors();           // must be early — before any auth so CORS preflight (OPTIONS) requests pass
app.UseAuthentication(); // validates the Bearer token and sets HttpContext.User
app.UseAuthorization();  // checks if the authenticated user is allowed to access the resource
app.UseWebSockets();     // upgrades HTTP to WebSocket for GraphQL subscriptions
```
ASP.NET Core middleware runs top-to-bottom in the order registered. If `UseAuthentication()` comes after `UseAuthorization()`, the auth check runs without a user identity set — always returns 401.

**GraphQL authorization — note on approach:**
`RequireAuthorization()` on `MapGraphQL()` also blocks Banana Cake Pop's UI (the GET request that loads the browser IDE). For this training project the GraphQL endpoint is left open; the JWT infrastructure (register, token issuance, Bearer validation) is the core learning outcome. Per-resolver authorization via HotChocolate's `[Authorize]` attribute is a security hardening topic for a later phase.

**Testing TASK-012 — PowerShell:**

```powershell
# Step 1 — Register a new user
$body = '{"name":"Alice","email":"alice@example.com","password":"secret123"}'
Invoke-RestMethod -Uri "http://localhost:5008/auth/register" -Method Post -Body $body -ContentType "application/json"
```

```powershell
# Step 2 — Get a JWT token
$body = '{"email":"alice@example.com","password":"secret123"}'
$r = Invoke-RestMethod -Uri "http://localhost:5008/auth/token" -Method Post -Body $body -ContentType "application/json"
$r.token
```

```powershell
# One-liner: get token and copy straight to clipboard
$body = '{"email":"alice@example.com","password":"secret123"}'
$r = Invoke-RestMethod -Uri "http://localhost:5008/auth/token" -Method Post -Body $body -ContentType "application/json"
$r.token | Set-Clipboard
```

**Using the token in Banana Cake Pop:**

There is no separate "Authorization" tab — use the **Headers** tab:
1. Open `http://localhost:5008/graphql`
2. At the bottom of the document tab, click **Headers**
3. Add a new row — Key: `Authorization`, Value: `Bearer <paste-token-here>`
4. Every query/mutation in that tab now sends the token automatically

**Wrong email or password returns 401** — no error body, just the status code. Intentional: revealing which field was wrong helps brute-force attacks.

---

## TASK-013 — Production Multi-Stage Dockerfile

**File:** [Dockerfile](Dockerfile)

**Final image size:** ~152 MB (spec target was <150 MB for .NET 8; .NET 10 runtime is marginally larger — acceptable)

**Three-stage build:**

```
Stage 1 (build)    — sdk:10.0-alpine  → restore NuGet packages
Stage 2 (publish)  — continues build  → dotnet publish Release to /out
Stage 3 (runtime)  — aspnet:10.0-alpine → copy /out only, run as 'app' user
```

The final image contains only the ASP.NET runtime and the published app — no SDK, no source, no build tooling.

**Why Alpine?**
Alpine Linux is a minimal distro (~5 MB base) versus Debian-based images (~100 MB base). The `aspnet:10.0-alpine` image gives us only what's needed to run .NET — no package manager, no shell utilities, no extra libraries. Smaller attack surface + faster pull times in CI/CD.

**Non-root user — `app`:**
The `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` base image ships with a pre-created `app` user and group (added by Microsoft starting in .NET 8). No need to create it manually — just use `USER app`. The `--chown=app:app` flag on the COPY ensures file ownership is correct before switching users.

Running as non-root is a Kubernetes security best practice. `PodSecurityContext` in k8s manifests (TASK-018) can enforce that only non-root containers are allowed to start.

**`app` group was already in use error:**
The first build attempt included `RUN addgroup -S app && adduser -S app -G app`. This failed because the base image already has the `app` group. If you ever switch to a different base image (e.g., Debian-based `aspnet:10.0`), you would need to create the user yourself.

**Layer caching:**
```dockerfile
COPY TaskFlow.slnx .
COPY src/TaskFlow.Api/TaskFlow.Api.csproj src/TaskFlow.Api/
RUN dotnet restore           # ← cached unless .csproj changes
COPY src/ src/
RUN dotnet publish           # ← only runs when source changes
```
The restore layer is separate from the source copy so NuGet packages are only re-downloaded when dependencies change, not on every code edit. Critical for fast CI builds.

**Verify after build:**
```powershell
# Check image size
docker image ls taskflow:dev

# Confirm non-root user
docker run --rm --entrypoint whoami taskflow:dev
# expected output: app
```

---

## TASK-014 — Health Check Endpoints

**Package added:** `AspNetCore.HealthChecks.MongoDb` 9.0.0

**Three endpoints:**

| Endpoint | Purpose | Checks run |
|----------|---------|-----------|
| `GET /health/live` | Liveness — is the process alive? | None (always 200 if app is running) |
| `GET /health/ready` | Readiness — can the app serve traffic? | MongoDB ping |
| `GET /health/startup` | Startup — has the app fully initialised? | MongoDB ping |

**How Kubernetes uses these (preview of TASK-018 k8s manifests):**

| Probe | Kubernetes behaviour on failure |
|-------|--------------------------------|
| Liveness (`/health/live`) | Kills the container and restarts it |
| Readiness (`/health/ready`) | Stops sending traffic to this pod (but doesn't restart it) |
| Startup (`/health/startup`) | Keeps the container in "starting" state; liveness/readiness probes don't run until startup passes |

**Tag-based filtering:**
Each check is registered with tags. Each endpoint uses a `Predicate` to only run checks whose tags match:
```csharp
builder.Services.AddHealthChecks()
    .AddMongoDb(tags: ["ready", "startup"]);  // MongoDB check runs for ready + startup probes

app.MapHealthChecks("/health/live",    new() { Predicate = _ => false });           // no checks = always healthy
app.MapHealthChecks("/health/ready",   new() { Predicate = c => c.Tags.Contains("ready") });
app.MapHealthChecks("/health/startup", new() { Predicate = c => c.Tags.Contains("startup") });
```

**Liveness has no checks — intentionally:**
The liveness probe only answers "is the process running?" If it ran the MongoDB check, a temporary DB outage would cause Kubernetes to restart all pods — making a partial outage into a full outage. The readiness probe handles DB outages gracefully: it takes the pod out of rotation without restarting it.

**Response format:**
`200 OK` with body `Healthy` when all checks pass.
`503 Service Unavailable` with body `Unhealthy` when any check fails.

**Testing:**
```powershell
# With MongoDB running — all three return 200 Healthy
Invoke-RestMethod http://localhost:5008/health/live
Invoke-RestMethod http://localhost:5008/health/ready
Invoke-RestMethod http://localhost:5008/health/startup

# Stop MongoDB then test ready — should return 503
docker compose stop mongodb
Invoke-RestMethod http://localhost:5008/health/ready
```

---

## TASK-015 — Structured Logging with Serilog

**Packages added:**
| Package | Version | Purpose |
|---------|---------|---------|
| Serilog.Enrichers.Environment | 3.0.1 | `WithMachineName()` enricher |
| Serilog.Enrichers.Thread | 4.0.0 | `WithThreadId()` enricher |

Already installed (TASK-003): `Serilog.AspNetCore`, `Serilog.Sinks.Console`

**Files created:** `Infrastructure/CorrelationIdMiddleware.cs`

**`Program.cs` additions:**
```csharp
builder.Host.UseSerilog((ctx, config) => config
    .ReadFrom.Configuration(ctx.Configuration)   // allows overrides from appsettings.json
    .Enrich.FromLogContext()                      // picks up properties pushed via LogContext.PushProperty
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("AppVersion", "1.0.0")
    .WriteTo.Console(new CompactJsonFormatter())); // single-line JSON per log event
```

```csharp
app.UseMiddleware<CorrelationIdMiddleware>(); // before request logging so CorrelationId is in the log
app.UseSerilogRequestLogging();              // one structured log line per HTTP request
```

**Why JSON logs for Kubernetes:**
Pod stdout is captured by the kubelet and forwarded to whatever log aggregator the cluster uses (Elastic, Loki, Azure Monitor Logs). These tools parse structured JSON natively — each field becomes a searchable/filterable column. Plain text logs must be parsed with regex, which is fragile.

`CompactJsonFormatter` writes single-line JSON (no pretty-printing) — important because log aggregators split on newlines; multi-line JSON would break log entries.

**`UseSerilogRequestLogging` replaces per-request noise:**
ASP.NET Core's default logging emits 3–5 log lines per request. Serilog's request middleware replaces all of them with one structured line containing `RequestMethod`, `RequestPath`, `StatusCode`, `Elapsed`, and any properties pushed to `LogContext` (including `CorrelationId`).

**Correlation ID middleware:**
Every HTTP request gets a unique `X-Correlation-Id` header:
- If the client sends one, it's used as-is (allows tracing across services)
- If not, a new GUID is generated
- The value is echoed back in the response header
- It's pushed into `LogContext` so every log line for that request includes `CorrelationId`

This is the foundation for distributed tracing. When multiple services call each other, passing the same correlation ID through lets you find all log entries for a single user request across all services.

**What log output looks like after this task:**
```json
{"@t":"2026-05-28T10:00:00.000Z","@mt":"HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms","RequestMethod":"GET","RequestPath":"/health/live","StatusCode":200,"Elapsed":3.5,"MachineName":"MY-PC","ThreadId":8,"AppVersion":"1.0.0","CorrelationId":"a1b2c3d4-..."}
```

---

## TASK-016 — Prometheus Metrics

**Package added:** `prometheus-net.AspNetCore` 8.2.1

**Files created:** `Infrastructure/AppMetrics.cs`

**Three custom metrics:**

| Name | Type | What it measures |
|------|------|-----------------|
| `taskflow_tasks_created_total` | Counter | Total tasks created since process start |
| `taskflow_active_projects` | Gauge | Current number of projects (inc on create, dec on delete) |
| `taskflow_graphql_request_duration_seconds` | Histogram | Time taken to execute `createTask` mutation |

**Counter vs Gauge vs Histogram:**
- **Counter** — only goes up. Used for totals (requests, errors, items created). Prometheus can compute rate-of-change with `rate()`.
- **Gauge** — can go up or down. Used for current state (active connections, queue depth, project count).
- **Histogram** — records value distributions in configurable buckets. Used for latency/duration. Enables percentile queries like p99 response time.

**`Program.cs` additions:**
```csharp
app.UseHttpMetrics();  // auto-records HTTP request counts, durations, in-flight by method/path/status
app.MapMetrics();      // exposes /metrics in Prometheus text format
```

**`UseHttpMetrics` is automatic:** No code changes needed in endpoints — it wraps every HTTP request and records `http_requests_received_total`, `http_request_duration_seconds`, and `http_requests_in_progress` labelled by method, route, and status code.

**Histogram timer pattern in mutations:**
```csharp
using var timer = AppMetrics.GraphQlRequestDuration.NewTimer();
// ... do work ...
// timer disposes when the method returns — records elapsed seconds to the histogram
```

**Testing:**
```powershell
# Run the API then hit /metrics
Invoke-RestMethod http://localhost:5008/metrics
```
Look for these lines in the output:
```
# HELP taskflow_tasks_created_total Total number of tasks created
taskflow_tasks_created_total 0
# HELP taskflow_active_projects Number of active projects
taskflow_active_projects 0
```
After running a `createTask` mutation in Banana Cake Pop, `taskflow_tasks_created_total` will increment to 1.

**AKS translation:** In TASK-024 (Prometheus + Grafana), a Prometheus pod inside the k3d cluster will scrape `/metrics` from the API pods every 15 seconds. The `ServiceMonitor` CRD tells Prometheus where to find the targets.

---

## TASK-017 — Push Image to Local k3d Registry

**Commands:**
```powershell
# Rebuild with latest code
docker build -t taskflow:dev .

# Tag for the registry
docker tag taskflow:dev localhost:5050/taskflow:v1

# Push to the local registry (accessible from host at localhost:5050)
docker push localhost:5050/taskflow:v1
```

**Critical discovery — two different addresses for the same registry:**

| Context | Address to use |
|---------|---------------|
| Host machine (docker push, browser) | `localhost:5050` |
| Inside k3d cluster (k8s manifests) | `taskflow-registry:5000` |

From the host, the registry is exposed on `localhost:5050`. But inside the k3d cluster, nodes are Docker containers on an internal network — they can't reach `localhost:5050` (that resolves to the container's own loopback). They reach the registry by its Docker container name `taskflow-registry` on port `5000` (the internal port).

k3d configures containerd's registry mirror on each node (`/etc/rancher/k3s/registries.yaml`):
```yaml
mirrors:
  taskflow-registry:5000:
    endpoint:
    - http://taskflow-registry:5000
```

So all k8s manifests (Deployments, etc.) must reference images as `taskflow-registry:5000/taskflow:v1` — not `localhost:5050/taskflow:v1`.

**Verification:**
```powershell
# This FAILS — localhost:5050 not reachable from inside the cluster
kubectl run test --image=localhost:5050/taskflow:v1 --restart=Never

# This WORKS — uses internal Docker network name
kubectl run test --image=taskflow-registry:5000/taskflow:v1 --restart=Never
kubectl get pod test   # Status: Completed
kubectl delete pod test
```

**Kubeconfig connectivity note:**
After restarting the cluster or machine, `kubectl get nodes` may fail with a connection error. k3d writes `host.docker.internal` as the API server address, which doesn't resolve reliably. Fix with:
```powershell
$env:PATH += ";C:\ProgramData\chocolatey\bin"
k3d kubeconfig get taskflow | Select-String "server:"    # find current port
kubectl config set-cluster k3d-taskflow --server=https://127.0.0.1:<port>
kubectl config set-cluster k3d-taskflow --insecure-skip-tls-verify=true
```

**AKS translation:** In real AKS, the equivalent is `az acr build` (build + push in one step) or `docker push myregistry.azurecr.io/taskflow:v1`. The image reference in manifests becomes `myregistry.azurecr.io/taskflow:v1`.

**Why Docker Desktop shows images as "unused":**
After this task, Docker Desktop shows both `taskflow:dev` and `localhost:5050/taskflow:v1` marked as "unused". This is normal and not an error.

"Unused" just means no container is *currently running* from that image. `docker tag` doesn't create a second image — it adds a second name pointing to the same layers (same SHA256 digest). The test pod we ran to verify the pull completed and was deleted, so nothing is actively using the image right now.

To confirm the image is properly stored in the registry, open this in a browser:
```
http://localhost:5050/v2/taskflow/tags/list
```
Expected response: `{"name":"taskflow","tags":["v1"]}` — the image is in the registry and ready for the cluster to pull whenever a Deployment is created.

---

## TASK-018 — Kubernetes Deployment and Service Manifests

**Files created in `k8s/api/`:**

| File | Kind | Purpose |
|------|------|---------|
| `namespace.yaml` | Namespace | Isolates all TaskFlow resources under `taskflow-dev` |
| `configmap.yaml` | ConfigMap | Non-sensitive config: environment, URLs, DB name, JWT settings |
| `secret.yaml` | Secret | Sensitive config: MongoDB connection string, JWT signing key (base64) |
| `deployment.yaml` | Deployment | 2 replicas, rolling update, probes, resource limits |
| `service.yaml` | Service | ClusterIP — routes traffic to any pod with `app: taskflow-api` label |

**Apply order problem and fix:**
`kubectl apply -f k8s/api/` applies files alphabetically. `configmap.yaml` (c) and `deployment.yaml` (d) ran before `namespace.yaml` (n) existed, causing NotFound errors. Fix: apply twice — the second run succeeds because the namespace already exists. In production, use Helm (TASK-021) or kustomize which handles ordering correctly.

**ConfigMap vs Secret — what goes where:**

ConfigMap (plain text, safe to commit):
- `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`
- `MongoDb__DatabaseName`, `Jwt__Issuer`, `Jwt__Audience`

Secret (base64-encoded, **never commit real values**):
- `MongoDb__ConnectionString` — points to `host.docker.internal:27018` (Docker Compose MongoDB)
- `Jwt__Key` — signing key for JWT tokens

Secret values are only base64, not encrypted. Anyone with `kubectl get secret` access can decode them. In real AKS use Azure Key Vault with the Secrets Store CSI driver.

**`host.docker.internal` — how pods reach Docker Compose MongoDB:**
k3d cluster nodes are Docker containers. They can't reach `localhost:27018` (that's the node's own loopback). But Docker Desktop provides the special hostname `host.docker.internal` which resolves to the host machine from inside any container — so `mongodb://host.docker.internal:27018` reaches the Docker Compose MongoDB running on your machine.

**Deployment highlights:**

```yaml
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxSurge: 1        # allow 1 extra pod during rollout (3 pods briefly during deploy)
    maxUnavailable: 0  # never take a pod down before a new one is Ready
```
With `maxUnavailable: 0`, a rolling update is zero-downtime — Kubernetes waits for the new pod to pass its readiness probe before removing the old one.

**Three probes and why each matters:**

| Probe | Path | What happens on failure |
|-------|------|------------------------|
| Startup | `/health/startup` | Pod stays in "starting" — liveness/readiness don't fire yet |
| Liveness | `/health/live` | Pod is killed and restarted |
| Readiness | `/health/ready` | Pod is removed from Service load balancer (no traffic), not restarted |

**Resource limits:**
```yaml
requests: { cpu: "100m", memory: "128Mi" }   # guaranteed minimum (used for scheduling)
limits:   { cpu: "500m", memory: "256Mi" }   # hard ceiling (OOMKill if exceeded)
```
`100m` CPU = 0.1 of one core. The scheduler uses `requests` to decide which node to place the pod on. `limits` prevent a runaway pod from starving its neighbours.

**Verifying the deployment:**
```powershell
kubectl get pods -n taskflow-dev          # should show 2/2 Running
kubectl logs -n taskflow-dev -l app=taskflow-api --tail=5   # JSON log lines from both pods
kubectl describe deployment taskflow-api -n taskflow-dev    # full deployment details
```

---

## TASK-019 — NGINX Ingress (Traefik)

**File created:** `k8s/api/ingress.yaml`

**What Ingress does:**
Without Ingress, the `taskflow-api` Service is type `ClusterIP` — only reachable from inside the cluster. Ingress adds an HTTP router at the cluster edge that forwards external requests to the right Service based on hostname and/or path.

```
Browser: http://taskflow.local:8080
    ↓
localhost:8080  (k3d load balancer — maps host 8080 → cluster port 80)
    ↓
Traefik Ingress Controller (listening on cluster port 80)
    ↓  matches rule: host = taskflow.local, path = /
taskflow-api Service (ClusterIP, port 8080)
    ↓  load balances across pods
Pod 1 or Pod 2
```

**Traefik, not NGINX:**
The task spec said to install NGINX Ingress via Helm. However, k3s (the Kubernetes distribution k3d uses) ships with Traefik already installed and already bound to port 80. The k3d load balancer forwards `localhost:8080 → cluster port 80`, which reaches Traefik.

Installing NGINX as well put it on NodePort 31510 — unreachable via the k3d 8080:80 mapping. Switching `ingressClassName: traefik` in the manifest immediately fixed the routing. The NGINX controller was then uninstalled.

**Key lesson:** k3d/k3s includes Traefik by default. Always check what IngressClasses are available before installing another controller:
```powershell
kubectl get ingressclass
```

**Windows hosts file:**
To resolve `taskflow.local` to localhost, add this line (requires **Administrator** PowerShell):
```powershell
Add-Content "C:\Windows\System32\drivers\etc\hosts" "`n127.0.0.1 taskflow.local"
```

**Testing:**
```powershell
Invoke-RestMethod http://taskflow.local:8080/health/live    # returns: Healthy
Invoke-RestMethod http://taskflow.local:8080/health/ready   # returns: Healthy
```

The GraphQL IDE is also now available in a browser at `http://taskflow.local:8080/graphql`.

**AKS translation:** In real AKS, you install the NGINX Ingress Controller via Helm (or use the AKS-managed Application Gateway Ingress Controller). There's no Traefik. The Service type becomes `LoadBalancer` which provisions an Azure Load Balancer with a public IP automatically.

---

## TASK-020 — MongoDB as a StatefulSet

**Files created in `k8s/mongodb/`:**

| File | Purpose |
|------|---------|
| `secret.yaml` | MongoDB root password (base64) |
| `service.yaml` | Headless service — gives MongoDB stable DNS inside the cluster |
| `statefulset.yaml` | MongoDB pod with 1Gi persistent volume |

**StatefulSet vs Deployment:**

| | Deployment | StatefulSet |
|--|-----------|-------------|
| Pod names | Random suffix (`pod-abc123`) | Stable, ordered (`mongodb-0`, `mongodb-1`) |
| DNS | Via Service only | Each pod gets its own DNS entry |
| Storage | Shared or ephemeral | Each pod gets its own PVC |
| Start/stop order | Parallel | Sequential (0 before 1) |

MongoDB uses a StatefulSet because:
1. It needs stable pod names (`mongodb-0`) for replica set configuration
2. Each pod needs its own persistent disk — a Deployment would share one PVC across all replicas

**Headless Service (`clusterIP: None`):**
A normal Service has a virtual IP (ClusterIP) that load-balances across pods. A headless service has no virtual IP — DNS queries return the actual pod IPs directly. This is required for StatefulSets so that each pod is individually addressable:
- `mongodb-0.mongodb.taskflow-dev.svc.cluster.local` → mongodb-0 pod IP
- `mongodb-1.mongodb.taskflow-dev.svc.cluster.local` → mongodb-1 pod IP (if scaled)

The API connects to just `mongodb://admin:<password>@mongodb:27017/taskflow?authSource=admin` — the headless service name resolves to the pod's IP.

**`volumeClaimTemplates` — how persistent storage works:**
```yaml
volumeClaimTemplates:
  - metadata:
      name: mongodb-data
    spec:
      accessModes: ["ReadWriteOnce"]
      resources:
        requests:
          storage: 1Gi
```
k3d includes the `local-path` storage provisioner. When the StatefulSet creates pod `mongodb-0`, it automatically creates PVC `mongodb-data-mongodb-0` which is bound to a directory on one of the cluster nodes. Data survives pod restarts and rescheduling.

**MongoDB auth:**
Root credentials are stored in `k8s/mongodb/secret.yaml`:
- Username: `admin`
- Password: set to a value of your choice, base64-encoded (training project used a simple dev password — replace for any real deployment)

The API connection string was updated in `k8s/api/secret.yaml` to:
`mongodb://admin:<password>@mongodb:27017/taskflow?authSource=admin`

`authSource=admin` tells the driver to authenticate against the `admin` database (where the root user lives), while the data goes into the `taskflow` database.

**Rolling restart after Secret change:**
Kubernetes does NOT automatically restart pods when a Secret changes. After updating `k8s/api/secret.yaml`, a manual rollout restart is needed:
```powershell
kubectl rollout restart deployment/taskflow-api -n taskflow-dev
kubectl rollout status deployment/taskflow-api -n taskflow-dev
```

**Verifying:**
```powershell
# Ping MongoDB from inside the pod
kubectl exec -n taskflow-dev mongodb-0 -- mongosh --username admin --password <your-password> --authenticationDatabase admin --eval "db.adminCommand('ping')"
# Expected: { ok: 1 }

# Check API can reach MongoDB
Invoke-RestMethod http://taskflow.local:8080/health/ready
# Expected: Healthy
```

---

## TASK-021 — NetworkPolicy

**Files created:**
- `k8s/mongodb/networkpolicy.yaml` — restricts who can reach MongoDB
- `k8s/api/networkpolicy.yaml` — restricts what the API pods can call outbound

**What NetworkPolicy does:**
By default, all pods in a namespace can talk to all other pods freely. NetworkPolicy is a firewall at the pod level — it uses pod label selectors to allow/deny traffic.

The key rule: once ANY NetworkPolicy selects a pod, ALL traffic not explicitly allowed is denied.

**MongoDB policy — Ingress restriction:**
```yaml
podSelector:
  matchLabels:
    app: mongodb      # applies to MongoDB pods
policyTypes: [Ingress]
ingress:
  - from:
      - podSelector:
          matchLabels:
            app: taskflow-api   # only API pods allowed in
    ports:
      - protocol: TCP
        port: 27017
```
Any pod without the `app: taskflow-api` label that tries to connect to MongoDB on port 27017 gets blocked — connection times out.

**API policy — Egress restriction:**
```yaml
podSelector:
  matchLabels:
    app: taskflow-api
policyTypes: [Egress]
egress:
  - to:
      - podSelector:
          matchLabels:
            app: mongodb
    ports:
      - protocol: TCP
        port: 27017
  - ports:
      - protocol: UDP
        port: 53
      - protocol: TCP
        port: 53
```
The API can only initiate outbound connections to MongoDB (27017) and kube-dns (port 53). DNS is required — without it, the hostname `mongodb` can't be resolved to an IP.

**Egress vs Ingress policies:**
- **Ingress** controls traffic coming INTO the pod
- **Egress** controls traffic going OUT of the pod
- Response traffic for established connections is always allowed (connection tracking) — you don't need to add a reverse rule

**Why Egress doesn't block the API from serving HTTP:**
HTTP requests to the API (from Traefik) are INCOMING connections — they're controlled by Ingress policy, not Egress. Since we didn't add an Ingress policy on the API pods, all incoming traffic remains allowed.

**Verified:**
```
API → MongoDB (port 27017): ✅ ALLOWED  — /health/ready returns Healthy
test-pod → MongoDB (port 27017): ❌ BLOCKED — nc prints "BLOCKED", connection timed out
```

**AKS translation:** NetworkPolicy works identically on real AKS. AKS supports both the standard Kubernetes NetworkPolicy API and the more advanced Azure Network Policy (which adds FQDN-based rules). The manifests here work unchanged on real AKS.

---

## TASK-022 — Helm Chart Skeleton

**What is Helm and why use it?**

Up to TASK-021, each Kubernetes resource was a separate YAML file applied manually with `kubectl apply -f`. Helm is the standard package manager for Kubernetes — it bundles all those files into a single *chart* and lets you:
- Install everything in one command: `helm install taskflow helm/taskflow`
- Upgrade with one command: `helm upgrade taskflow helm/taskflow`
- Parametrise for different environments (dev vs prod) by swapping `values.yaml` files
- Track what's deployed and roll back: `helm rollback taskflow`

**Chart structure created at `helm/taskflow/`:**
```
helm/taskflow/
├── Chart.yaml          ← chart metadata (name, version, appVersion)
├── values.yaml         ← default configurable values
└── templates/
    ├── _helpers.tpl    ← Go template helper functions (naming, labels)
    ├── deployment.yaml ← Deployment template
    ├── service.yaml    ← Service template
    ├── ingress.yaml    ← Ingress template
    ├── hpa.yaml        ← HPA template (inactive by default)
    ├── serviceaccount.yaml ← ServiceAccount (inactive — create: false)
    └── NOTES.txt       ← printed after install/upgrade
```

**Values hierarchy:**
`values.yaml` defines all defaults. The template files reference them with `{{ .Values.xxx }}`. For different environments you'll create overlay files (TASK-024):
```
helm upgrade taskflow helm/taskflow -f helm/taskflow/values.dev.yaml
helm upgrade taskflow helm/taskflow -f helm/taskflow/values.prod.yaml
```

**Key values defined:**

| Value | Default | Purpose |
|-------|---------|---------|
| `image.repository` | `taskflow-registry:5000/taskflow` | In-cluster registry address |
| `image.tag` | `v1` | Overridden per release |
| `replicaCount` | `2` | Matches raw manifest |
| `ingress.host` | `taskflow.local` | Hostname (override for prod FQDN) |
| `ingress.className` | `traefik` | k3d uses Traefik; AKS uses `azure-application-gateway` |
| `autoscaling.enabled` | `false` | Enable when adding HPA in TASK-027 |
| `mongodb.connectionString` | full connection string | Injected as a Secret env var |

**Secrets in Helm — important pattern:**
The deployment template references `secretKeyRef` pointing to `taskflow-secret`. That secret is NOT templated yet (comes in TASK-023). In production, secrets are injected via:
- Azure Key Vault (CSI driver) — secrets never stored in the chart
- Sealed Secrets — encrypted secrets committed to git

For training, the raw secret value is in `values.yaml`. You would use `--set` or a `secrets.yaml` override (gitignored) in a real project.

**Difference between `values.yaml` and raw configmap.yaml:**
The raw `k8s/api/configmap.yaml` hard-codes every value. In Helm, values live in `values.yaml` and flow into templates via `{{ .Values.xxx }}`. This means you can deploy the same chart to dev and prod just by changing the values file — no YAML duplication.

**`helm lint` output:**
```
==> Linting helm/taskflow
[INFO] Chart.yaml: icon is recommended

1 chart(s) linted, 0 chart(s) failed
```
The INFO about `icon` is cosmetic (a chart icon for Artifact Hub). Zero errors — chart is valid.

**`helm template` dry-run** renders Deployment, Service, and Ingress correctly with all env vars, probes, resource limits, and Traefik ingress wired up.

**AKS translation:**
On real AKS you would:
1. Push the chart to Azure Container Registry: `az acr helm push`
2. Deploy with: `helm install taskflow oci://myregistry.azurecr.io/helm/taskflow`
3. Use `values.prod.yaml` that sets `image.repository: myregistry.azurecr.io/taskflow-api`, `ingress.className: azure-application-gateway`, and a real FQDN for `ingress.host`

---

## TASK-023 — Template All Kubernetes Resources into Helm Chart

**Goal achieved:** Every API resource from Phases 4–5 is now templated in Helm and deployed with a single `helm install` command.

**Final chart template list:**
```
helm/taskflow/templates/
├── _helpers.tpl       ← naming + label helpers (auto-generated, kept as-is)
├── configmap.yaml     ← ConfigMap with non-sensitive env vars
├── secret.yaml        ← Secret with connection string and JWT key
├── deployment.yaml    ← Deployment (RollingUpdate, probes, envFrom)
├── service.yaml       ← ClusterIP service on port 8080
├── ingress.yaml       ← Traefik ingress for taskflow.local
├── hpa.yaml           ← HPA (inactive by default; toggle with autoscaling.enabled)
├── pdb.yaml           ← PodDisruptionBudget (off in dev, on in prod)
├── serviceaccount.yaml← ServiceAccount (inactive; create: false)
└── NOTES.txt          ← Post-install instructions printed by helm
```

**Key design decisions:**

**`envFrom` instead of individual `env:` entries:**
The deployment uses `envFrom: [configMapRef, secretRef]`. This bulk-mounts all keys from both resources as environment variables. It's simpler than listing each key individually — adding a new config value only requires updating the ConfigMap, not both the ConfigMap and the deployment.

**Secrets in values.yaml — training only:**
The `secret.yaml` template reads plain-text values from `values.yaml` and base64-encodes them on the fly using Helm's `b64enc` function:
```yaml
MongoDb__ConnectionString: {{ .Values.mongodb.connectionString | b64enc | quote }}
```
In production you would NOT commit secret values in `values.yaml`. Instead:
- Pass via `--set config.jwtKey=<value>` at install time (CI/CD injects from Key Vault)
- Use Azure Key Vault CSI driver — secrets live in Key Vault, never in the chart
- Use Sealed Secrets — encrypted with cluster public key, safe to commit to git

**Helm tracks ownership via annotations:**
When `helm install` creates a resource, it adds `meta.helm.sh/release-name` and `meta.helm.sh/release-namespace` annotations. This is how `helm upgrade` knows which resources to update and `helm uninstall` knows what to delete. Raw `kubectl apply` resources have none of these — Helm will refuse to take ownership of resources it didn't create. This is why we deleted the raw resources before `helm install`.

**PodDisruptionBudget explained:**
The PDB ensures Kubernetes won't evict more pods than `minAvailable` at once during node maintenance or rolling upgrades. With `minAvailable: 1` and `replicaCount: 2`, at least 1 replica stays running during any disruption. Disabled in dev (`pdb.enabled: false`) since dev only runs 1 replica anyway.

**Install command used:**
```powershell
helm install taskflow helm/taskflow -n taskflow-dev -f helm/taskflow/values.dev.yaml
```

**Verified:**
```
pod/taskflow-57896d5cc9-5pkqs   1/1   Running  ✅
service/taskflow                ClusterIP ✅
ingress/taskflow                traefik → taskflow.local ✅
/health/live  → Healthy ✅
/health/ready → Healthy ✅
```

**Helm management commands:**
```powershell
helm list -n taskflow-dev          # see installed releases
helm status taskflow -n taskflow-dev  # check release status
helm history taskflow -n taskflow-dev # see revision history
helm upgrade taskflow helm/taskflow -n taskflow-dev -f helm/taskflow/values.dev.yaml
helm uninstall taskflow -n taskflow-dev  # removes all Helm-owned resources
```

**AKS translation:**
`helm install` / `helm upgrade` work identically on real AKS. You'd also:
- Store the chart in ACR: `helm push helm/taskflow oci://myregistry.azurecr.io/helm`
- Use Azure Pipelines to run `helm upgrade --install` in CI/CD
- Supply secrets via `--set-string` from Azure Key Vault references in the pipeline

---

## TASK-024 — Environment Overlays

**Goal:** Two value files for deploying the same chart to different environments without duplicating YAML.

**Files created:**
- `helm/taskflow/values.dev.yaml` — dev overrides (already created in TASK-023)
- `helm/taskflow/values.prod.yaml` — production overrides

**How overlays work:**
Helm merges values in order — later files override earlier keys:
```powershell
# Dev deploy (base values overridden by values.dev.yaml)
helm upgrade taskflow helm/taskflow -n taskflow-dev -f helm/taskflow/values.dev.yaml

# Prod deploy (base values overridden by values.prod.yaml)
helm upgrade taskflow helm/taskflow -n taskflow-prod -f helm/taskflow/values.prod.yaml
```
`values.yaml` is always the base. The overlay file only needs to contain keys that differ.

**Differences between dev and prod:**

| Setting | Dev | Prod |
|---------|-----|------|
| `replicaCount` | 1 | 3 (then HPA takes over) |
| `config.environment` | Development | Production |
| `config.logLevel` | Debug | Information |
| `resources.requests.cpu` | 50m | 200m |
| `resources.requests.memory` | 64Mi | 256Mi |
| `resources.limits.cpu` | 200m | 1000m |
| `resources.limits.memory` | 128Mi | 512Mi |
| `autoscaling.enabled` | false | true (3–8 replicas) |
| `pdb.enabled` | false | true (minAvailable: 2) |

**Why `replicaCount` disappears in prod manifest:**
The deployment template uses `{{- if not .Values.autoscaling.enabled }}` around `replicas:`. When HPA is enabled in prod, the `replicas:` field is omitted from the Deployment — this lets the HPA manage the count. If you hardcode `replicas: 3` and also enable HPA, they fight each other on every reconcile loop.

**Why PDB is off in dev:**
Dev only runs 1 replica. A PDB with `minAvailable: 1` on a 1-replica deployment would block ALL node maintenance (Kubernetes can never evict the only pod). So PDB is only meaningful with multiple replicas.

**helm-diff plugin (Helm 4 compatibility note):**
`helm diff upgrade` is the standard command to preview an upgrade before applying it. As of Helm v4.1.4, the plugin `helm-diff 3.15.7` has a compatibility issue (it passes the deprecated `--validate` flag that Helm 4 removed). Workaround:
```powershell
helm diff upgrade taskflow helm/taskflow -n taskflow-dev -f helm/taskflow/values.prod.yaml --dry-run=client
```
`--dry-run=client` skips live-cluster validation — the diff is still correct for template changes.

**Verified diff between dev and prod (via `helm template` comparison):**
```diff
# PodDisruptionBudget ADDED in prod (disabled in dev)
+apiVersion: policy/v1
+kind: PodDisruptionBudget
+spec:
+  minAvailable: 2

# Secret: Jwt__Key changes (different value per environment)
-  Jwt__Key: "dGFza2Zsb3ctZGV2..."
+  Jwt__Key: "UkVQTEFDRS1XSVRILVNUUk9ORy..."

# ConfigMap: environment changes
-  ASPNETCORE_ENVIRONMENT: "Development"
+  ASPNETCORE_ENVIRONMENT: "Production"

# Deployment: replicas removed in prod (HPA controls it)
-  replicas: 1

# Resources upgraded in prod
-  cpu: 200m / memory: 128Mi (limits)
+  cpu: 1000m / memory: 512Mi (limits)

# HPA ADDED in prod (disabled in dev)
+apiVersion: autoscaling/v2
+kind: HorizontalPodAutoscaler
+spec:
+  minReplicas: 3
+  maxReplicas: 8
```

**AKS translation:**
On real AKS the prod values file would also change:
- `image.repository: myregistry.azurecr.io/taskflow-api` (ACR instead of local registry)
- `ingress.className: azure-application-gateway` (AGW instead of Traefik)
- `ingress.host: taskflow.yourdomain.com` (real FQDN)
- `config.jwtKey` sourced from Azure Key Vault at deploy time via `--set` (never committed to git)

---

## TASK-025 — Deploy Prometheus and Grafana

**What was installed:**
`kube-prometheus-stack` (Prometheus Operator + Prometheus + Grafana + node-exporter + kube-state-metrics) deployed into the `monitoring` namespace via Helm.

```powershell
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts --insecure-skip-tls-verify
helm install monitoring prometheus-community/kube-prometheus-stack `
  -n monitoring --create-namespace `
  -f k8s/monitoring/prometheus-values.yaml
```

**Components running in `monitoring` namespace:**

| Pod | Role |
|-----|------|
| `monitoring-grafana` | Dashboard UI — login admin/admin |
| `monitoring-kube-prometheus-prometheus-0` | Prometheus server — stores + queries metrics |
| `monitoring-kube-prometheus-operator` | Watches for ServiceMonitor CRDs and configures Prometheus |
| `monitoring-kube-state-metrics` | Exposes k8s object metrics (pod counts, deployment status) |
| `monitoring-prometheus-node-exporter` | Exposes OS-level metrics per node (CPU, memory, disk) |

**Key config decisions in `k8s/monitoring/prometheus-values.yaml`:**

`serviceMonitorSelectorNilUsesHelmValues: false` — By default, Prometheus only watches ServiceMonitors in its own namespace (the `monitoring` namespace). Setting this to `false` combined with empty selectors tells Prometheus to discover ServiceMonitors in ALL namespaces. Without this, the `taskflow-dev` ServiceMonitor would be ignored.

`alertmanager.enabled: false` — AlertManager handles notifications (PagerDuty, Slack, email). Not needed for this training task.

`retention: 2h` — Prometheus only keeps 2 hours of data (default is 10 days). Saves disk space on the local cluster.

**ServiceMonitor explained:**
A `ServiceMonitor` is a Kubernetes custom resource (CRD) introduced by the Prometheus Operator. Instead of editing Prometheus config files, you declare what to scrape as a Kubernetes object:

```yaml
# k8s/monitoring/servicemonitor.yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: taskflow-api
  namespace: taskflow-dev
  labels:
    release: monitoring  # Must match Prometheus's serviceMonitorSelector
spec:
  selector:
    matchLabels:
      app.kubernetes.io/name: taskflow  # Matches the Helm Service labels
  endpoints:
    - port: http
      path: /metrics
      interval: 15s
```

The Prometheus Operator sees this resource and automatically reconfigures Prometheus to scrape it — no Prometheus restart needed.

**Why `release: monitoring` label?**
By default, kube-prometheus-stack's Prometheus only watches ServiceMonitors that have the label `release: monitoring`. We overrode this with `serviceMonitorSelectorNilUsesHelmValues: false`, but adding the label anyway is good practice for clarity.

**Verified:**
```
Prometheus target: job=taskflow  health=up  url=http://10.42.1.11:8080/metrics ✅
```
Prometheus is actively scraping the TaskFlow `/metrics` endpoint every 15 seconds.

**How to access:**
- Grafana: http://grafana.local:8080 — login: admin / admin
- Prometheus: `kubectl port-forward -n monitoring svc/monitoring-kube-prometheus-prometheus 9090:9090` then http://localhost:9090

**To add `grafana.local` to your hosts file** (requires admin terminal):
```powershell
# Run in PowerShell as Administrator
Add-Content C:\Windows\System32\drivers\etc\hosts "`n127.0.0.1 grafana.local"
```

**Querying TaskFlow metrics in Prometheus UI:**
Search for these metric names (from `AppMetrics.cs`):
```
taskflow_tasks_created_total
taskflow_active_projects
taskflow_graphql_request_duration_seconds_bucket
```
Standard HTTP metrics from `prometheus-net.AspNetCore`:
```
http_requests_received_total
http_request_duration_seconds_bucket
```

**AKS translation:**
On real AKS you'd use the same kube-prometheus-stack chart. Differences:
- Grafana ingress uses `ingressClassName: azure-application-gateway` with a real FQDN
- Node-exporter works identically on AKS nodes
- Azure Monitor can also scrape Prometheus metrics via the Azure Monitor workspace integration (no extra setup needed — AKS has native Prometheus support)

---

## Grafana — Exploration Guide

**Access:** http://grafana.local:8080 — login: `admin` / `admin`

> **Prerequisite:** `grafana.local` must be in your hosts file. Run once in PowerShell **as Administrator**:
> ```powershell
> Add-Content C:\Windows\System32\drivers\etc\hosts "`n127.0.0.1 grafana.local"
> ```

---

### Running queries in Explore

Go to **Explore** (compass icon on the left sidebar). You'll see a query editor with two modes:

**Builder mode** (default — good for discovery):
1. Click the **"Select metric"** dropdown and type `taskflow` — your custom metrics appear
2. Optionally add label filters (e.g. `namespace = taskflow-dev`)
3. Click the blue **Run query** button (top right)

**Code mode** (faster — write PromQL directly):
1. Click **"Code"** in the top-right of the query box
2. Type or paste a PromQL expression
3. Press **Shift+Enter** or click the blue refresh button

---

### TaskFlow custom metrics

These come from `src/TaskFlow.Api/Infrastructure/AppMetrics.cs`:

```promql
# Total tasks ever created (counter — only goes up)
taskflow_tasks_created_total

# Number of active projects right now (gauge — goes up and down)
taskflow_active_projects

# GraphQL mutation duration histogram — P95 latency
histogram_quantile(0.95, rate(taskflow_graphql_request_duration_seconds_bucket[5m]))
```

> **No data on task/project metrics?** They only update when you call the API. Create a project or task via GraphQL or REST first, then re-run the query.

---

### HTTP metrics (from prometheus-net)

These are collected automatically for every HTTP request:

```promql
# Requests per second hitting the TaskFlow API
rate(http_requests_received_total{namespace="taskflow-dev"}[1m])

# Error rate — percentage of 5xx responses
rate(http_requests_received_total{namespace="taskflow-dev", code=~"5.."}[1m])
/ rate(http_requests_received_total{namespace="taskflow-dev"}[1m]) * 100

# P95 response latency across all endpoints
histogram_quantile(0.95,
  rate(http_request_duration_seconds_bucket{namespace="taskflow-dev"}[5m])
)
```

---

### Kubernetes infrastructure metrics

These come from kube-state-metrics and node-exporter — no code changes needed:

```promql
# Number of running TaskFlow pods
count(kube_pod_info{namespace="taskflow-dev"})

# TaskFlow API container memory usage (bytes)
container_memory_working_set_bytes{namespace="taskflow-dev", container="taskflow"}

# TaskFlow API container CPU usage (cores)
rate(container_cpu_usage_seconds_total{namespace="taskflow-dev", container="taskflow"}[1m])
```

---

### Pre-built dashboards (Dashboards → Browse)

kube-prometheus-stack ships with ~30 dashboards. Most useful for this training:

| Dashboard name | What to look for |
|---------------|-----------------|
| **Kubernetes / Compute Resources / Namespace (Pods)** | Filter to `taskflow-dev` — see CPU + memory per pod live |
| **Kubernetes / Compute Resources / Cluster** | Overall cluster health across all 3 k3d nodes |
| **Node Exporter / Nodes** | OS-level: disk I/O, network, CPU steal |
| **Kubernetes / Networking / Namespace (Pods)** | Network bytes in/out per pod |

---

### Generating test traffic to populate metrics

If queries return "No data", generate some API traffic first:

```powershell
# Health checks (populates http_requests_received_total)
Invoke-RestMethod http://taskflow.local:8080/health/live
Invoke-RestMethod http://taskflow.local:8080/health/ready

# Register a user and get a JWT token
$reg = Invoke-RestMethod http://taskflow.local:8080/auth/register `
  -Method Post -ContentType "application/json" `
  -Body '{"name":"Test User","email":"test@example.com","password":"Test1234!"}'

$token = (Invoke-RestMethod http://taskflow.local:8080/auth/token `
  -Method Post -ContentType "application/json" `
  -Body '{"email":"test@example.com","password":"Test1234!"}').token

# Create a project (populates taskflow_active_projects)
Invoke-RestMethod http://taskflow.local:8080/api/projects `
  -Method Post -ContentType "application/json" `
  -Headers @{Authorization="Bearer $token"} `
  -Body '{"name":"Test Project","description":"Metrics test","workspaceId":"000000000000000000000001"}'
```

Wait ~15 seconds (one Prometheus scrape interval), then re-run your queries.

---

## TASK-026 — Build Grafana Dashboard

**Files created:**
- `k8s/monitoring/taskflow-dashboard.json` — raw Grafana dashboard JSON (importable manually)
- `k8s/monitoring/dashboard-configmap.yaml` — Kubernetes ConfigMap that auto-provisions the dashboard

**How auto-provisioning works:**
The kube-prometheus-stack Grafana installation includes a sidecar container (`grafana-sc-dashboard`) that watches for ConfigMaps across all namespaces with the label `grafana_dashboard: "1"`. When it finds one, it writes the JSON to `/tmp/dashboards/` inside the Grafana pod and Grafana hot-reloads it — no Grafana restart needed.

```yaml
metadata:
  labels:
    grafana_dashboard: "1"   # ← this label triggers the sidecar
```

**Dashboard panels (10 panels):**

| Panel | Type | PromQL |
|-------|------|--------|
| Request Rate | Stat | `sum(rate(http_requests_received_total{namespace="taskflow-dev"}[1m]))` |
| Error Rate (5xx %) | Stat | `100 * rate(5xx) / rate(total)` |
| P95 Response Latency | Stat | `histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))` |
| Tasks Created / min | Stat | `rate(taskflow_tasks_created_total[1m]) * 60` |
| Active Projects | Gauge | `taskflow_active_projects` |
| Pod Count | Stat | `count(kube_pod_info{namespace="taskflow-dev"})` |
| HTTP Request Rate by Status | Timeseries | by status code (2xx, 4xx, 5xx) |
| Latency Percentiles | Timeseries | P50, P95, P99 over time |
| MongoDB Resource Usage | Timeseries | container memory + CPU (proxy for connection pressure) |
| GraphQL Mutation Duration | Timeseries | P50 and P95 from histogram |

**Why "MongoDB connection pool saturation" uses a proxy metric:**
True MongoDB connection pool metrics require the `mongodb-exporter` sidecar (Percona's mongodb_exporter). Without it, we use container memory and CPU as a reasonable proxy — high memory and sustained CPU on the MongoDB container correlates with connection pool pressure. To add the real metric later:
```bash
helm install mongodb-exporter prometheus-community/prometheus-mongodb-exporter \
  --set mongodb.uri="mongodb://admin:<password>@mongodb.taskflow-dev.svc:27017" \
  -n taskflow-dev
```
Then the metric `mongodb_ss_connections{state="current"}` becomes available.

**Threshold colours explained:**
Each stat panel uses colour coding:
- Green → healthy range
- Yellow → warning (investigate)  
- Red → critical (alert)

For example, Error Rate: green below 1%, yellow 1–5%, red above 5%.

**To view the dashboard:**
1. Go to http://grafana.local:8080
2. Dashboards → Browse → search "TaskFlow"
3. Or direct link: http://grafana.local:8080/d/taskflow-api

**To update the dashboard:**
Edit `k8s/monitoring/taskflow-dashboard.json`, then re-apply the ConfigMap:
```powershell
kubectl create configmap taskflow-dashboard -n monitoring `
  --from-file="taskflow-dashboard.json=k8s/monitoring/taskflow-dashboard.json" `
  --dry-run=client -o yaml | kubectl apply -f -
kubectl label configmap taskflow-dashboard -n monitoring grafana_dashboard=1 --overwrite
```

**AKS translation:**
Identical process on real AKS. The ConfigMap approach (GitOps-friendly) works unchanged. In production you'd also:
- Enable Grafana persistent storage (PVC) so dashboards survive pod restarts
- Use Grafana's folder provisioning to organise dashboards by team/service
- Export dashboards from the UI as JSON and commit them to git (exactly what we've done here)

---

## TASK-027 — Jaeger Distributed Tracing

**What was deployed:** Jaeger all-in-one (`jaegertracing/all-in-one:1.57`) in the `monitoring` namespace, configured to receive traces via OTLP on port 4317.

**Files created/changed:**

| File | Change |
|------|--------|
| `k8s/monitoring/jaeger.yaml` | Jaeger Deployment + Service + Ingress |
| `src/TaskFlow.Api/Program.cs` | OTel registration + MongoDB instrumentation |
| `src/TaskFlow.Api/Infrastructure/CorrelationIdMiddleware.cs` | Tag active span with `correlation.id` |
| `src/TaskFlow.Api/appsettings.json` | Added `Otel:Endpoint` for local dev |
| `helm/taskflow/templates/configmap.yaml` | Added `Otel__Endpoint` key |
| `helm/taskflow/values.yaml` | Added `otel.endpoint` pointing to Jaeger in-cluster |

**NuGet packages added:**

| Package | Version | Purpose |
|---------|---------|---------|
| `OpenTelemetry.Extensions.Hosting` | 1.15.3 | Wires OTel into ASP.NET Core DI |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.15.2 | Auto-instruments HTTP requests (no code per endpoint) |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.15.3 | Sends traces to Jaeger via OTLP/gRPC |
| `MongoDB.Driver.Core.Extensions.DiagnosticSources` | 3.0.0 | Emits Activity spans for every MongoDB command |

**Why OTLP instead of `OpenTelemetry.Exporter.Jaeger` (as in the task spec):**
`OpenTelemetry.Exporter.Jaeger` is deprecated. The current approach is OTLP — an open standard protocol (part of the OpenTelemetry spec). Jaeger v1.35+ accepts OTLP directly, so we send OTLP to Jaeger's port 4317. This also means the same exporter config works with Grafana Tempo, AWS X-Ray, Azure Monitor, etc. — you just change the endpoint URL.

**How MongoDB spans work:**
`DiagnosticsActivityEventSubscriber` hooks into the MongoDB driver's event system. Every command (find, insert, update, aggregate) emits an `Activity` span. In `Program.cs`:
```csharp
// Wire up subscriber when creating MongoClient
mongoSettings.ClusterConfigurator = cb => cb.Subscribe(new DiagnosticsActivityEventSubscriber());

// Tell OTel to listen to MongoDB's activity source
.AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")
```

**X-Correlation-Id propagation:**
The `CorrelationIdMiddleware` now tags the active span:
```csharp
Activity.Current?.SetTag("correlation.id", correlationId.ToString());
```
This means every trace in Jaeger has the same correlation ID that's on the HTTP response header and in the Serilog logs — you can jump from a log line to its trace by searching the correlation ID.

**Trace structure verified:**
```
Trace b1edad078d64b31e  (2 spans)
  [GET /health/ready]         correlation.id = 30ccec4d-1e73-4f57...
  [listDatabases admin]       db.system = mongodb
```

**Access Jaeger UI:**
- `kubectl port-forward -n monitoring svc/jaeger 16686:16686` then http://localhost:16686
- Or via Ingress: http://jaeger.local:8080 (add `127.0.0.1 jaeger.local` to hosts as Administrator)
- Select service `taskflow-api` → Find Traces

**For local `dotnet run` development:**
Port-forward Jaeger first, then run the API:
```powershell
# Terminal 1
kubectl port-forward -n monitoring svc/jaeger 4317:4317

# Terminal 2
dotnet run --project src/TaskFlow.Api
```
The API will connect to Jaeger at `http://localhost:4317` (from `appsettings.json`).

**AKS translation:**
In real AKS you'd replace Jaeger with a managed tracing backend:
- **Azure Monitor Application Insights** — change OTLP endpoint to the AI connection string endpoint
- **Grafana Tempo** (open source) — same OTLP config, different endpoint
- No code changes needed — just update `Otel:Endpoint` in Key Vault / ConfigMap

---

## TASK-028 — Horizontal Pod Autoscaler

**Goal:** API pods scale up automatically when CPU pressure rises, then scale back down when load drops.

**How HPA was enabled:**
The HPA template already existed in the Helm chart from TASK-023 (`helm/taskflow/templates/hpa.yaml`). It's toggled via `autoscaling.enabled`. Upgraded with:
```powershell
helm upgrade taskflow helm/taskflow -n taskflow-dev `
  -f helm/taskflow/values.dev.yaml `
  --set image.tag=v2 `
  --set autoscaling.enabled=true `
  --set autoscaling.minReplicas=1 `
  --set autoscaling.maxReplicas=5 `
  --set autoscaling.targetCPUUtilizationPercentage=30
```
> Note: threshold set to 30% (instead of production's 70%) so it triggers quickly on a local cluster with modest load.

**Equivalent raw YAML (for reference):**
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: taskflow
  namespace: taskflow-dev
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: taskflow
  minReplicas: 1
  maxReplicas: 5
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 30
```

**Prerequisite — metrics-server:**
HPA requires `metrics-server` to get CPU readings from pods. k3d ships with it built in:
```
NAME             READY
metrics-server   1/1     ✅
```
Without metrics-server, HPA shows `cpu: <unknown>/30%` and never scales.

**Scale-up observed:**

| Time | CPU | Replicas | Event |
|------|-----|----------|-------|
| T+0  | 6%  | 1 | Baseline (no load) |
| T+45s | 71% | 5 | 15 concurrent workers → HPA triggered |
| T+2min | 15% | 5 | Load finishing, cooldown active |
| T+7min | 14% | 5 | Still in stabilisation window |
| T+10min | 5% | 2 | **Scaled down** — cooldown elapsed |

**Why scale-down is slow (5 minutes):**
HPA has a built-in **stabilisation window** of 300 seconds for scale-down. This prevents "flapping" — if load came back 30 seconds after scale-down, you'd immediately need to scale up again and thrash the cluster. The 5-minute window ensures the low CPU reading is stable before terminating pods.

Scale-up has no cooldown — it reacts immediately to protect availability.

**How HPA interacts with the Deployment's `replicas:` field:**
In the Helm deployment template:
```yaml
{{- if not .Values.autoscaling.enabled }}
replicas: {{ .Values.replicaCount }}
{{- end }}
```
When HPA is enabled, the `replicas:` field is removed from the Deployment spec entirely. This prevents Helm from fighting the HPA every time you run `helm upgrade` — without this pattern, `helm upgrade` would reset the pod count back to `replicaCount: 1` on every deploy.

**Load test command (PowerShell — no external tools needed):**
```powershell
$jobs = 1..15 | ForEach-Object {
    Start-Job -ScriptBlock {
        1..300 | ForEach-Object {
            Invoke-RestMethod "http://taskflow.local:8080/health/ready" -TimeoutSec 3 | Out-Null
            Invoke-RestMethod "http://taskflow.local:8080/graphql" -Method POST `
                -ContentType "application/json" `
                -Body '{"query":"{ __typename }"}' -TimeoutSec 3 | Out-Null
        }
    }
}
# Watch the HPA
kubectl get hpa -n taskflow-dev -w
```

**AKS translation:**
HPA works identically on real AKS. Additional options available on AKS:
- **KEDA** (Kubernetes Event-Driven Autoscaling) — scale on custom metrics like queue depth, not just CPU
- **Cluster Autoscaler** — scales the number of AKS *nodes* (VMs) when pods can't be scheduled due to resource exhaustion. Works alongside HPA: HPA scales pods, Cluster Autoscaler scales nodes.

---

## TASK-030 — Namespace ResourceQuota and LimitRange

**What problem do these solve?**
Without resource controls, a single namespace can claim all cluster CPU and memory, starving other workloads. Two separate Kubernetes objects handle this at different scopes:

| Object | Scope | Enforced when |
|--------|-------|---------------|
| **LimitRange** | Per *container* | Pod is created — defaults and validates individual containers |
| **ResourceQuota** | Per *namespace* | Pod is created — validates total usage across all pods in the namespace |

**Manifest:** [k8s/namespace-quotas.yaml](k8s/namespace-quotas.yaml)

**ResourceQuota — namespace ceiling:**
```yaml
apiVersion: v1
kind: ResourceQuota
metadata:
  name: taskflow-quota
  namespace: taskflow-dev
spec:
  hard:
    requests.cpu: "2"       # total CPU requests across all pods ≤ 2 cores
    requests.memory: 1Gi    # total memory requests ≤ 1 GiB
    limits.cpu: "4"         # total CPU limits ≤ 4 cores
    limits.memory: 2Gi      # total memory limits ≤ 2 GiB
    pods: "20"              # max 20 pods in the namespace
    persistentvolumeclaims: "5"
```

**LimitRange — per-container defaults and bounds:**
```yaml
apiVersion: v1
kind: LimitRange
metadata:
  name: taskflow-limits
  namespace: taskflow-dev
spec:
  limits:
    - type: Container
      default:           # applied if container specifies no limits
        cpu: 500m
        memory: 256Mi
      defaultRequest:    # applied if container specifies no requests
        cpu: 100m
        memory: 128Mi
      max:               # hard ceiling per container
        cpu: "2"
        memory: 1Gi
      min:               # hard floor per container
        cpu: 10m
        memory: 16Mi
```

**Why both?** LimitRange protects against a single container running wild (useful when a developer forgets to set limits). ResourceQuota protects against too many containers collectively exhausting the cluster. They're complementary, not redundant.

**Current usage after applying:**
```
Resource             Used    Hard
--------             ----    ----
limits.cpu           1500m   4
limits.memory        1152Mi  2Gi
persistentvolumeclaims  1     5
pods                 6       20
requests.cpu         350m    2
requests.memory      576Mi   1Gi
```
(2 taskflow pods + 1 MongoDB pod + 3 other system pods in the namespace)

**Enforcement verified — two rejection scenarios:**

1. **LimitRange blocks oversized container** (10 CPU exceeds the 2 CPU per-container max):
   ```
   Error from server (Forbidden): pods "quota-test" is forbidden:
   maximum cpu usage per Container is 2, but limit is 10
   ```

2. **ResourceQuota blocks namespace overflow** (1800m request would push total past 2 CPU hard limit):
   ```
   Error from server (Forbidden): pods "quota-test-2" is forbidden:
   exceeded quota: taskflow-quota,
   requested: requests.cpu=1800m,
   used: requests.cpu=350m,
   limited: requests.cpu=2
   ```
   Note the detailed error: it tells you exactly how much is currently used and what the hard limit is.

**What happens to existing pods when a LimitRange is added?**
LimitRange is only enforced at **pod creation time**. Already-running pods are not evicted or modified. The defaults (defaultRequest/default) are applied only to pods that don't specify their own values. If a pod was created before LimitRange was applied and had no resources set, it keeps running unchanged.

**How to check what a container actually got:**
```powershell
kubectl get pod taskflow-<hash> -n taskflow-dev -o json | ConvertFrom-Json | Select-Object -ExpandProperty spec | Select-Object -ExpandProperty containers | Select-Object name, resources
```

**AKS translation:**
ResourceQuota and LimitRange work identically on real AKS. Common AKS practice:
- Set a LimitRange in every namespace to ensure containers always have resource definitions (needed for accurate bin-packing and Cluster Autoscaler decisions)
- Set conservative namespace quotas and raise them deliberately — avoids "runaway pod" incidents where a bug causes infinite goroutines/threads and exhausts a node
- Use Azure Policy to *enforce* that every namespace has a ResourceQuota (so teams can't skip it)

---

## TASK-029 — Pod Disruption Budget (PDB) + Node Drain

**What is a PodDisruptionBudget?**
A PDB tells Kubernetes the minimum number of pods for a deployment that must stay Running during a *voluntary disruption* — things like node drains, cluster upgrades, or scaling down a node pool. Without a PDB, a drain could evict all your pods at once, causing downtime.

Voluntary disruptions (covered by PDB) include: `kubectl drain`, node pool upgrades, cluster maintenance windows.
Involuntary disruptions (NOT covered by PDB) include: hardware failure, OOM kill, node crash.

**PDB template:** [helm/taskflow/templates/pdb.yaml](helm/taskflow/templates/pdb.yaml)

```yaml
{{- if .Values.pdb.enabled }}
apiVersion: policy/v1
kind: PodDisruptionBudget
metadata:
  name: {{ include "taskflow.fullname" . }}
  namespace: {{ .Release.Namespace }}
spec:
  minAvailable: {{ .Values.pdb.minAvailable }}
  selector:
    matchLabels:
      {{- include "taskflow.selectorLabels" . | nindent 6 }}
{{- end }}
```

**Values configured:**
```yaml
# values.yaml (default)
pdb:
  enabled: true
  minAvailable: 1    # at least 1 pod must stay Running during drains

# values.dev.yaml
pdb:
  enabled: false     # no PDB in dev — single-replica deployments would block all drains

# values.prod.yaml
pdb:
  enabled: true
  minAvailable: 2    # production requires 2 pods up at all times; max disruption = 1
```

**Why `minAvailable: 1` not `maxUnavailable: 1`?**
Both express the same intent on a 2-pod deployment, but `minAvailable` is safer when the replica count grows: it always means "keep at least N pods alive", regardless of the current total. `maxUnavailable: 1` with 10 pods allows 9 evictions simultaneously — likely not what you want.

**PDB status check:**
```
NAME       MIN AVAILABLE   MAX UNAVAILABLE   ALLOWED DISRUPTIONS   AGE
taskflow   1               N/A               1                     3m
```
`ALLOWED DISRUPTIONS: 1` means Kubernetes will permit evicting 1 pod (total=2, minAvailable=1 → can evict 1). If you drain a second node while the first pod is still being rescheduled, the drain is blocked.

**Node drain observed:**

```powershell
kubectl drain k3d-taskflow-agent-0 --ignore-daemonsets --delete-emptydir-data --timeout=60s
```

Flags explained:
- `--ignore-daemonsets` — DaemonSet pods (node-exporter, kube-proxy) are not evicted because they immediately respawn on the same node; without this flag, drain refuses to proceed
- `--delete-emptydir-data` — allows eviction of pods using `emptyDir` volumes (data is ephemeral anyway)
- `--timeout=60s` — fail fast if eviction is stuck (e.g., a second PDB blocking it)

**What happened during the drain:**
1. `kubectl drain` evicted one taskflow pod from `agent-0`
2. The PDB prevented both pods from being evicted simultaneously — one pod (`taskflow-c44c55788-z42dt` on `agent-1`) stayed Running throughout
3. The evicted pod was rescheduled onto `server-0` (the only uncordoned node)
4. `/health/ready` returned `Healthy` immediately — zero downtime during the drain

**Pod state during drain:**

| Pod | Before drain | During drain | After drain |
|-----|-------------|--------------|-------------|
| taskflow-c44c55788-z42dt (agent-1) | Running | Running ✅ | Running |
| taskflow-c44c55788-68wd7 (agent-0) | Running | Evicted | Rescheduled on server-0 |

**Restore the node:**
```powershell
kubectl uncordon k3d-taskflow-agent-0
```
After uncordon, `ALLOWED DISRUPTIONS` returned to 2 (3 pods running → can lose 2 while keeping minAvailable=1).

**Why `enabled: false` in dev:**
If only 1 replica is running (dev) and PDB requires `minAvailable: 1`, *no pods can ever be evicted*. Any drain would hang indefinitely. Always disable PDB or set `minAvailable: 0` in environments with single-replica deployments.

**AKS translation:**
PDB behaviour is identical on real AKS. Key AKS-specific use case: during **node pool upgrades** (`az aks nodepool upgrade`), AKS drains nodes one at a time and respects PDBs. Without a PDB, an upgrade can take all pods of a deployment offline simultaneously. With `minAvailable: 1`, at least one pod survives each node's drain cycle — your API stays available throughout the upgrade.

```
AKS upgrade flow with PDB:
  Node 1 drain → PDB blocks eviction of pod 2 until pod 1 is rescheduled ✅
  Node 2 drain → same pattern ✅
  Result: zero-downtime node pool upgrade
```

---

## TASK-031 — Pod SecurityContext

**What it does:**
SecurityContext tells Kubernetes (and the Linux kernel) what privileges a container is allowed to use. By default a container runs with broad Linux capabilities and can write anywhere on its filesystem — a security risk if the process is compromised.

Two levels of security context exist:
- **Pod-level** (`spec.securityContext`) — applies to all containers in the pod and controls user/group identity
- **Container-level** (`spec.containers[].securityContext`) — controls per-container Linux capabilities and filesystem access

**Changes made:**

Values added to [helm/taskflow/values.yaml](helm/taskflow/values.yaml):
```yaml
podSecurityContext:
  runAsNonRoot: true   # Kubernetes rejects the pod if UID == 0
  runAsUser: 1654      # Run as the built-in 'app' user from dotnet/aspnet:10.0-alpine
  fsGroup: 1654        # Volume files are owned by GID 1654 — so 'app' user can read them

containerSecurityContext:
  allowPrivilegeEscalation: false  # Process cannot gain more privileges than its parent
  readOnlyRootFilesystem: true     # Container cannot write to its own root filesystem
  capabilities:
    drop:
      - ALL                        # Drop all Linux capabilities (NET_RAW, CHOWN, etc.)
```

Deployment template [helm/taskflow/templates/deployment.yaml](helm/taskflow/templates/deployment.yaml) changes:
```yaml
spec:
  securityContext:
    {{- toYaml .Values.podSecurityContext | nindent 8 }}
  containers:
    - securityContext:
        {{- toYaml .Values.containerSecurityContext | nindent 12 }}
      volumeMounts:
        - name: tmp
          mountPath: /tmp          # emptyDir provides a writable /tmp
  volumes:
    - name: tmp
      emptyDir: {}
```

**Why `runAsUser: 1654` not `1000`?**
The TASKS.md spec suggests UID 1000, but the base image `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` has a built-in `app` user with UID 1654 (Microsoft standardised this in .NET 8). The published application files are `--chown=app:app` in the Dockerfile. Using the wrong UID risks filesystem permission mismatches. Always match `runAsUser` to the actual file owner in the image.

To verify the UID of a base image: `docker run --rm mcr.microsoft.com/dotnet/aspnet:10.0-alpine id app`

**Why `readOnlyRootFilesystem: true` needs an emptyDir?**
ASP.NET Core (and many frameworks) write temporary files to `/tmp` — for example, data protection key files, compressed response buffers, and compile-on-first-run artifacts. With `readOnlyRootFilesystem: true`, any write to the container's root filesystem returns `EROFS`. The `emptyDir` volume at `/tmp` shadows the read-only `/tmp` with a writable in-memory volume. The emptyDir is per-pod and ephemeral — wiped when the pod is deleted, which is correct for temp files.

**Verified on the running pod:**
```json
// spec.securityContext
{
  "fsGroup": 1654,
  "runAsNonRoot": true,
  "runAsUser": 1654
}

// spec.containers[0].securityContext
{
  "allowPrivilegeEscalation": false,
  "capabilities": { "drop": ["ALL"] },
  "readOnlyRootFilesystem": true
}
```
Health check after rollout: `Healthy` — the restriction did not break the app.

**What each setting actually prevents:**

| Setting | Without it | With it |
|---------|-----------|---------|
| `runAsNonRoot: true` | A misconfigured image running as root can modify system files | Pod fails to start if image UID == 0 |
| `runAsUser: 1654` | Container runs as whoever the Dockerfile declares (could be root) | Forces a specific UID regardless of image |
| `allowPrivilegeEscalation: false` | A child process (e.g., via `sudo` or setuid binary) could gain root | `PR_SET_NO_NEW_PRIVS` set — privilege escalation blocked at kernel level |
| `readOnlyRootFilesystem: true` | Malware could write scripts to `/usr/bin`, modify the app, etc. | Writes to container FS fail at kernel level; only mounted volumes are writable |
| `capabilities: drop: ALL` | Container has capabilities like `NET_RAW` (raw sockets for ARP spoofing), `CHOWN`, `KILL` | Only the minimum POSIX capability set remains — no network raw socket, no arbitrary kill |

**AKS translation:**
Azure Kubernetes Service can enforce these settings cluster-wide via **Azure Policy** built-in initiatives:
- *"Kubernetes cluster containers should not run with elevated privileges"* — enforces `allowPrivilegeEscalation: false`
- *"Kubernetes cluster pods and containers should only run with approved user and group IDs"* — validates runAsUser ranges
- *"Kubernetes cluster containers should have a read-only root filesystem"* — enforces readOnlyRootFilesystem

In a real AKS production cluster, security contexts are enforced by policy, not just convention — a Helm chart without them would be rejected at deploy time.

---

## TASK-032 — RBAC for the API ServiceAccount

**What is RBAC?**
Role-Based Access Control limits what a Kubernetes identity (a ServiceAccount) is allowed to do with the Kubernetes API. By default, every pod is assigned the `default` ServiceAccount and gets a mounted token that can call the k8s API. If a container is compromised, an attacker could use that token to list secrets, exec into other pods, or escalate privileges within the cluster.

The fix: give the API its own minimal ServiceAccount with only the permissions it genuinely needs, and don't mount the token unless the app actually calls the k8s API.

**Three new resources in the Helm chart:**

1. **ServiceAccount** ([helm/taskflow/templates/serviceaccount.yaml](helm/taskflow/templates/serviceaccount.yaml)) — a dedicated identity for the pod
2. **Role** ([helm/taskflow/templates/role.yaml](helm/taskflow/templates/role.yaml)) — defines what the SA is allowed to do
3. **RoleBinding** ([helm/taskflow/templates/rolebinding.yaml](helm/taskflow/templates/rolebinding.yaml)) — attaches the Role to the SA

**Role — minimal permissions:**
```yaml
rules:
  - apiGroups: [""]
    resources: ["configmaps"]
    verbs: ["get", "list", "watch"]
```
The API only needs to read ConfigMaps (if it ever reads config dynamically from the cluster). It gets nothing else — not pods, not secrets, not deployments.

**values.yaml changes:**
```yaml
serviceAccount:
  create: true
  name: "taskflow-api"
  automount: false    # do NOT mount the token — we don't need k8s API access
```

**Deployment template changes** ([helm/taskflow/templates/deployment.yaml](helm/taskflow/templates/deployment.yaml)):
```yaml
spec:
  serviceAccountName: {{ include "taskflow.serviceAccountName" . }}
  automountServiceAccountToken: {{ .Values.serviceAccount.automount }}
```

**`automountServiceAccountToken: false` — what it means:**
When this is `false`, Kubernetes does not create or mount the `kube-api-access-*` projected volume into the pod. Previously this volume was always present:
```
volumes: [tmp, kube-api-access-bz2vh]   # before TASK-032
volumes: [tmp]                           # after TASK-032
```
With no token mounted, even if a container is fully compromised, the attacker has no credential to call the Kubernetes API. The token simply isn't there.

**RBAC verified:**
```
can-i get configmaps (taskflow-dev):  yes   ← the one permission it needs
can-i get pods (taskflow-dev):        no    ✅
can-i get secrets (taskflow-dev):     no    ✅
can-i delete pods (taskflow-dev):     no    ✅
```

**RBAC object relationships:**
```
ServiceAccount: taskflow-api
        │
        └──(RoleBinding: taskflow-reader)──► Role: taskflow-reader
                                                    └── get/list/watch configmaps
```
A Role is namespace-scoped — it can only grant permissions within `taskflow-dev`. A ClusterRole would span the entire cluster; never use that unless absolutely necessary.

**Why not just use the `default` ServiceAccount?**
You could grant the `default` SA permissions, but then every pod in the namespace (including pods from third-party tools that also use `default`) would inherit those permissions. A dedicated SA means the scope is exactly one application.

**AKS translation:**
RBAC is enabled by default on AKS — you cannot turn it off. Best practices on AKS:
- Use **Workload Identity** (formerly Pod Identity) to replace ServiceAccount tokens with Azure AD federated credentials. Instead of a k8s token, the pod gets an Azure AD token that can access Key Vault, Storage, etc. without storing credentials anywhere.
- **Azure RBAC for Kubernetes** — lets you use Azure AD groups to control `kubectl` access to the cluster (so your AAD admin group gets `cluster-admin`, developers get namespace-scoped access, etc.)
- The principle demonstrated here — minimal SA permissions + no token mount — applies identically to AKS. The only difference is the federation mechanism for external Azure resource access.

---

## TASK-033 — Makefile for the Dev Workflow

**Why a Makefile?**
As the project grows, the deploy workflow involves many sequential commands: build → tag → push → helm upgrade. A Makefile gives a single documented entrypoint for every common operation — no need to remember exact flags or command order. It also makes CI/CD straightforward since a pipeline just calls `make release`.

**File:** [Makefile](Makefile)

**Available targets:**
```
make dev           # docker compose up --build (local dev, hot reload)
make build         # dotnet build TaskFlow.slnx
make test          # dotnet test TaskFlow.slnx
make docker-build  # docker build + tag as localhost:5050/taskflow:v1
make docker-push   # docker push to k3d local registry
make deploy        # helm upgrade --install (includes kubeconfig fix)
make rollback      # helm rollback taskflow (goes to previous revision)
make status        # kubectl get all -n taskflow-dev
make logs          # kubectl logs ... --follow (Ctrl+C to stop)
make clean         # helm uninstall + kubectl delete namespace
make release       # docker-build + docker-push + deploy in sequence
make kubeconfig    # fix k3d connection after cluster restart
```

**Overriding the image tag:**
```bash
make docker-build docker-push deploy IMAGE_TAG=v2
# or
make release IMAGE_TAG=v2
```
The `?=` operator in Make means "use this default unless the caller sets it" — so `IMAGE_TAG ?= v1` is overridden by `IMAGE_TAG=v2` on the command line.

**Key design decisions:**

- `kubeconfig` is a prerequisite of `deploy`, `rollback`, `status`, `logs`, `clean` — so the k3d connection is always fixed before any cluster operation. You never need to remember to run it manually first.
- `helm upgrade --install` is used instead of separate `helm install` / `helm upgrade` — it's idempotent: works whether the release exists or not.
- `|| true` on clean targets prevents `make` from failing if the release or namespace doesn't exist (already cleaned).
- `SHELL := bash` at the top ensures recipes run in Git Bash on Windows, not CMD.
- Tools (`helm`, `kubectl`, `make`) are co-located in `~/.local/bin` — this is necessary on Windows because GNU Make uses CreateProcess to run recipes and does not inherit the shell's PATH modifications.

**How to deploy a new version end-to-end:**
```bash
# 1. Make a code change
# 2. Run:
make release IMAGE_TAG=v2
# This does: docker build → tag as localhost:5050/taskflow:v2 → push → helm upgrade --set image.tag=v2
# 3. Kubernetes rolls out the new image with zero downtime (RollingUpdate, maxUnavailable=0)
```

**AKS translation:**
The Makefile stays identical on real AKS. The only lines that change:
```makefile
# Local k3d
REGISTRY_HOST := localhost:5050

# Real AKS (Azure Container Registry)
REGISTRY_HOST := mycompany.azurecr.io

# Local: no kubeconfig fix needed (or use az aks get-credentials)
kubeconfig:
    az aks get-credentials --resource-group rg-taskflow --name aks-taskflow
```
The `make release` target becomes the core of your CI/CD pipeline — GitHub Actions / Azure DevOps just calls it with the correct image tag from the build number.

---

## TASK-034 — Zero-Downtime Rolling Deployment

**Goal:** Demonstrate that Kubernetes deploys a new version without dropping a single request.

**What was changed for v2:**
- `AppVersion` bumped from `"1.0.0"` → `"2.0.0"` in Serilog enrichment (Program.cs:28)
- Added a `/version` endpoint: `app.MapGet("/version", () => new { version = "2.0.0" });`

These two lines are enough to produce a genuinely different Docker image (different config digest in the registry).

**Rolling update mechanics (configured since TASK-023):**
```yaml
strategy:
  type: RollingUpdate
  rollingUpdate:
    maxSurge: 1         # at most 1 extra pod above desired count during update
    maxUnavailable: 0   # never take a pod offline before a replacement is ready
```
With 2 replicas: Kubernetes starts a v2 pod (now 3 running), waits for it to pass readinessProbe, then terminates one v1 pod (back to 2). Repeats for the second pod. At no point are fewer than 2 pods healthy.

**Load test result:**
```
t=20  e=0
t=40  e=0
...
t=260 e=0
FINAL: requests=270 errors=0
```
270 requests at 5 req/s across the full rollout window — **zero errors**.

**Verifying v2 is running:**
```bash
curl http://taskflow.local:8080/version
# → {"version":"2.0.0"}
```
Pod image digest confirmed different from v1 (sha256:ff1a0f5e... vs sha256:d0892dcd...).

**Important k3d discovery: `k3d image import` is required**

When running `imagePullPolicy: IfNotPresent` (the production default), k3d nodes use their local containerd cache. Simply pushing a new tag to the registry is NOT enough — nodes cache the tag-to-digest mapping and won't pull the new image until the cache is invalidated.

The fix: after pushing to the registry, also import the image directly into all k3d nodes:
```bash
docker tag localhost:5050/taskflow:v2 taskflow-registry:5000/taskflow:v2
k3d image import taskflow-registry:5000/taskflow:v2 -c taskflow
```

This is now built into `make docker-push`:
```makefile
docker-push:
    docker push $(IMAGE_LOCAL)
    docker tag $(IMAGE_LOCAL) $(IMAGE_CLUSTER)
    k3d image import $(IMAGE_CLUSTER) -c $(K3D_CLUSTER)
```

**Why this doesn't matter on real AKS:**
On AKS, nodes pull images from Azure Container Registry (ACR) over the network. There is no local containerd pre-cache — every new pod with a new tag pulls fresh from ACR. The `k3d image import` step is local-cluster-only and should not appear in a production Makefile or CI pipeline targeting AKS.

**How the readinessProbe prevents downtime:**
```
Pod lifecycle during rolling update:
  v2 pod starts → fails readinessProbe → NOT added to Service endpoints
  v2 pod passes readinessProbe → ADDED to Service endpoints
  v1 pod receives SIGTERM → existing connections drain (terminationGracePeriodSeconds)
  v1 pod terminates → removed from Service endpoints
```
The Service (and Ingress) only route traffic to pods that have passed their readinessProbe. This is why 0 requests failed — no traffic reached the v2 pod until it was Ready, and v1 stayed in the rotation until v2 was confirmed healthy.

**AKS translation:**
- Rolling update strategy is identical on AKS
- `maxSurge: 1` / `maxUnavailable: 0` is the recommended production pattern
- For truly instantaneous cutover (blue/green), AKS supports swapping Ingress backends between two separate Deployments — all traffic flips at once with zero transition period

---

## TASK-035 — Rollback Simulation

**Goal:** Simulate a bad deployment and recover from it safely using `helm rollback`.

**How the broken v3 was created:**
```csharp
// Added right after var builder = WebApplication.CreateBuilder(args);
throw new InvalidOperationException("v3 deliberate crash — rollback demo");
```
This causes the process to exit immediately before it can bind to any port. The container exits with a non-zero code → Kubernetes sees it as a crashed container.

**What happened during the bad deploy:**

1. `helm upgrade ... --set image.tag=v3` succeeded (Helm only validates the manifest, not whether the app runs)
2. Kubernetes started one v3 pod (maxSurge: 1 → 3 pods total)
3. v3 pod crashed immediately: `CrashLoopBackOff`
4. v3 pod never passed `startupProbe` → never became Ready → never added to Service endpoints
5. `kubectl rollout status` timed out: `"timed out waiting for the condition"`
6. The two v2 pods stayed untouched (maxUnavailable: 0 → Kubernetes won't evict a running pod until a replacement is confirmed Ready)

```
Pod state during stalled rollout:
  taskflow-67585db756-q4nkx   0/1   CrashLoopBackOff  ← v3 stuck here
  taskflow-f4f87c4f5-9xlhq    1/1   Running           ← v2 still serving
  taskflow-f4f87c4f5-srvsg    1/1   Running           ← v2 still serving
```

**Traffic during the broken deploy:**
```
curl http://taskflow.local:8080/health/ready
→ Healthy    ← v2 still serving, zero customer impact
```

**The rollback command:**
```bash
helm rollback taskflow -n taskflow-dev
# (no revision number = go to previous revision)
# or explicitly:
helm rollback taskflow 9 -n taskflow-dev
```
Rollback is simply another Helm upgrade — it re-applies the manifests from a previous revision. Kubernetes treats it as a normal rolling update back to the old image tag.

**After rollback:**
```
REVISION  STATUS      DESCRIPTION
9         superseded  Upgrade complete         ← was v2
10        superseded  Upgrade complete         ← was v3 (broken)
11        deployed    Rollback to 9            ← current: v2 restored
```
`/health/ready` → `Healthy`
`/version` → `{"version":"2.0.0"}`

**`helm rollback` vs `kubectl rollout undo`:**

| | `helm rollback` | `kubectl rollout undo` |
|--|--|--|
| Reverts | Full Helm release (all templates) | Only the Deployment resource |
| Preserves history | Yes — adds a new revision | Yes — adds a new rollout revision |
| Scope | ConfigMaps, Secrets, Ingress, etc. | Deployment spec only |
| Recommended for | Helm-managed apps (always) | Quick hotfix if not using Helm |

Always use `helm rollback` for Helm-managed releases — `kubectl rollout undo` only reverts the Deployment and leaves ConfigMaps, Secrets, and other resources at the broken version.

**Why `maxUnavailable: 0` is the hero here:**
If `maxUnavailable` were 1, Kubernetes could have terminated a v2 pod to make room for the v3 pod, leaving only 1 pod running while v3 was crashing. With `maxUnavailable: 0`, Kubernetes is conservative: never remove a healthy pod until there is a confirmed ready replacement. The broken deploy stalls but never causes downtime.

**Cleanup:**
After the demo, the `throw` was removed from Program.cs and `values.yaml` was updated to `tag: "v2"` so `make deploy` deploys v2 by default going forward.

**AKS translation:**
Helm rollback works identically on AKS. Additional AKS patterns:
- **Azure DevOps release gates** — can automatically trigger `helm rollback` if a deployment health check fails after a configurable window
- **GitOps with Flux/Argo CD** — rollback is a git revert; the GitOps operator detects the change and reconciles the cluster back to the previous state without manual `helm rollback`

---

## TASK-036 — Scaffold React + Vite + TypeScript Frontend

**Stack installed:**

| Package | Role |
|---------|------|
| `vite` + `@vitejs/plugin-react` | Build tool + HMR dev server |
| `react` + `react-dom` v19 | UI framework |
| `react-router-dom` v7 | Client-side routing |
| `@apollo/client` v4 + `graphql` | GraphQL client |
| `graphql-ws` | WebSocket transport for subscriptions |
| `tailwindcss` v4 + `@tailwindcss/vite` | Utility CSS (Vite plugin, no config file needed) |
| `@graphql-codegen/*` | Auto-generates typed React hooks from the schema |

**Tailwind v4 — what changed from v3:**
Tailwind v4 dropped `tailwind.config.js` and `npx tailwindcss init`. Configuration lives entirely in CSS using `@theme` directives. For Vite, use `@tailwindcss/vite` plugin instead of the PostCSS approach:
```ts
// vite.config.ts
import tailwindcss from '@tailwindcss/vite'
plugins: [react(), tailwindcss()]
```
```css
/* src/index.css */
@import "tailwindcss";
```
That's all — no config file, no content paths to specify.

**Vite dev proxy** ([frontend/vite.config.ts](frontend/vite.config.ts)):
```ts
server: {
  proxy: {
    '/graphql': { target: 'http://localhost:5000', ws: true },
    '/api':     { target: 'http://localhost:5000' },
    '/auth':    { target: 'http://localhost:5000' },
    '/health':  { target: 'http://localhost:5000' },
  }
}
```
Why a proxy? The frontend runs on `localhost:5173`, the API on `localhost:5000`. Browsers block cross-origin requests unless CORS is configured. The Vite proxy rewrites paths so the browser thinks everything is on `localhost:5173` — no CORS issues in dev. In production (Kubernetes), Traefik routes by path prefix, achieving the same result at the ingress level.

The `ws: true` on `/graphql` enables WebSocket proxying for GraphQL subscriptions.

**GraphQL Code Generator** ([frontend/codegen.yml](frontend/codegen.yml)):
```yaml
schema: "http://localhost:5000/graphql"
documents: "src/**/*.graphql"
generates:
  src/generated/graphql.ts:
    plugins:
      - typescript
      - typescript-operations
      - typescript-react-apollo
    config:
      withHooks: true
```
Run: `npm run codegen` (requires the API to be running).
Output: `src/generated/graphql.ts` with fully-typed hooks like `useGetWorkspacesQuery`, `useCreateProjectMutation`, etc. — no hand-writing Apollo boilerplate.

**Docker Compose dev service** ([docker-compose.yml](docker-compose.yml)):
```yaml
frontend:
  build: { context: ./frontend, dockerfile: Dockerfile.dev }
  ports: ["5173:5173"]
  volumes:
    - ./frontend:/app
    - /app/node_modules    # prevents host node_modules from shadowing container's
  depends_on: [api]
```
The `node_modules` volume trick: mounting `./frontend` into `/app` would overwrite the container's `node_modules` with the host's (or worse, an empty directory). The anonymous volume `/app/node_modules` shadows that specific subdirectory, keeping the container's installed packages intact while host source files still hot-reload.

**`src/generated/` is gitignored** (created fresh by `npm run codegen` against the live schema — committing generated code creates merge conflicts and gets out of sync with the schema).

**Done when:** `npm run dev` starts Vite on port 5173; TypeScript compiles with zero errors.

---

## TASK-037 — Apollo Client + GraphQL Code Generator

**Files created:**
- [frontend/src/apollo/AuthContext.tsx](frontend/src/apollo/AuthContext.tsx) — JWT stored in React state (not localStorage)
- [frontend/src/apollo/client.ts](frontend/src/apollo/client.ts) — Apollo client with HTTP + WebSocket links
- [frontend/src/main.tsx](frontend/src/main.tsx) — wraps app in `ApolloProvider` + `AuthProvider`
- [frontend/src/graphql/*.graphql](frontend/src/graphql/) — operation definitions for all queries, mutations, subscriptions
- [frontend/schema.graphql](frontend/schema.graphql) — downloaded SDL from the running API

**Apollo client architecture** ([frontend/src/apollo/client.ts](frontend/src/apollo/client.ts)):
```
Request
  ↓
split(isSubscription?)
  ├── yes → GraphQLWsLink (WebSocket to /graphql)
  └── no  → authLink → HttpLink (HTTP POST to /graphql)
```

- **`authLink`** — reads the token from `getTokenRef()` (a module-level variable, not a hook — Apollo links run outside the React render cycle) and injects `Authorization: Bearer <token>` into the request headers
- **`HttpLink`** — sends to `/graphql` (Vite proxies this to the API in dev; Traefik handles routing in Kubernetes)
- **`GraphQLWsLink`** — creates a WebSocket connection for subscriptions; passes the token in `connectionParams`
- **`split`** — routes based on operation type: subscriptions go over WS, everything else over HTTP

**Why not store the token in localStorage?**
localStorage is accessible from any JavaScript on the page. If a dependency has an XSS vulnerability, it can steal the token and impersonate the user from any origin. An in-memory variable is cleared when the browser tab closes and is inaccessible to injected scripts.

The tradeoff: page refresh loses the token — the user has to log in again. For a task management tool this is acceptable; for a consumer app you'd use httpOnly cookies instead (immune to XSS, automatic expiry).

**The `getTokenRef` / `setTokenRef` pattern:**
React hooks can't be called outside components, but Apollo's `authLink` is a plain function that runs during the request pipeline. The solution: a module-level variable (`_tokenRef`) that is written from the `AuthContext` whenever the token changes and read by the Apollo link without needing React.

**Getting the schema for codegen:**
HotChocolate v16 disables `__schema` introspection in Production mode (error: *"Introspection is not allowed for the current request"*). However it exposes a schema SDL download endpoint: `GET /graphql?sdl`. This returns the full schema as an SDL file without needing introspection access.

The downloaded SDL is stored at [frontend/schema.graphql](frontend/schema.graphql). `codegen.yml` points at the local file so `npm run codegen` works without a running server:
```yaml
schema: "./schema.graphql"   # local SDL — no live API required
documents: "src/**/*.graphql"
```

When the API schema changes, regenerate: `curl http://taskflow.local:8080/graphql?sdl > schema.graphql && npm run codegen`

**Generated hooks** (1145 lines in `src/generated/graphql.ts`):
```typescript
// Queries
useGetWorkspacesQuery()
useGetProjectsQuery({ variables: { workspaceId } })
useGetTasksQuery({ variables: { projectId } })

// Mutations
useCreateWorkspaceMutation()
useCreateProjectMutation()
useUpdateTaskMutation()

// Subscriptions
useOnTaskUpdatedSubscription({ variables: { projectId } })
useOnCommentAddedSubscription({ variables: { taskId } })
```
Each hook is fully typed — variables, return data, and loading/error states all have TypeScript types derived from the schema and operation definitions.

---

## TASK-038 — Login Page (JWT in Memory)

**Files created:**
- [frontend/src/pages/Login/LoginPage.tsx](frontend/src/pages/Login/LoginPage.tsx) — login + register form
- [frontend/src/components/ProtectedRoute.tsx](frontend/src/components/ProtectedRoute.tsx) — redirects to `/login` when no token
- Updated [frontend/src/apollo/AuthContext.tsx](frontend/src/apollo/AuthContext.tsx) — stores `AuthUser` (token + userId + email + role)
- Updated [frontend/src/App.tsx](frontend/src/App.tsx) — full route structure

**AuthContext redesign:**
```tsx
interface AuthUser { token: string; userId: string; email: string; role: string }

// Module-level ref (readable by Apollo links without React hooks)
let _tokenRef: string | null = null
export function getTokenRef() { return _tokenRef }

// React context (for components)
export function AuthProvider({ children }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const login = useCallback((u: AuthUser) => { _tokenRef = u.token; setUser(u) }, [])
  const logout = useCallback(() => { _tokenRef = null; setUser(null) }, [])
  ...
}
```
`login()` sets both the module-level ref (for Apollo) and the React state (for components) atomically.

**Login flow:**
```
User fills form → POST /auth/token → { token, userId, email, role, expiresAt }
                                          ↓
                                    login(user) → _tokenRef = token (Apollo reads this)
                                          ↓
                                    setUser(user) → React re-renders
                                          ↓
                                    navigate('/') → ProtectedRoute allows through
```

**The fetch call uses `/auth/token` (no base URL):**
```typescript
const res = await fetch('/auth/token', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ email, password }),
})
```
Vite proxies `/auth/*` to the API. In production (Kubernetes), Traefik routes `/auth/` to the API service at the ingress level — the same relative path works in both environments.

**ProtectedRoute:**
```tsx
export default function ProtectedRoute({ children }) {
  const { user } = useAuth()
  if (!user) return <Navigate to="/login" replace />
  return <>{children}</>
}
```
`replace` prevents the login page from being added to browser history — pressing Back after login doesn't go back to `/login`.

**Route structure:**
```
/login          → LoginPage (public)
/               → DashboardPage (protected)
/projects/:id   → BoardPage (protected)
*               → redirect to /
```

**UI: combined Login + Register form:**
The login page includes a "Register" toggle so a new user can create an account in one place. This is practical for a training demo — no separate `/register` route needed.

**Test user created:**
```
email:    souvika@taskflow.local
password: Test1234!
userId:   6a18f2457a1eb11f99666663
role:     Member
```

**Proxy verified:**
`POST http://localhost:5173/auth/token` → Vite proxy → `http://taskflow.local:8080/auth/token` → returned a 411-character JWT.

**Vite proxy target for k3d dev:**
The proxy in `vite.config.ts` was updated to `http://taskflow.local:8080` (the k3d ingress) instead of `http://localhost:5000` (docker-compose). When using docker-compose for local dev, change it back to `localhost:5000`.

**AKS translation:**
In production, the frontend is served by Nginx and the API sits behind Azure API Management or Traefik (at the ingress). The `/auth`, `/graphql`, `/api` path prefixes are routed at the ingress level — the frontend code never changes between environments because it always uses relative paths.

**Bug fixed — blank white page (Apollo Client v4 breaking change):**
Apollo Client v4 split the unified `@apollo/client` package: `ApolloProvider` and all React hooks (`useQuery`, `useMutation`, etc.) moved to `@apollo/client/react`. Importing `ApolloProvider` from the main `@apollo/client` entry returned `undefined`, causing React to throw silently on render → blank white page.

Two fixes:
1. `main.tsx`: `import { ApolloProvider } from '@apollo/client/react'`
2. `codegen.yml`: Added `apolloReactHooksImportFrom: '@apollo/client/react'` so generated hooks import from the right subpath

| Apollo v3 | Apollo v4 |
|-----------|-----------|
| `import { ApolloProvider, useQuery } from '@apollo/client'` | `import { ApolloProvider, useQuery } from '@apollo/client/react'` |
| `import { ApolloClient, split } from '@apollo/client'` | Same — core types stay in `@apollo/client` |

---

## TASK-039 — Dashboard Page

**Files created/updated:**
- [frontend/src/pages/Dashboard/DashboardPage.tsx](frontend/src/pages/Dashboard/DashboardPage.tsx) — new
- [frontend/src/App.tsx](frontend/src/App.tsx) — replaced placeholder `DashboardPage` with real import

**Layout:**
```
┌─────────────────────────────────────────────────────┐
│  Nav bar: logo + email + Sign out                   │
├──────────────┬──────────────────────────────────────┤
│  Workspaces  │  [Workspace name]        + New Proj  │
│  sidebar     │                                      │
│  ─────────   │  Project card  Project card  ...     │
│  My WS  ←   │  (links to /projects/:id)            │
│  + New WS    │                                      │
└──────────────┴──────────────────────────────────────┘
```

**Apollo hooks used:**
| Hook | When |
|------|------|
| `useGetWorkspacesQuery` | On mount — loads all workspaces; auto-selects the first one |
| `useGetProjectsQuery` | Whenever `selectedWorkspaceId` changes; skipped if null |
| `useCreateWorkspaceMutation` | On workspace form submit |
| `useCreateProjectMutation` | On project form submit |

**Cache refresh after mutations:**
Both mutations use `refetchQueries` to re-fetch the relevant list after creation:
```typescript
useCreateProjectMutation({
  refetchQueries: [
    { query: GetProjectsDocument, variables: { workspaceId: selectedWorkspaceId } }
  ]
})
```
This is simpler to understand than `cache.modify` for filtered queries, and correct: the list re-fetches once and the new item appears. The tradeoff vs `cache.modify`: one extra network round-trip, but the data is authoritative (server-confirmed, not just optimistically written).

**`skip` pattern for dependent queries:**
```typescript
useGetProjectsQuery({
  variables: { workspaceId: selectedWorkspaceId! },
  skip: !selectedWorkspaceId,   // do not run until a workspace is selected
})
```
`skip: true` prevents Apollo from sending the query. The `!` assertion on the variable is safe here because the query won't run when `selectedWorkspaceId` is null.

**`onCompleted` for auto-select:**
```typescript
useGetWorkspacesQuery({
  onCompleted(data) {
    if (!selectedWorkspaceId && data.workspaces.length > 0) {
      setSelectedWorkspaceId(data.workspaces[0].id)
    }
  }
})
```
The first workspace is selected automatically so the user sees projects immediately on login.

**Loading states:**
- Workspaces sidebar: skeleton list of grey rounded boxes (animate-pulse)
- Projects grid: 3 skeleton cards with pulsing placeholder lines
- Error states: red bordered message with the Apollo error text

**Project status badge:**
Projects have a `ProjectStatus` enum (`Active`, `OnHold`, `Archived`). Each status gets a colour-coded pill badge on the card.

---

## TASK-040 — Project Board with Real-Time Subscriptions

**Files created/updated:**
- [frontend/src/pages/Board/BoardPage.tsx](frontend/src/pages/Board/BoardPage.tsx) — new
- [frontend/src/App.tsx](frontend/src/App.tsx) — replaced placeholder `BoardPage` with real import

**Board layout:**
```
┌──────────┬──────────────┬───────────┬──────┐
│  To Do   │  In Progress │ In Review │ Done │
├──────────┼──────────────┼───────────┼──────┤
│ Card ←→  │ Card ←→      │ Card ←→   │ Card │
│ Card ←→  │              │           │      │
│ + Add    │ + Add        │ + Add     │+Add  │
└──────────┴──────────────┴───────────┴──────┘
```
Each card shows its title and a priority badge. Hovering reveals ← / → arrows to move it to the adjacent column.

**Apollo patterns demonstrated:**

*1. Optimistic update (instant card movement):*
```typescript
updateTask({
  variables: { input: { id, status: newStatus, ...rest } },
  optimisticResponse: {
    updateTask: {
      __typename: 'TaskItem',
      id: task.id,
      status: newStatus,
      ...rest,
      updatedAt: new Date().toISOString(),
    },
  },
})
```
Apollo writes the optimistic result into the normalized cache (`TaskItem:<id>`) immediately. The `useGetTasksQuery` result re-computes from cache and the card moves columns before the server replies. If the server returns an error, Apollo rolls the cache back.

*2. Subscription writing directly to normalized cache:*
```typescript
useOnTaskUpdatedSubscription({
  variables: { projectId: id },
  onData({ client, data }) {
    const task = data.data?.onTaskUpdated
    client.cache.writeFragment({
      id: client.cache.identify({ __typename: 'TaskItem', id: task.id }),
      fragment: gql`fragment SubscriptionTaskUpdate on TaskItem { id status title priority updatedAt }`,
      data: task,
    })
  },
})
```
When another user moves a card, the subscription fires. `writeFragment` updates the normalized `TaskItem` object in place. Because `useGetTasksQuery` reads from the same normalized entries, it re-renders and the card moves in the second tab — no refetch needed.

**Why normalized cache makes this work:**
InMemoryCache stores each `TaskItem` once, keyed by `TaskItem:<id>`. Both `useGetTasksQuery` (which reads the list) and `updateTask` / `writeFragment` (which write individual items) operate on the same entries. Any write to `TaskItem:X` automatically propagates to all queries that reference it.

| Write source | Cache action | Who sees it |
|---|---|---|
| `optimisticResponse` | Writes `TaskItem:id` optimistically | Current tab immediately |
| Server mutation result | Overwrites optimistic with authoritative data | Current tab on confirm |
| `onData` → `writeFragment` | Writes `TaskItem:id` from subscription event | Other tab's `useGetTasksQuery` |

**To demo the real-time sync:**
1. Open two browser tabs on the same board URL
2. Move a card in tab 1 — the card moves in tab 2 within milliseconds (WebSocket push, no polling)
