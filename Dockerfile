# 1. Etapa de compilación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar archivos de proyecto y restaurar dependencias
COPY ["src/MiPos.API/MiPos.API.csproj", "src/MiPos.API/"]
COPY ["src/MiPos.Shared/MiPos.Shared.csproj", "src/MiPos.Shared/"]
RUN dotnet restore "src/MiPos.API/MiPos.API.csproj"

# Copiar todo el código y compilar
COPY . .
WORKDIR "/src/src/MiPos.API"
RUN dotnet publish "MiPos.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Etapa de ejecución (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Render asigna dinámicamente el puerto en la variable PORT
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MiPos.API.dll"]