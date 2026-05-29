FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY . .
RUN dotnet restore "BudgetManagementBotSystem.csproj"

RUN dotnet publish "BudgetManagementBotSystem.csproj" -c Release -o /app/publish /p:PublishTrimmed=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production

VOLUME ["/app/data/evidences"]

ENTRYPOINT ["dotnet", "BudgetManagementBotSystem.dll"]
