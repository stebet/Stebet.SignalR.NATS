[![codecov](https://codecov.io/gh/stebet/Stebet.SignalR.NATS/graph/badge.svg?token=x6Zb2f10w7)](https://codecov.io/gh/stebet/Stebet.SignalR.NATS)
# Building and Testing Instructions

## Prerequisites

Before you begin, ensure you have the following installed:
- [.NET SDK](https://dotnet.microsoft.com/download)
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) or later with the ASP.NET and web development workload

For integration tests:
- [NATS Server](https://nats.io/download/) running on localhost:4222 to run integration tests. For ease of use you can run it via. Docker by running `docker run -p 4222:4222 -p 8222:8222 --name nats -d nats:latest` in your terminal.

For load tests:
- [Docker Desktop](https://www.docker.com/products/docker-desktop) to run the load tests in a containerized environment

## Building the Solution

1. Clone the repository to your local machine using Git
2. Navigate to the cloned repository's directory
3. Run `dotnet build --tl` to build the solution

## Running Tests

This solution includes some integration tests. To run them, follow these steps:
1. Ensure the NATS Server is running on your local machine on port `4222`. You can run it by downloading the NATS Server binary or by running it in docker using the following command: `docker run -p 4222:4222 -p 8222:8222 --name nats -d nats:latest`
2. Run the tests using the .NET CLI by running `dotnet test` in the root directory of the repository.

## Running Load Tests
There are also load tests included in the solution. To run them, follow these steps:
1. Make sure you are in the root directory of the repository
2. Publish the solution by running `dotnet publish -c Release --os linux -t:PublishContainer`.
3. Start the docker compose file by running `docker compose -f .\docker-compose-nats.yml -p signalr-nats up -d`.
    * Note: There is also a Docker Compose file to start the Redis backplane for comparison purposes. You can start it by running `docker compose -f .\docker-compose-redis.yml -p signalr-redis up -d`.
4. Run the performance tests by running `dotnet run -c Release --project .\perf\Stebet.SignalR.NATS.LoadTest\Stebet.SignalR.NATS.LoadTest.csproj`.

## Viewing Code Coverage
The project is configured to generate code coverage reports with Codecov. To view the coverage report, follow the badge link in the README.md file or visit [Codecov](https://codecov.io/gh/stebet/Stebet.SignalR.NATS) directly.
