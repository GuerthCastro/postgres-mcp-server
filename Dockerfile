FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

COPY ["Mcp.PostgreSQL/Mcp.PostgreSQL.csproj", "Mcp.PostgreSQL/"]
RUN dotnet restore "Mcp.PostgreSQL/Mcp.PostgreSQL.csproj"

COPY . .
WORKDIR "/src/Mcp.PostgreSQL"
RUN dotnet publish "Mcp.PostgreSQL.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=80

COPY --from=build /app/publish .

EXPOSE 80

ENTRYPOINT ["dotnet", "Mcp.PostgreSQL.dll"]