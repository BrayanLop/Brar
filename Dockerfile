# Construir desde la raíz del repositorio:
#   docker build -t brar-api .
#
# La imagen resultante es la que se despliega (docker run / docker compose / registro de contenedores).

# Etapa 1: restaurar y compilar
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Solo los .csproj primero: la restauración de paquetes queda en una capa cacheable.
COPY ["global.json", "./"]
COPY ["Brar/Brar.csproj", "Brar/"]
COPY ["Brar.Application/Brar.Application.csproj", "Brar.Application/"]
COPY ["Brar.Domain/Brar.Domain.csproj", "Brar.Domain/"]
COPY ["Brar.Infrastructure/Brar.Infrastructure.csproj", "Brar.Infrastructure/"]
RUN dotnet restore "Brar/Brar.csproj"

COPY . .

# Etapa 2: publicar
FROM build AS publish
RUN dotnet publish "Brar/Brar.csproj" -c Release -o /app/publish /p:UseAppHost=false --no-restore

# Etapa 3: runtime (solo el runtime de ASP.NET, sin SDK)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Escucha en 8080 dentro del contenedor; se puede sobrescribir con ASPNETCORE_URLS.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Usuario sin privilegios que trae la imagen oficial de .NET
USER $APP_UID

ENTRYPOINT ["dotnet", "Brar.dll"]
