# Etapa 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar archivos de proyecto para restaurar dependencias
COPY TriviUpBackend.csproj ./
RUN dotnet restore TriviUpBackend.csproj

# Copiar todo el código fuente
COPY . ./

# Publicar la aplicación
RUN dotnet publish TriviUpBackend.csproj -c Release -o /app/publish --no-restore

# Etapa 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Los imágenes .NET ya corren como usuario no-root por defecto
# Copiar los archivos publicados desde la etapa de build
COPY --from=build /app/publish .

# Exponer el puerto
EXPOSE 5164

# Variable de entorno para el puerto
ENV ASPNETCORE_URLS=http://+:5164
ENV ASPNETCORE_ENVIRONMENT=Development

# Comando de entrada
ENTRYPOINT ["dotnet", "TriviUpBackend.dll"]