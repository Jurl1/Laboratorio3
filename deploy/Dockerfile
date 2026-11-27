# Build context: raíz del repo (ver deploy/docker-compose.yml)

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Lab3.Web/Lab3.Web.csproj src/Lab3.Web/
RUN dotnet restore src/Lab3.Web/Lab3.Web.csproj
COPY src/ src/
RUN dotnet publish src/Lab3.Web/Lab3.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
# Data Protection keys en un volumen (docker-compose.yml): si no, cada rebuild desloguea a todos.
ENV DataProtection__KeysPath=/keys
RUN mkdir -p /keys && chown $APP_UID /keys
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Lab3.Web.dll"]
