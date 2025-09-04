# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

### Development and Building
- **Build project**: `dotnet build`
- **Run server**: `dotnet run --project Server/Server.csproj`
- **Run in release mode**: `dotnet run --project Server/Server.csproj -c Release`
- **Build release**: `dotnet build -c Release`
- **Build specific project**: `dotnet build Server/Server.csproj`

### Docker
- **Build for all architectures**: `./docker-build.sh all`
- **Build for specific architecture**: `./docker-build.sh {x64|arm|arm64|win64}`
- **Run with docker-compose**: `docker-compose up -d`
- **View logs**: `docker-compose logs --tail=20 --follow`
- **Stop server**: `docker-compose stop`

### Testing
- **Run TestClient**: `dotnet run --project TestClient/TestClient.csproj <server-ip>`
- **Example**: `dotnet run --project TestClient/TestClient.csproj 127.0.0.1`

### Package Management
- **Restore packages**: `dotnet restore`
- **Clean build artifacts**: `dotnet clean`
- **NuGet troubleshooting**: If package restoration fails:
  ```bash
  dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
  dotnet nuget locals all --clear
  ```

## Architecture Overview

### Project Structure
The solution consists of three main projects:
- **Server**: Core server application that handles multiplayer connections
- **Shared**: Common code shared between server and clients (packet definitions, constants, utilities)
- **TestClient**: Simple test client for debugging server functionality

### Core Components

#### Server (Server/)
- **Program.cs**: Main entry point using top-level statements with packet handling logic, console commands, and shine synchronization
- **Server.cs**: TCP server implementation that manages client connections and packet broadcasting
- **Client.cs**: Represents individual connected clients with metadata and packet sending capabilities
- **Settings.cs**: Configuration management with JSON serialization
- **CommandHandler.cs**: Console command processing system for text-based commands
- **BanLists.cs**: Player and stage banning functionality
- **DiscordBot.cs**: Legacy Discord integration using DSharpPlus
- **Discord/**: Modern Discord bot system using Discord.Net
  - **ModernDiscordBot.cs**: Main Discord.Net bot implementation with slash commands
  - **ServerService.cs**: Service for accessing server data from Discord modules
  - **LocalizationManager.cs**: Multi-language support (English/French)
  - **[*]Commands.cs**: Modular slash command implementations
- **JsonApi/**: REST API implementation for external integrations
  - **JsonApi.cs**: HTTP API server for external tools and integrations
  - **ApiRequest.cs**: Request handling and routing
  - **BlockClients.cs**: API client blocking/filtering
  - **Context.cs**: API context and state management

#### Shared (Shared/)
- **Packet/**: Complete packet system for client-server communication
  - **PacketType.cs**: Enum defining all packet types (Init, Player, Game, Tag, etc.)
  - **Packets/**: Individual packet implementations (PlayerPacket, GamePacket, TagPacket, etc.)
  - **PacketUtils.cs**: Serialization utilities
- **Constants.cs**: Shared constants and packet mappings
- **Logger.cs**: Logging utility
- **Stages.cs**: Stage/level definitions and mappings

### Key Systems

#### Packet System
- Strongly typed packet system with automatic serialization
- Header + payload structure with size validation
- Broadcast system for distributing packets to connected clients
- Packet handlers can modify or prevent broadcasting

#### Client Management
- Automatic reconnection handling with client state preservation
- Metadata system for storing per-client game state
- Ban system supporting IPv4 addresses, profile IDs, stages, and game modes
- Player "flipping" system for visual effects

#### Game State Synchronization
- **Shine/Moon sync**: Tracks collected moons across all players
- **Costume sync**: Synchronizes player appearance
- **Stage sync**: Manages player locations and scenario states
- **Tag game modes**: Hide and seek, sardines with timer support

#### Settings System
- JSON configuration with automatic saving/loading
- Runtime settings reload with `loadsettings` command
- Structured settings tables for different subsystems
- Discord bot configuration supports both legacy (DSharpPlus) and modern (Discord.Net) systems

#### Discord Integration
- **Dual bot system**: Legacy DSharpPlus and modern Discord.Net support
- **Configuration**: `UseModernBot` and `UseLegacyBot` flags in settings.json
- **Modern bot features**: 
  - Slash commands with Discord permissions integration
  - Modular command architecture
  - Multi-language support (English/French)
  - Rich embeds and interactive responses
- **Complete command coverage**: All console commands available as Discord slash commands

## Important Notes

### Server Console Commands
The server provides extensive console commands for administration:
- `help`: List all available commands
- `list`: Show connected players
- `ban/unban`: Player and gamemode management
- `send/sendall`: Force player stage transitions
- `flip`: Player visual effects management
- `shine`: Moon synchronization controls
- `tag`: Tag game mode management
- `loadsettings`: Reload configuration without restart

### Configuration
- Settings auto-generate in `settings.json` on first run
- Most settings can be reloaded without restart via `loadsettings`
- Server address/port changes require full restart
- **Discord bot**: Set `UseModernBot: true` for Discord.Net slash commands, `UseLegacyBot: true` for DSharpPlus text commands
- **NuGet packages**: Package restoration handled automatically with dotnet restore
- **JSON API**: HTTP REST API available for external integrations and monitoring
- **Localization**: Multi-language Discord bot support (English/French) with extensible localization system

### Development Workflow
- **Framework**: Project targets .NET 8.0 (upgraded from .NET 6.0)
- **Dependencies**: 
  - DSharpPlus 4.3.0-nightly (legacy Discord bot)
  - Discord.Net 3.15.3 (modern Discord bot with slash commands)
  - Microsoft.Extensions.DependencyInjection 8.0.0
  - Newtonsoft.Json 13.0.3
- **Server runs** on port 1027 by default
- **TestClient**: Requires server IP argument for connection
- **Docker support** available for cross-platform deployment
- **Discord bot integration** with comprehensive slash command coverage
- **Top-level statements**: Program.cs uses modern C# top-level statements with global variables exposed via ServerService
- **JSON API**: REST endpoints for external monitoring and administration

## Project Information

### Repository
- **GitHub**: https://github.com/LeGeRyChEeSe/SmoOnlineServer  
- **Upstream**: Based on https://github.com/Sanae6/SmoOnlineServer
- **Original Project**: Based on the Super Mario Odyssey Online Server
- **Main Branch**: `master`
- **Maximum Players**: Up to 8 concurrent players supported
- **Default Port**: 1027 (configurable in settings.json)