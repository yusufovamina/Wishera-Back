FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

ENV ASPNETCORE_URLS=http://+:5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for better layer caching and dependency resolution
COPY ./user-service/user-service.csproj /src/user-service/user-service.csproj
COPY ./WishlistApp/WishlistApp.csproj /src/WishlistApp/WishlistApp.csproj

# Restore dependencies (this will resolve project references)
RUN dotnet restore /src/WishlistApp/WishlistApp.csproj

# Copy all source files
COPY ./user-service /src/user-service
COPY ./WishlistApp /src/WishlistApp

# Build and publish
RUN dotnet publish /src/WishlistApp/WishlistApp.csproj -c Release -o /out /p:ErrorOnDuplicatePublishOutputFiles=false

FROM base AS final
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "WishlistApp.dll"]
