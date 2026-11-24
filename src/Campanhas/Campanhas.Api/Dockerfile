# syntax=docker/dockerfile:1
# Contexto de build: raiz do repositório.
#   docker build -f src/Campanhas/Campanhas.Api/Dockerfile -t campanhas-api .

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada própria: só muda quando um .csproj ou as versões centrais mudam.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/BuildingBlocks/ConexaoSolidaria.Contracts/ConexaoSolidaria.Contracts.csproj src/BuildingBlocks/ConexaoSolidaria.Contracts/
COPY src/BuildingBlocks/ConexaoSolidaria.Mensageria/ConexaoSolidaria.Mensageria.csproj src/BuildingBlocks/ConexaoSolidaria.Mensageria/
COPY src/BuildingBlocks/ConexaoSolidaria.Observabilidade/ConexaoSolidaria.Observabilidade.csproj src/BuildingBlocks/ConexaoSolidaria.Observabilidade/
COPY src/BuildingBlocks/ConexaoSolidaria.Persistencia/ConexaoSolidaria.Persistencia.csproj src/BuildingBlocks/ConexaoSolidaria.Persistencia/
COPY src/BuildingBlocks/ConexaoSolidaria.Web/ConexaoSolidaria.Web.csproj src/BuildingBlocks/ConexaoSolidaria.Web/
COPY src/Campanhas/Campanhas.Domain/Campanhas.Domain.csproj src/Campanhas/Campanhas.Domain/
COPY src/Campanhas/Campanhas.Infrastructure/Campanhas.Infrastructure.csproj src/Campanhas/Campanhas.Infrastructure/
COPY src/Campanhas/Campanhas.Api/Campanhas.Api.csproj src/Campanhas/Campanhas.Api/
RUN dotnet restore src/Campanhas/Campanhas.Api/Campanhas.Api.csproj

COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Campanhas/ src/Campanhas/
RUN dotnet publish src/Campanhas/Campanhas.Api/Campanhas.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Campanhas.Api.dll"]
