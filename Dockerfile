FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY Theoria.slnx .
COPY src/Theoria.Shared/Theoria.Shared.csproj src/Theoria.Shared/
COPY src/Theoria.Engine/Theoria.Engine.csproj src/Theoria.Engine/
COPY src/Theoria.Api/Theoria.Api.csproj src/Theoria.Api/
RUN dotnet restore src/Theoria.Api/Theoria.Api.csproj

# Copy everything and publish
COPY src/Theoria.Shared/ src/Theoria.Shared/
COPY src/Theoria.Engine/ src/Theoria.Engine/
COPY src/Theoria.Api/ src/Theoria.Api/
RUN dotnet publish src/Theoria.Api/Theoria.Api.csproj -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Render sets PORT env var
ENV ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "Theoria.Api.dll"]
