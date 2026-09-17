# See https://aka.ms/customizecontainer to learn how to customize your debug container 
# and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["GeoSlayer.Core/GeoSlayer.Core.csproj", "GeoSlayer.Core/"]
COPY ["GeoSlayer.Domain/GeoSlayer.Domain.csproj", "GeoSlayer.Domain/"]

# Restore dependencies
RUN dotnet restore "GeoSlayer.Core/GeoSlayer.Core.csproj"

# Copy all source
COPY . .

# Build the project
WORKDIR "/src/GeoSlayer.Core"
RUN dotnet build "GeoSlayer.Core.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR "/src/GeoSlayer.Core"
RUN dotnet publish "GeoSlayer.Core.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GeoSlayer.Core.dll"]
