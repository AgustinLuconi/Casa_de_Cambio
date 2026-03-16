FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore
COPY CasaCambio.Server/CasaCambio.Server.csproj CasaCambio.Server/
COPY CasaCambio.Shared/CasaCambio.Shared.csproj CasaCambio.Shared/
RUN dotnet restore CasaCambio.Server/CasaCambio.Server.csproj

# Copy source and publish
COPY . .
RUN dotnet publish CasaCambio.Server/CasaCambio.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "CasaCambio.Server.dll"]
