FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/InsulinAndCoffee.Api/InsulinAndCoffee.Api.csproj src/InsulinAndCoffee.Api/
COPY src/InsulinAndCoffee.Application/InsulinAndCoffee.Application.csproj src/InsulinAndCoffee.Application/
COPY src/InsulinAndCoffee.Domain/InsulinAndCoffee.Domain.csproj src/InsulinAndCoffee.Domain/
COPY src/InsulinAndCoffee.Infrastructure/InsulinAndCoffee.Infrastructure.csproj src/InsulinAndCoffee.Infrastructure/

RUN dotnet restore src/InsulinAndCoffee.Api/InsulinAndCoffee.Api.csproj

COPY src/ src/

RUN dotnet publish src/InsulinAndCoffee.Api/InsulinAndCoffee.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "InsulinAndCoffee.Api.dll"]