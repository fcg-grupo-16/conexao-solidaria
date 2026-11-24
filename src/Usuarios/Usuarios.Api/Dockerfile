# syntax=docker/dockerfile:1
# Contexto de build: raiz do repositório.
#   docker build -f src/Usuarios/Usuarios.Api/Dockerfile -t usuarios-api .

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada própria: só muda quando um .csproj ou as versões centrais mudam.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/BuildingBlocks/ConexaoSolidaria.Observabilidade/ConexaoSolidaria.Observabilidade.csproj src/BuildingBlocks/ConexaoSolidaria.Observabilidade/
COPY src/BuildingBlocks/ConexaoSolidaria.Web/ConexaoSolidaria.Web.csproj src/BuildingBlocks/ConexaoSolidaria.Web/
COPY src/Usuarios/Usuarios.Domain/Usuarios.Domain.csproj src/Usuarios/Usuarios.Domain/
COPY src/Usuarios/Usuarios.Infrastructure/Usuarios.Infrastructure.csproj src/Usuarios/Usuarios.Infrastructure/
COPY src/Usuarios/Usuarios.Api/Usuarios.Api.csproj src/Usuarios/Usuarios.Api/
RUN dotnet restore src/Usuarios/Usuarios.Api/Usuarios.Api.csproj

COPY src/BuildingBlocks/ConexaoSolidaria.Observabilidade/ src/BuildingBlocks/ConexaoSolidaria.Observabilidade/
COPY src/BuildingBlocks/ConexaoSolidaria.Web/ src/BuildingBlocks/ConexaoSolidaria.Web/
COPY src/Usuarios/ src/Usuarios/
RUN dotnet publish src/Usuarios/Usuarios.Api/Usuarios.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Usuarios.Api.dll"]
