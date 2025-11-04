# Use the official .NET runtime as a parent image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["PresentationLayer/PresentationLayer.csproj", "PresentationLayer/"]
COPY ["BusinessLayer/BusinessLayer.csproj", "BusinessLayer/"]
COPY ["PersistenceLayer/PersistenceLayer.csproj", "PersistenceLayer/"]
RUN dotnet restore "PresentationLayer/PresentationLayer.csproj"

# Copy all files
COPY . .

# Build the application
RUN dotnet build "PresentationLayer/PresentationLayer.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "PresentationLayer/PresentationLayer.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5002

# Copy published files
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "PresentationLayer.dll"]


