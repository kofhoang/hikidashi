# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Hikidashi.Core/Hikidashi.Core.csproj src/Hikidashi.Core/
COPY src/Hikidashi.Data/Hikidashi.Data.csproj src/Hikidashi.Data/
COPY src/Hikidashi.Web/Hikidashi.Web.csproj src/Hikidashi.Web/
RUN dotnet restore src/Hikidashi.Web/Hikidashi.Web.csproj

COPY src/ src/
RUN dotnet publish src/Hikidashi.Web/Hikidashi.Web.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Scaleway Serverless Containers route to the port in $PORT (default 8080).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Hikidashi.Web.dll"]
