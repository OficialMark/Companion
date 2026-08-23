FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Companion.slnx Directory.Build.props ./
COPY src/Companion.Domain/Companion.Domain.csproj src/Companion.Domain/
COPY src/Companion.Application/Companion.Application.csproj src/Companion.Application/
COPY src/Companion.Infrastructure/Companion.Infrastructure.csproj src/Companion.Infrastructure/
COPY src/Companion.Api/Companion.Api.csproj src/Companion.Api/
RUN dotnet restore src/Companion.Api/Companion.Api.csproj
COPY src/ src/
RUN dotnet publish src/Companion.Api/Companion.Api.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Companion.Api.dll"]
