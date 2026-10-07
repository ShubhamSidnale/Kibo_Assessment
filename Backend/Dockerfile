FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props InventoryHold.slnx global.json ./
COPY src/ ./src/
RUN dotnet restore InventoryHold.slnx
RUN dotnet test src/InventoryHold.UnitTests/InventoryHold.UnitTests.csproj --no-restore -c Release
RUN dotnet publish src/InventoryHold.WebApi/InventoryHold.WebApi.csproj --no-restore -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "InventoryHold.WebApi.dll"]
