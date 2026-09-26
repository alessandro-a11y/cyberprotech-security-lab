# Imagem de produção da API (ASP.NET Core 8).
# Multi-stage, sem dependência de WSL: funciona no Docker Desktop (Windows) e no WSL 2.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/ ./
RUN dotnet restore "API/API.csproj" \
 && dotnet publish "API/API.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
# A imagem de runtime nao traz wget nem curl, e sem um deles o healthcheck do
# Compose falha sempre. O curl serve apenas ao healthcheck.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish ./
EXPOSE 8080
# Porta interna 8080 (padrão das imagens ASP.NET 8). Mapeada no docker-compose.
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "API.dll"]
