FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5400

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Copy project files first for better layer caching
COPY ./WishlistApp/WishlistApp.csproj /src/WishlistApp/WishlistApp.csproj
COPY ./user-service/user-service.csproj /src/user-service/user-service.csproj
# Restore dependencies
RUN dotnet restore /src/user-service/user-service.csproj
# Copy all source files
COPY ./WishlistApp /src/WishlistApp
COPY ./user-service /src/user-service
# Build and publish
RUN dotnet publish /src/user-service/user-service.csproj -c Release -o /out

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "user-service.dll"]


