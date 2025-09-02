using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Shared;
using Shared.Packet.Packets;

namespace Server.Discord;

public class ModernDiscordBot
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _services;
    private readonly LocalizationManager _localization;
    private readonly Logger _logger;

    public ModernDiscordBot()
    {
        // Configuration du client Discord
        var config = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages
        };

        _client = new DiscordSocketClient(config);
        _interactionService = new InteractionService(_client);
        _localization = new LocalizationManager();
        _logger = new Logger("DiscordBot");

        // Configuration des services
        var services = new ServiceCollection()
            .AddSingleton(_client)
            .AddSingleton(_interactionService)
            .AddSingleton(_localization)
            .AddSingleton<ServerService>()
            .BuildServiceProvider();

        _services = services;

        // Configuration des événements
        _client.Log += LogAsync;
        _client.Ready += ReadyAsync;
        _client.SlashCommandExecuted += SlashCommandHandler;
        _client.ButtonExecuted += ButtonHandler;
        _interactionService.Log += LogAsync;
    }

    public async Task StartAsync(string token, global::Server.Server server)
    {
        // Configuration du service serveur
        var serverService = _services.GetRequiredService<ServerService>();
        serverService.SetServer(server);

        // Connexion à Discord
        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        _logger.Info("Modern Discord bot started successfully");
    }

    public async Task StopAsync()
    {
        await _client.StopAsync();
        await _client.LogoutAsync();
    }

    private Task LogAsync(LogMessage log)
    {
        var severity = log.Severity switch
        {
            LogSeverity.Critical => "CRITICAL",
            LogSeverity.Error => "ERROR", 
            LogSeverity.Warning => "WARN",
            LogSeverity.Info => "INFO",
            LogSeverity.Verbose => "VERBOSE",
            LogSeverity.Debug => "DEBUG",
            _ => "INFO"
        };

        _logger.Info($"[{severity}] {log.Source}: {log.Message}");
        
        if (log.Exception != null)
            _logger.Error(log.Exception);

        return Task.CompletedTask;
    }

    private async Task ReadyAsync()
    {
        _logger.Info($"Bot is ready! Logged in as {_client.CurrentUser.Username}");

        // Chargement des modules de commandes slash
        await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

        // Enregistrement des commandes globales (pour la production)
        // await _interactionService.RegisterCommandsGloballyAsync();
        
        // Enregistrement des commandes de test (pour le développement)
        if (Settings.Instance.Discord.TestGuildId != null && ulong.TryParse(Settings.Instance.Discord.TestGuildId, out var guildId))
        {
            await _interactionService.RegisterCommandsToGuildAsync(guildId);
            _logger.Info($"Slash commands registered to test guild {guildId}");
        }
    }

    private async Task SlashCommandHandler(SocketSlashCommand command)
    {
        var ctx = new SocketInteractionContext(_client, command);
        await _interactionService.ExecuteCommandAsync(ctx, _services);
    }

    private async Task ButtonHandler(SocketMessageComponent component)
    {
        var ctx = new SocketInteractionContext(_client, component);
        await _interactionService.ExecuteCommandAsync(ctx, _services);
    }

    public T? GetService<T>() where T : class
    {
        return _services.GetService<T>();
    }
}

// Module de base pour les commandes slash
public class ModernDiscordModule : InteractionModuleBase<SocketInteractionContext>
{
    protected readonly ServerService ServerService;
    protected readonly LocalizationManager Localization;

    public ModernDiscordModule(ServerService serverService, LocalizationManager localization)
    {
        ServerService = serverService;
        Localization = localization;
    }

    /// <summary>
    /// Obtient la meilleure locale à utiliser pour ce contexte
    /// </summary>
    protected string GetBestLocale()
    {
        return Context.GetBestLocale(Localization.UserPreferences);
    }

