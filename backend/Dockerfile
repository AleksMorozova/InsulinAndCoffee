FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY InsulinAndCoffee.sln ./
COPY backend/src/InsulinAndCoffee.Api/InsulinAndCoffee.Api.csproj backend/src/InsulinAndCoffee.Api/
COPY backend/src/InsulinAndCoffee.Application/InsulinAndCoffee.Application.csproj backend/src/InsulinAndCoffee.Application/
COPY backend/src/InsulinAndCoffee.Domain/InsulinAndCoffee.Domain.csproj backend/src/InsulinAndCoffee.Domain/
COPY backend/src/InsulinAndCoffee.Infrastructure/InsulinAndCoffee.Infrastructure.csproj backend/src/InsulinAndCoffee.Infrastructure/
COPY backend/tests/InsulinAndCoffee.Application.Tests/InsulinAndCoffee.Application.Tests.csproj backend/tests/InsulinAndCoffee.Application.Tests/

RUN dotnet restore backend/src/InsulinAndCoffee.Api/InsulinAndCoffee.Api.csproj

COPY backend/src backend/src

RUN dotnet publish backend/src/InsulinAndCoffee.Api/InsulinAndCoffee.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

ENTRYPOINT ["sh", "-c", "dotnet InsulinAndCoffee.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
