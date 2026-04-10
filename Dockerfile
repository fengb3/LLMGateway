FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY LLMGateway.slnx Directory.Build.props .editorconfig ./
COPY src/LLMGateway/LLMGateway.csproj src/LLMGateway/
COPY src/LLMGateway.Data/LLMGateway.Data.csproj src/LLMGateway.Data/
COPY src/LLMGateway.Models/LLMGateway.Models.csproj src/LLMGateway.Models/
RUN dotnet restore src/LLMGateway/LLMGateway.csproj

COPY . .
RUN dotnet publish src/LLMGateway/LLMGateway.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

# Data volume for SQLite database and logs
VOLUME ["/data"]

ENV ASPNETCORE_URLS=http://+:8080
ENV Gateway__DatabasePath=/data/gateway.db

ENTRYPOINT ["dotnet", "LLMGateway.dll"]
