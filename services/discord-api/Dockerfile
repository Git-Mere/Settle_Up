FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["services/discord-api/src/DiscordApi.csproj", "services/discord-api/src/"]
COPY ["shared/SettleUp.Observability/SettleUp.Observability.csproj", "shared/SettleUp.Observability/"]
RUN dotnet restore "services/discord-api/src/DiscordApi.csproj"

COPY ["services/discord-api/", "services/discord-api/"]
COPY ["shared/SettleUp.Observability/", "shared/SettleUp.Observability/"]
RUN dotnet publish "services/discord-api/src/DiscordApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:5000
EXPOSE 5000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DiscordApi.dll"]
