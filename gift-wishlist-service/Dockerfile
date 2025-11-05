FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5300

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Copy all source files in dependency order (user-service → WishlistApp → gift-wishlist-service)
COPY ./user-service /src/user-service
COPY ./WishlistApp /src/WishlistApp
COPY ./gift-wishlist-service /src/gift-wishlist-service
# Restore and build dependencies in order to resolve project references
RUN dotnet restore /src/user-service/user-service.csproj && \
    dotnet build /src/user-service/user-service.csproj -c Release --no-restore && \
    dotnet restore /src/WishlistApp/WishlistApp.csproj && \
    dotnet build /src/WishlistApp/WishlistApp.csproj -c Release --no-restore && \
    dotnet restore /src/gift-wishlist-service/gift-wishlist-service.csproj
# Build and publish, excluding appsettings files from referenced project
RUN dotnet publish /src/gift-wishlist-service/gift-wishlist-service.csproj -c Release -o /out /p:ErrorOnDuplicatePublishOutputFiles=false

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "gift-wishlist-service.dll"]


