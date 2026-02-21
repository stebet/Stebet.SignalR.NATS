[![NuGet](https://img.shields.io/nuget/v/Stebet.SignalR.NATS.svg)](https://www.nuget.org/packages/Stebet.SignalR.NATS)
[![codecov](https://codecov.io/gh/stebet/Stebet.SignalR.NATS/graph/badge.svg?token=x6Zb2f10w7)](https://codecov.io/gh/stebet/Stebet.SignalR.NATS)

# Stebet.SignalR.NATS

A high-performance [NATS](https://nats.io) backplane for [ASP.NET Core SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction), enabling horizontal scaling of SignalR hubs across multiple server instances.

## Installation

Install the package from NuGet:

```shell
dotnet add package Stebet.SignalR.NATS
```

## Usage

Call `AddNats` on your SignalR builder in `Program.cs`:

```csharp
builder.Services.AddSignalR()
    .AddNats("nats://localhost:4222");
```

### Options

`AddNats` accepts an optional subject prefix (default: `"signalr.nats"`) used for all NATS subjects. Override it if you run multiple SignalR applications sharing the same NATS cluster:

```csharp
builder.Services.AddSignalR()
    .AddNats("nats://localhost:4222", natsSubjectPrefix: "myapp.signalr");
```

### Connecting to a NATS cluster

Pass a comma-separated list of server URLs to connect to a NATS cluster:

```csharp
builder.Services.AddSignalR()
    .AddNats("nats://nats1:4222,nats://nats2:4222,nats://nats3:4222");
```

## Requirements

- .NET 9.0 or later
- A running [NATS Server](https://nats.io/download/) (2.x or later)

## Contributing / Development

See [DEVELOPMENT.md](DEVELOPMENT.md) for instructions on how to build the solution, run tests, and run load tests.