    /// <summary>
    /// Répond avec un message localisé
    /// </summary>
    protected async Task RespondLocalizedAsync(string responsePath, string? locale = null, bool ephemeral = false, params object[] args)
    {
        locale ??= GetBestLocale();
        var message = Localization.GetResponse(responsePath, locale, args);
        await RespondAsync(message, ephemeral: ephemeral);
    }

    /// <summary>
    /// Répond avec un embed localisé
    /// </summary>
    protected async Task RespondWithLocalizedEmbedAsync(string titlePath, string? descriptionPath = null, string colorType = "info", string? locale = null, bool ephemeral = false, params object[] args)
    {
        locale ??= GetBestLocale();
        
        var embed = new EmbedBuilder()
            .WithTitle(Localization.GetResponse(titlePath, locale, args))
            .WithColor(Localization.GetEmbedColor(colorType, locale))
            .WithFooter(Localization.GetEmbedFooter(locale))
            .WithTimestamp(DateTimeOffset.Now);
            
        if (!string.IsNullOrEmpty(descriptionPath))
        {
            embed.WithDescription(Localization.GetResponse(descriptionPath, locale, args));
        }
        
        await RespondAsync(embed: embed.Build(), ephemeral: ephemeral);
    }

    /// <summary>
    /// Répond avec un message d'erreur localisé
    /// </summary>
    protected async Task RespondErrorAsync(string errorPath = "errors.general", string? locale = null, params object[] args)
    {
        locale ??= GetBestLocale();
        var message = Localization.GetResponse(errorPath, locale, args);
        
        var embed = new EmbedBuilder()
            .WithDescription(message)
            .WithColor(Localization.GetEmbedColor("error", locale))
            .WithFooter(Localization.GetEmbedFooter(locale))
            .WithTimestamp(DateTimeOffset.Now)
            .Build();
            
        await RespondAsync(embed: embed, ephemeral: true);
    }

    /// <summary>
    /// Répond avec un message de succès localisé
    /// </summary>
    protected async Task RespondSuccessAsync(string successPath, string? locale = null, params object[] args)
    {
        locale ??= GetBestLocale();
        var message = Localization.GetResponse(successPath, locale, args);
        
        var embed = new EmbedBuilder()
            .WithDescription(message)
            .WithColor(Localization.GetEmbedColor("success", locale))
            .WithFooter(Localization.GetEmbedFooter(locale))
            .WithTimestamp(DateTimeOffset.Now)
            .Build();
            
        await RespondAsync(embed: embed);
    }

    /// <summary>
    /// Répond avec un message d'avertissement localisé
    /// </summary>
    protected async Task RespondWarningAsync(string warningPath, string? locale = null, params object[] args)
    {
        locale ??= GetBestLocale();
        var message = Localization.GetResponse(warningPath, locale, args);
        
        var embed = new EmbedBuilder()
            .WithDescription(message)
            .WithColor(Localization.GetEmbedColor("warning", locale))
            .WithFooter(Localization.GetEmbedFooter(locale))
            .WithTimestamp(DateTimeOffset.Now)
            .Build();
            
        await RespondAsync(embed: embed);
    }
}

// Commandes de base du serveur
public class ServerCommands : ModernDiscordModule
{
    public ServerCommands(ServerService serverService, LocalizationManager localization) : base(serverService, localization) { }

    private string GetServerUptime()
    {
        var startTime = System.Diagnostics.Process.GetCurrentProcess().StartTime;
        var uptime = DateTime.Now - startTime;
        return $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";
    }

