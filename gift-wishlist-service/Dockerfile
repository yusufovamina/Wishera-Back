FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5300

ENV ASPNETCORE_URLS=http://+:5300

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Copy csproj files and restore with solution context for project references
COPY ./WishlistApp/WishlistApp.csproj /src/WishlistApp/WishlistApp.csproj
COPY ./gift-wishlist-service/gift-wishlist-service.csproj /src/gift-wishlist-service.csproj
RUN dotnet restore /src/gift-wishlist-service.csproj
# Copy full sources
COPY . /src
RUN dotnet publish /src/gift-wishlist-service.csproj -c Release -o /out

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "gift-wishlist-service.dll"]


