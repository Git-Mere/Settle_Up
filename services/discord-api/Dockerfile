FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/DiscordApi/DiscordApi.csproj", "src/DiscordApi/"]
RUN dotnet restore "src/DiscordApi/DiscordApi.csproj"

COPY . .
RUN dotnet publish "src/DiscordApi/DiscordApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DiscordApi.dll"]
