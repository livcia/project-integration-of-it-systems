FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY jira.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish jira.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 6767
ENTRYPOINT ["dotnet", "jira.dll"]
