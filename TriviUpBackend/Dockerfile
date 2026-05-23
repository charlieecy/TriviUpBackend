# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore
COPY TriviUpBackend.csproj ./
RUN dotnet restore TriviUpBackend.csproj

# Copy source
COPY . ./

# Stage 2: Tests & Coverage
FROM build AS test
WORKDIR /src

# Copy test project and run tests with coverage
COPY TriviUpTest/ ./TriviUpTest/
WORKDIR /src/TriviUpTest
RUN dotnet test --collect:"XPlat Code Coverage" -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura -p:CoverletOutput=./coverage/

# Stage 3: Publish
FROM build AS publish
WORKDIR /src
RUN dotnet publish TriviUpBackend.csproj -c Release -o /app/publish --no-restore

# Stage 4: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published files
COPY --from=publish /app/publish .

# Copy coverage report from test stage
COPY --from=test /src/TriviUpTest/coverage ./coverage

EXPOSE 5164
ENV ASPNETCORE_URLS=http://+:5164
ENTRYPOINT ["dotnet", "TriviUpBackend.dll"]
