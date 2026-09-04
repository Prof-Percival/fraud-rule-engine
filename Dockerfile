# Multi stage, so the SDK never reaches the running image.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The build files first, then restore, then the source. A change to a .cs file leaves the restore
# layer cached, which is most of the build time.
COPY global.json Directory.Build.props Directory.Packages.props FraudRuleEngine.sln ./
COPY src/FraudRuleEngine.Domain/*.csproj src/FraudRuleEngine.Domain/
COPY src/FraudRuleEngine.Application/*.csproj src/FraudRuleEngine.Application/
COPY src/FraudRuleEngine.Infrastructure/*.csproj src/FraudRuleEngine.Infrastructure/
COPY src/FraudRuleEngine.Api/*.csproj src/FraudRuleEngine.Api/
COPY tests/Directory.Build.props tests/
COPY tests/FraudRuleEngine.Domain.Tests/*.csproj tests/FraudRuleEngine.Domain.Tests/
COPY tests/FraudRuleEngine.Application.Tests/*.csproj tests/FraudRuleEngine.Application.Tests/
COPY tests/FraudRuleEngine.Api.Tests/*.csproj tests/FraudRuleEngine.Api.Tests/
RUN dotnet restore

COPY . .
RUN dotnet build --configuration Release --no-restore

FROM build AS publish
RUN dotnet publish src/FraudRuleEngine.Api \
    --configuration Release \
    --no-build \
    --output /app

# Runs the tests inside the image, so the same result is reachable without an SDK on the host. Not part
# of the path to the runtime image, so a normal build does not pay for it.
FROM build AS test

# Building this stage runs the tests, and a failure fails the build. The default excludes anything
# needing a database, because no database is reachable while an image is being built.
ARG TEST_FILTER="Category!=Integration"
RUN dotnet test --configuration Release --no-build --filter "$TEST_FILTER"

# Running the stage instead runs everything, which is the compose path, where the database is up and
# ConnectionStrings__Default points at it.
ENTRYPOINT ["dotnet", "test", "--configuration", "Release", "--no-build"]

# Chiselled: no shell and no package manager, and it runs as a non root user already.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final
WORKDIR /app
COPY --from=publish /app .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "FraudRuleEngine.Api.dll"]
