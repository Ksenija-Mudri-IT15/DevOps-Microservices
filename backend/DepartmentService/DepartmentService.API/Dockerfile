FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["DepartmentService.API/DepartmentService.API.csproj", "DepartmentService.API/"]
COPY ["DepartmentService.Application/DepartmentService.Application.csproj", "DepartmentService.Application/"]
COPY ["DepartmentService.Domain/DepartmentService.Domain.csproj", "DepartmentService.Domain/"]
COPY ["DepartmentService.Infrastructure/DepartmentService.Infrastructure.csproj", "DepartmentService.Infrastructure/"]

RUN dotnet restore "DepartmentService.API/DepartmentService.API.csproj"

COPY . .
WORKDIR "/src/DepartmentService.API"
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
# curl is not in the base runtime image; the docker-compose healthcheck needs it to call /health.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DepartmentService.API.dll"]
