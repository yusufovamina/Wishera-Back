FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5003

ENV ASPNETCORE_URLS=http://+:5003

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for better layer caching and dependency resolution
# gift-wishlist-service -> WishlistApp -> user-service (dependency chain)
COPY ./user-service/user-service.csproj /src/user-service/user-service.csproj
COPY ./WishlistApp/WishlistApp.csproj /src/WishlistApp/WishlistApp.csproj
COPY ./gift-wishlist-service/gift-wishlist-service.csproj /src/gift-wishlist-service/gift-wishlist-service.csproj

# Restore dependencies (this will resolve all project references)
RUN dotnet restore /src/gift-wishlist-service/gift-wishlist-service.csproj

# Copy all source files
COPY ./user-service /src/user-service
COPY ./WishlistApp /src/WishlistApp
COPY ./gift-wishlist-service /src/gift-wishlist-service

# Build and publish, excluding appsettings files from referenced projects
RUN dotnet publish /src/gift-wishlist-service/gift-wishlist-service.csproj -c Release -o /out /p:ErrorOnDuplicatePublishOutputFiles=false

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "gift-wishlist-service.dll"]


