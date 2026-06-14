FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY jira.csproj ./
RUN dotnet restore

# Copy all source files and publish
COPY . ./
RUN dotnet publish jira.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published output (includes wwwroot/_framework with blazor.web.js)
COPY --from=build /app/publish .

EXPOSE 6767
ENTRYPOINT ["dotnet", "jira.dll"]
