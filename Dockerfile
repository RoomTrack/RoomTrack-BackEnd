# One Dockerfile for every .NET project of the solution (services, worker and gateway). The project is chosen with
# build arguments, e.g.:
#   docker build --build-arg PROJECT=src/Services/Bookings/RoomTrack.Bookings.API \
#                --build-arg ASSEMBLY=RoomTrack.Bookings.API -t roomtrack-bookings .
# docker-compose.yml builds every image this way.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG PROJECT
ARG ASSEMBLY

WORKDIR /src

# Copy the build settings and the project files first to take advantage of Docker's layer cache
COPY Directory.Build.props Directory.Packages.props ./
COPY src/BuildingBlocks/RoomTrack.Contracts/RoomTrack.Contracts.csproj src/BuildingBlocks/RoomTrack.Contracts/
COPY src/BuildingBlocks/RoomTrack.BuildingBlocks/RoomTrack.BuildingBlocks.csproj src/BuildingBlocks/RoomTrack.BuildingBlocks/
COPY ${PROJECT}/${ASSEMBLY}.csproj ${PROJECT}/

# Restore dependencies
RUN dotnet restore ${PROJECT}/${ASSEMBLY}.csproj

# Copy the source code (see .dockerignore: no bin/obj, .git, tests or secrets)
COPY src/ src/

# Build and publish
RUN dotnet publish ${PROJECT}/${ASSEMBLY}.csproj \
    -c Release \
    -o /app/publish \
    --no-restore


FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
ARG ASSEMBLY
ENV APP_ASSEMBLY=${ASSEMBLY}.dll

WORKDIR /app

COPY --from=build /app/publish .

# Every container listens on 8080 inside the compose network; only the gateway is published to the host.
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

# Run as the non-root user shipped with the official .NET images (UID 1654)
USER $APP_UID

ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_ASSEMBLY\""]
