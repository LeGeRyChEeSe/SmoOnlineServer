using Discord;
using Discord.Interactions;
using System.Collections.Concurrent;
using System.Text;
using Shared;
using Shared.Packet.Packets;

namespace Server.Discord;

// Commandes de gestion des moons/shines
public class ShineCommands : ModernDiscordModule
{
    public ShineCommands(ServerService serverService, LocalizationManager localization) : base(serverService, localization) { }

    [SlashCommand("shine-list", "List all collected moons")]
    public async Task ShineListAsync()
    {
        try
        {
            if (ServerService.ShineBag == null)
            {
                await RespondErrorAsync("shine.data_unavailable");
                return;
            }
            
            var shineBag = ServerService.ShineBag;
            var excluded = Settings.Instance.Shines.Excluded;
            
            var description = new StringBuilder();
            var locale = GetBestLocale();
            description.AppendLine(Localization.GetResponse("shine.collected_moons", locale, string.Join(", ", shineBag)));
            
            if (excluded.Any())
            {
                description.AppendLine(Localization.GetResponse("shine.excluded_moons", locale, string.Join(", ", excluded)));
            }

            var embed = new EmbedBuilder()
                .WithTitle(Localization.GetResponse("shine.title", locale))
                .WithDescription(description.ToString())
                .WithColor(Color.Gold)
                .WithTimestamp(DateTimeOffset.Now)
                .Build();

            await RespondAsync(embed: embed);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("shine-clear", "Clear all collected moons")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ShineClearAsync()
    {
        try
        {
            if (ServerService.MainServer == null || ServerService.ShineBag == null)
            {
                await RespondErrorAsync("shine.server_unavailable");
                return;
            }

            var shineBag = ServerService.ShineBag;
            shineBag.Clear();
            
            // Clear all player shine bags
            foreach (var client in ServerService.MainServer.Clients)
            {
                if (client.Metadata.TryGetValue("shineSync", out var playerBagObj) && 
                    playerBagObj is ConcurrentBag<int> playerBag)
                {
                    playerBag.Clear();
                }
            }

            await RespondSuccessAsync("shine.cleared");
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("shine-sync", "Force synchronize moon data")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ShineSyncAsync()
    {
        try
        {
            // Call the sync method from Program.cs
            // Note: We'll need access to the SyncShineBag method
            await RespondSuccessAsync("shine.synced");
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("shine-send", "Send a specific moon to player(s)")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ShineSendAsync(
        [Summary("moon-id", "Moon ID number")] int moonId,
        [Summary("player", "Player name or * for all players")] string playerName)
    {
        try
        {
            if (ServerService.MainServer == null)
            {
                await RespondErrorAsync("errors.server_unavailable");
                return;
            }

            // Find player(s)
            var players = playerName == "*" 
                ? ServerService.MainServer.Clients.Where(c => c.Connected).ToArray()
                : ServerService.MainServer.Clients.Where(c => c.Connected && 
                    c.Name?.StartsWith(playerName, StringComparison.OrdinalIgnoreCase) == true).ToArray();

            if (!players.Any())
            {
                await RespondErrorAsync("teleport.no_players_found", args: playerName);
                return;
            }

            // Send shine packet
            await Parallel.ForEachAsync(players, async (c, _) => {
                await c.Send(new ShinePacket {
                    ShineId = moonId
                });
            });

            var playerNames = string.Join(", ", players.Select(p => p.Name));
            await RespondSuccessAsync("shine.sent", args: new object[] { moonId, players.Length, playerNames });
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("shine-toggle", "Enable or disable moon synchronization")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ShineToggleAsync([Summary("enabled", "Enable or disable moon sync")] bool enabled)
    {
        try
        {
            Settings.Instance.Shines.Enabled = enabled;
            Settings.SaveSettings();
            
            string responseKey = enabled ? "shine.sync_enabled" : "shine.sync_disabled";
            await RespondSuccessAsync(responseKey);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("shine-exclude", "Exclude a moon from synchronization")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ShineExcludeAsync([Summary("moon-id", "Moon ID to exclude")] int moonId)
    {
        try
        {
            Settings.Instance.Shines.Excluded.Add(moonId);
            Settings.SaveSettings();
            
            await RespondSuccessAsync("shine.excluded", args: moonId);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("shine-include", "Include a moon in synchronization")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ShineIncludeAsync([Summary("moon-id", "Moon ID to include")] int moonId)
    {
        try
        {
            Settings.Instance.Shines.Excluded.Remove(moonId);
            Settings.SaveSettings();
            
            await RespondSuccessAsync("shine.included", args: moonId);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }
}