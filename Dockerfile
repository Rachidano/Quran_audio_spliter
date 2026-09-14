# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project definitions for efficient layer caching
COPY ["QuranWarsh.sln", "./"]
COPY ["src/Quran.Domain/Quran.Domain.csproj", "src/Quran.Domain/"]
COPY ["src/Quran.Application/Quran.Application.csproj", "src/Quran.Application/"]
COPY ["src/Quran.Infrastructure/Quran.Infrastructure.csproj", "src/Quran.Infrastructure/"]
COPY ["src/Quran.AudioProcessor/Quran.AudioProcessor.csproj", "src/Quran.AudioProcessor/"]
COPY ["src/Quran.Import/Quran.Import.csproj", "src/Quran.Import/"]
COPY ["src/Quran.Web/Quran.Web.csproj", "src/Quran.Web/"]
COPY ["tests/Quran.UnitTests/Quran.UnitTests.csproj", "tests/Quran.UnitTests/"]
COPY ["tests/Quran.IntegrationTests/Quran.IntegrationTests.csproj", "tests/Quran.IntegrationTests/"]

RUN dotnet restore "QuranWarsh.sln"

# Copy source tree and compile
COPY . .
WORKDIR "/src/src/Quran.Web"
RUN dotnet publish "Quran.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Production Runtime with FFmpeg
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install FFmpeg and dependencies for audio slicing, waveforms, and audio streams
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Expose default container port
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Quran.Web.dll"]
