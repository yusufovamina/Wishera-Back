FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5300

ENV ASPNETCORE_URLS=http://+:5300

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Copy all source files (needed for project references to resolve during restore)
COPY ./WishlistApp /src/WishlistApp
COPY ./gift-wishlist-service /src/gift-wishlist-service
# Restore dependencies
RUN dotnet restore /src/gift-wishlist-service/gift-wishlist-service.csproj
# Build and publish
RUN dotnet publish /src/gift-wishlist-service/gift-wishlist-service.csproj -c Release -o /out

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "gift-wishlist-service.dll"]


