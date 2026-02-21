# Development Guide

## Prerequisites

- [.NET SDK 9.0](https://dotnet.microsoft.com/download) or later
- [Visual Studio Code](https://code.visualstudio.com/) with the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension, or [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with the ASP.NET and web development workload
- [Docker Desktop](https://www.docker.com/products/docker-desktop) — required for integration tests and load tests

## Building the Solution

```shell
dotnet build --tl
```

## Running Integration Tests

The integration tests require a NATS server on `localhost:4222`. Start one with Docker:

```shell
docker run -p 4222:4222 -p 8222:8222 --name nats -d nats:latest
```

Then run the tests:

```shell
dotnet test
```

Code coverage reports are generated automatically and published to [Codecov](https://codecov.io/gh/stebet/Stebet.SignalR.NATS).

## Running Load Tests

The load tests spin up a multi-server SignalR environment in Docker (3 SignalR servers behind a YARP proxy) and hit it with concurrent SignalR connections.

### Using VS Code Launch Profiles (recommended)

Four launch profiles are available in the Run & Debug panel:

| Profile | Backplane | Build |
|---|---|---|
| **Load Test (NATS)** | NATS cluster | Debug |
| **Load Test (Redis)** | Redis | Debug |
| **Load Test Release (NATS)** | NATS cluster | Release |
| **Load Test Release (Redis)** | Redis | Release |

Each profile automatically:
1. Builds the `Stebet.SignalR.NATS.TestServer` and YARP proxy Docker images via `dotnet publish /t:PublishContainer`
2. Starts the appropriate `docker-compose-nats.yml` or `docker-compose-redis.yml` stack
3. Builds and launches the load test
4. Tears down the Docker stack when the run finishes

### Using the CLI

**1. Build the Docker images**

```shell
dotnet publish perf/Stebet.SignalR.NATS.TestServer/Stebet.SignalR.NATS.TestServer.csproj /t:PublishContainer -p:ContainerRepository=stebet-signalr-nats-testserver -p:TargetFramework=net10.0
dotnet publish perf/Stebet.SignalR.NATS.Yarp/Stebet.SignalR.NATS.Yarp.csproj /t:PublishContainer -p:ContainerRepository=stebet-signalr-nats-yarp -p:TargetFramework=net10.0
```

**2. Start the backplane stack**

NATS:
```shell
docker compose -f docker-compose-nats.yml up -d
```

Redis (for comparison):
```shell
docker compose -f docker-compose-redis.yml up -d
```

**3. Run the load test**

```shell
dotnet run -c Release --project perf/Stebet.SignalR.NATS.LoadTest/Stebet.SignalR.NATS.LoadTest.csproj -f net10.0
```

**4. Tear down**

```shell
docker compose -f docker-compose-nats.yml down
# or
docker compose -f docker-compose-redis.yml down
```
