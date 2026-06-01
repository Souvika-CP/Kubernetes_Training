# Stage 1: restore dependencies (cached unless .csproj changes)
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY TaskFlow.slnx .
COPY src/TaskFlow.Api/TaskFlow.Api.csproj src/TaskFlow.Api/
RUN dotnet restore src/TaskFlow.Api/TaskFlow.Api.csproj

# Stage 2: publish release build
FROM build AS publish
COPY src/ src/
RUN dotnet publish src/TaskFlow.Api/TaskFlow.Api.csproj -c Release -o /out --no-restore

# Stage 3: lean runtime image
# The dotnet/aspnet:10.0-alpine base image ships with a built-in non-root 'app' user
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=publish --chown=app:app /out .
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]