    [SlashCommand("status", "Show server status")]
    public async Task ServerStatusAsync()
    {
        try
        {
            if (ServerService.MainServer == null)
            {
                await RespondErrorAsync("errors.server_unavailable");
                return;
            }

            var locale = GetBestLocale();
            var embed = new EmbedBuilder()
                .WithTitle(Localization.GetResponse("server.status_title", locale))
                .WithColor(Localization.GetEmbedColor("success", locale))
                .WithFooter(Localization.GetEmbedFooter(locale))
                .WithTimestamp(DateTimeOffset.Now);

            var statusInfo = Localization.GetResponse("server.status_info", locale,
                Settings.Instance.Server.Address,
                Settings.Instance.Server.Port,
                ServerService.MainServer.ClientsConnected.Count(),
                GetServerUptime());

            embed.WithDescription(statusInfo);

            await RespondAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("players", "List connected players")]
    public async Task PlayersAsync()
    {
        try
        {
            if (ServerService.MainServer == null)
            {
                await RespondErrorAsync("errors.server_unavailable");
                return;
            }

            var locale = GetBestLocale();
            var clients = ServerService.MainServer.ClientsConnected.ToList();
            
            if (!clients.Any())
            {
                await RespondLocalizedAsync("server.no_players", locale);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle(Localization.GetResponse("server.players_title", locale))
                .WithColor(Localization.GetEmbedColor("info", locale))
                .WithFooter(Localization.GetEmbedFooter(locale))
                .WithTimestamp(DateTimeOffset.Now);

            var playerList = string.Join("\n", clients.Select((c, i) => 
            {
                var lastGame = c.Metadata.TryGetValue("lastGamePacket", out var gamePacket) ? (GamePacket?)gamePacket : null;
                var stage = lastGame?.Stage ?? "Unknown";
                var scenario = lastGame?.ScenarioNum ?? 0;
                return Localization.GetResponse("server.player_info", locale, c.Name, stage, scenario);
            }));

            embed.WithDescription(playerList);

            await RespondAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }
}

// Commandes d'administration
public class AdminCommands : ModernDiscordModule
{
    public AdminCommands(ServerService serverService, LocalizationManager localization) : base(serverService, localization) { }

    [SlashCommand("ban", "Ban a player from the server")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task BanAsync([Summary("player", "Player name to ban")] string playerName)
    {
        try
        {
            if (ServerService.MainServer == null)
            {
                await RespondErrorAsync("errors.server_unavailable");
                return;
            }

            var client = ServerService.MainServer.ClientsConnected.FirstOrDefault(c => 
                c.Name?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true);

            if (client == null)
            {
                await RespondErrorAsync("admin.player_not_found", args: playerName);
                return;
            }

            // Use the same logic as the console command for consistency
            // Auto-enable ban system
            if (!Settings.Instance.BanList.Enabled)
            {
                Settings.Instance.BanList.Enabled = true;
            }

            // Mark client as banned and add to ban lists (IP + Profile)
            client.Banned = true;
            Settings.Instance.BanList.Players.Add(client.Id);
            Settings.Instance.BanList.PlayerNames[client.Id] = client.Name;
            
            // Add IP to ban list if it's IPv4
            if (client.Socket?.RemoteEndPoint is System.Net.IPEndPoint ipEndPoint)
            {
                Settings.Instance.BanList.IpAddresses.Add(ipEndPoint.Address.ToString());
            }
            
            // Crash the client (same as console command)
            BanLists.Crash(client);
            
            // Save settings
            Settings.SaveSettings(true);

            await RespondSuccessAsync("ban.player_banned", args: playerName);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("unban", "Unban a player or IP from the server")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task UnbanAsync(
        [Summary("player", "Player name to unban")] string? player = null,
        [Summary("player_id", "Player ID (GUID) to unban")] string? playerId = null,
        [Summary("ip", "IP address to unban")] string? ip = null)
    {
        try
        {
            // Validate parameter combinations
            if (player == null && playerId == null && ip == null)
            {
                await RespondErrorAsync("ban.no_params_error");
                return;
            }

            if (player != null && playerId != null)
            {
                await RespondErrorAsync("ban.multiple_params_error");
                return;
            }

            bool removedProfile = false;
            bool removedIP = false;
            List<string> unbannedItems = new List<string>();

            // Handle player by name
            if (!string.IsNullOrWhiteSpace(player))
            {
                // Try to find by player name in ban list
                var foundProfileByName = Settings.Instance.BanList.PlayerNames
                    .Where(kvp => kvp.Value.Equals(player, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
                
                if (foundProfileByName.Key != Guid.Empty)
                {
                    removedProfile = Settings.Instance.BanList.Players.Remove(foundProfileByName.Key);
                    Settings.Instance.BanList.PlayerNames.Remove(foundProfileByName.Key);
                    if (removedProfile)
                    {
                        unbannedItems.Add($"Player **{foundProfileByName.Value}**");
                    }
                }
                else
                {
                    await RespondErrorAsync("ban.player_not_in_banlist", args: player);
                    return;
                }
            }

            // Handle player by ID
            if (!string.IsNullOrWhiteSpace(playerId))
            {
                if (Guid.TryParse(playerId, out Guid guid))
                {
                    if (Settings.Instance.BanList.Players.Contains(guid))
                    {
                        var playerName = Settings.Instance.BanList.PlayerNames.TryGetValue(guid, out var name) ? name : "Unknown";
                        removedProfile = Settings.Instance.BanList.Players.Remove(guid);
                        Settings.Instance.BanList.PlayerNames.Remove(guid);
                        if (removedProfile)
                        {
                            unbannedItems.Add($"Player **{playerName}** (`{guid}`)");
                        }
                    }
                    else
                    {
                        await RespondErrorAsync("ban.playerid_not_in_banlist", args: playerId);
                        return;
                    }
                }
                else
                {
                    await RespondErrorAsync("ban.invalid_playerid_format", args: playerId);
                    return;
                }
            }

            // Handle IP address
            if (!string.IsNullOrWhiteSpace(ip))
            {
                if (System.Net.IPAddress.TryParse(ip, out var ipAddress))
                {
                    if (Settings.Instance.BanList.IpAddresses.Contains(ip))
                    {
                        removedIP = Settings.Instance.BanList.IpAddresses.Remove(ip);
                        if (removedIP)
                        {
                            unbannedItems.Add($"IP **{ip}**");
                        }
                    }
                    else
                    {
                        await RespondErrorAsync("ban.ip_not_in_banlist", args: ip);
                        return;
                    }
                }
                else
                {
                    await RespondErrorAsync("ban.invalid_ip_format", args: ip);
                    return;
                }
            }

            if (unbannedItems.Count > 0)
            {
                Settings.SaveSettings(true);
                await RespondSuccessAsync("ban.unbanned", args: string.Join(", ", unbannedItems));
            }
            else
            {
                await RespondErrorAsync("ban.nothing_unbanned");
            }
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("banlist", "Show all banned players, IPs, stages and gamemodes")]
    [RequireUserPermission(GuildPermission.BanMembers)]
    public async Task BanListAsync()
    {
        try
        {
            var locale = GetBestLocale();
            var embed = new EmbedBuilder()
                .WithTitle(Localization.GetResponse("ban.title", locale))
                .WithColor(0xff6b6b)
                .WithTimestamp(DateTimeOffset.Now);

            var statusText = Settings.Instance.BanList.Enabled 
                ? Localization.GetResponse("ban.enabled", locale)
                : Localization.GetResponse("ban.disabled", locale);
            var description = Localization.GetResponse("ban.system_status", locale, statusText) + "\n\n";

            // Banned Profile IDs
            if (Settings.Instance.BanList.Players.Any())
            {
                description += Localization.GetResponse("ban.banned_players", locale) + "\n";
                foreach (var profileId in Settings.Instance.BanList.Players)
                {
                    var playerName = Settings.Instance.BanList.PlayerNames.TryGetValue(profileId, out var name) ? name : "Unknown";
                    description += $"• **{playerName}** (`{profileId}`)\n";
                }
                description += "\n";
            }

            // Banned IP Addresses
            if (Settings.Instance.BanList.IpAddresses.Any())
            {
                description += Localization.GetResponse("ban.banned_ips", locale) + "\n";
                foreach (var ip in Settings.Instance.BanList.IpAddresses)
                {
                    description += $"• `{ip}`\n";
                }
                description += "\n";
            }

            // Banned Stages
            if (Settings.Instance.BanList.Stages.Any())
            {
                description += Localization.GetResponse("ban.banned_stages", locale) + "\n";
                foreach (var stage in Settings.Instance.BanList.Stages)
                {
                    description += $"• `{stage}`\n";
                }
                description += "\n";
            }

            // Banned Game Modes
            if (Settings.Instance.BanList.GameModes.Any())
            {
                description += Localization.GetResponse("ban.banned_gamemodes", locale) + "\n";
                foreach (var gameMode in Settings.Instance.BanList.GameModes)
                {
                    description += $"• `{(GameMode)gameMode}`\n";
                }
                description += "\n";
            }

            var initialDescription = Localization.GetResponse("ban.system_status", locale, statusText) + "\n\n";
            if (description == initialDescription)
            {
                description += Localization.GetResponse("ban.no_bans", locale);
            }

            embed.WithDescription(description);
            await RespondAsync(embed: embed.Build());
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("kick", "Kick a player from the server")]
    [RequireUserPermission(GuildPermission.KickMembers)]
    public async Task KickAsync([Summary("player", "Player name to kick")] string playerName)
    {
        try
        {
            if (ServerService.MainServer == null)
            {
                await RespondErrorAsync("errors.server_unavailable");
                return;
            }

            var client = ServerService.MainServer.ClientsConnected.FirstOrDefault(c => 
                c.Name?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true);

            if (client == null)
            {
                await RespondErrorAsync("admin.player_not_found", args: playerName);
                return;
            }

            client.Socket?.Close();
            await RespondSuccessAsync("admin.player_kicked", args: playerName);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("crash", "Crash a player's game")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task CrashAsync([Summary("player", "Player name to crash")] string playerName)
    {
        try
        {
            if (ServerService.MainServer == null)
            {
                await RespondErrorAsync("errors.server_unavailable");
                return;
            }

            var client = ServerService.MainServer.ClientsConnected.FirstOrDefault(c => 
                c.Name?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true);

            if (client == null)
            {
                await RespondErrorAsync("admin.player_not_found", args: playerName);
                return;
            }

            BanLists.Crash(client);
            await RespondSuccessAsync("ban.player_crashed", args: playerName);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("rejoin", "Force a player to rejoin")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task RejoinAsync([Summary("player", "Player name to rejoin")] string playerName)
    {
        try
        {
            if (ServerService.MainServer == null)
            {
                await RespondErrorAsync("errors.server_unavailable");
                return;
            }

            var client = ServerService.MainServer.ClientsConnected.FirstOrDefault(c => 
                c.Name?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true);

            if (client == null)
            {
                await RespondErrorAsync("admin.player_not_found", args: playerName);
                return;
            }

            client.Dispose();
            await RespondSuccessAsync("ban.player_rejoined", args: playerName);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("maxplayers", "Set maximum players on server")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task MaxPlayersAsync([Summary("count", "Maximum number of players")] int count)
    {
        try
        {
            if (count < 1 || count > ushort.MaxValue)
            {
                await RespondErrorAsync("ban.invalid_maxplayers", args: new object[] { count, ushort.MaxValue });
                return;
            }

            Settings.Instance.Server.MaxPlayers = (ushort)count;
            Settings.SaveSettings();
            
            // Disconnect all players to apply changes
            if (ServerService.MainServer != null)
            {
                foreach (var client in ServerService.MainServer.Clients.ToArray())
                {
                    client.Dispose();
                }
            }

            await RespondSuccessAsync("ban.maxplayers_set", args: count);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("reload", "Reload server settings")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ReloadSettingsAsync()
    {
        try
        {
            Settings.LoadSettings();
            await RespondSuccessAsync("settings.reloaded");
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("settings.reload_error", args: ex.Message);
        }
    }
}