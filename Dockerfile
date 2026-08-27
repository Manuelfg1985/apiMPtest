# 1. Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar todo el código fuente al contenedor
COPY . .

# Restaurar dependencias usando la solución o el proyecto
RUN dotnet restore "src/MiPos.API/MiPos.API.csproj"

# Publicar el proyecto de la API
RUN dotnet publish "src/MiPos.API/MiPos.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Etapa de ejecución (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MiPos.API.dll"]