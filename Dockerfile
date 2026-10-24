# API InfoPresupuestal (.NET 9). Render: runtime Docker, raíz del repositorio como contexto.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/InfoPresupuestal.Api/InfoPresupuestal.Api.csproj src/InfoPresupuestal.Api/
RUN dotnet restore src/InfoPresupuestal.Api/InfoPresupuestal.Api.csproj

COPY src/ src/
RUN dotnet publish src/InfoPresupuestal.Api/InfoPresupuestal.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "InfoPresupuestal.Api.dll"]
