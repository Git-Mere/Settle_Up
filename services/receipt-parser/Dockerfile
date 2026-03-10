FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5040

ENV ASPNETCORE_URLS=http://+:5040

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src
COPY ["services/receipt-parser/receipt-parser.csproj", "services/receipt-parser/"]
COPY ["shared/SettleUp.Observability/SettleUp.Observability.csproj", "shared/SettleUp.Observability/"]
RUN dotnet restore "services/receipt-parser/receipt-parser.csproj"
COPY ["services/receipt-parser/", "services/receipt-parser/"]
COPY ["shared/SettleUp.Observability/", "shared/SettleUp.Observability/"]
WORKDIR "/src/services/receipt-parser"
RUN dotnet build "receipt-parser.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "receipt-parser.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "receipt-parser.dll"]
