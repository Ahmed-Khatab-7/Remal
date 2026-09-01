# syntax=docker/dockerfile:1
# ----- Build stage -----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["src/Remal.Domain/Remal.Domain.csproj", "src/Remal.Domain/"]
COPY ["src/Remal.Application/Remal.Application.csproj", "src/Remal.Application/"]
COPY ["src/Remal.Infrastructure/Remal.Infrastructure.csproj", "src/Remal.Infrastructure/"]
COPY ["src/Remal.Api/Remal.Api.csproj", "src/Remal.Api/"]
RUN dotnet restore "src/Remal.Api/Remal.Api.csproj"

COPY . .
WORKDIR /src/src/Remal.Api
RUN dotnet publish "Remal.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ----- Runtime stage -----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Healthcheck
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Remal.Api.dll"]
