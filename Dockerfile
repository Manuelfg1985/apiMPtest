# 1. Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar todos los archivos .csproj manteniendo la estructura
COPY ["src/MiPos.API/*.csproj", "src/MiPos.API/"]
COPY ["src/MiPos.Shared/*.csproj", "src/MiPos.Shared/"]

# Restaurar dependencias
RUN dotnet restore "src/MiPos.API/MiPos.API.csproj"

# Copiar el resto del código fuente
COPY . .

# Compilar y publicar
WORKDIR "/src/src/MiPos.API"
RUN dotnet publish "MiPos.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Etapa de ejecución (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MiPos.API.dll"]