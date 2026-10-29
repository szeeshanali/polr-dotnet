# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY src/Polr/Polr.csproj src/Polr/
RUN dotnet restore src/Polr/Polr.csproj
COPY src/ src/
RUN dotnet publish src/Polr/Polr.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Folder for data protection keys (mount a shared volume here when running several instances).
RUN mkdir /keys && chown $APP_UID /keys
ENV Polr__DataProtectionKeysPath=/keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Polr.dll"]
