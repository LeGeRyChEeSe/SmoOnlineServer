using Discord;
using Discord.Interactions;
using Shared;
using Shared.Packet.Packets;

namespace Server.Discord;

// Commandes de gestion du jeu
public class GameCommands : ModernDiscordModule
{
    public GameCommands(ServerService serverService, LocalizationManager localization) : base(serverService, localization) { }

    [LocalizedSlashCommand("game.flip.list", "flip-list", "List all flipped players")]
    public async Task FlipListAsync()
    {
        try
        {
            var flippedPlayers = Settings.Instance.Flip.Players.ToList();
            
            if (!flippedPlayers.Any())
            {
                await RespondLocalizedAsync("game.flip.no_players");
                return;
            }

            var playerList = string.Join("\n", flippedPlayers.Select(id => $"• {id}"));
            
            await RespondWithLocalizedEmbedAsync("game.flip.list_title", colorType: "info");
            await FollowupAsync(playerList);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [LocalizedSlashCommand("game.flip.add", "flip-add", "Add a player to the flip list")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task FlipAddAsync(
        [LocalizedSummary("game.flip.add.player", "player-id", "Player ID to flip")] string playerId)
    {
        try
        {
            if (Guid.TryParse(playerId, out Guid result))
            {
                Settings.Instance.Flip.Players.Add(result);
                Settings.SaveSettings();
                await RespondSuccessAsync("game.flip.player_added", args: result);
            }
            else
            {
                await RespondErrorAsync("game.flip.invalid_id", args: playerId);
            }
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [LocalizedSlashCommand("game.flip.remove", "flip-remove", "Remove a player from the flip list")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task FlipRemoveAsync(
        [LocalizedSummary("game.flip.remove.player", "player-id", "Player ID to unflip")] string playerId)
    {
        try
        {
            if (Guid.TryParse(playerId, out Guid result))
            {
                bool removed = Settings.Instance.Flip.Players.Remove(result);
                Settings.SaveSettings();
                
                if (removed)
                {
                    await RespondSuccessAsync("game.flip.player_removed", args: result);
                }
                else
                {
                    await RespondWarningAsync("game.flip.player_not_found", args: result);
                }
            }
            else
            {
                await RespondErrorAsync("game.flip.invalid_id", args: playerId);
            }
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [LocalizedSlashCommand("game.flip.toggle", "flip-toggle", "Enable or disable player flipping")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task FlipToggleAsync(
        [LocalizedSummary("game.flip.toggle.enabled", "enabled", "Enable or disable flip")] bool enabled)
    {
        try
        {
            Settings.Instance.Flip.Enabled = enabled;
            Settings.SaveSettings();
            
            string statusKey = enabled ? "game.flip.enabled" : "game.flip.disabled";
            await RespondSuccessAsync(statusKey);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("flip-pov", "Set flip point of view")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task FlipPovAsync([Summary("pov", "Point of view")] FlipPovChoice pov)
    {
        try
        {
            FlipOptions flipOption = pov switch
            {
                FlipPovChoice.Both => FlipOptions.Both,
                FlipPovChoice.Self => FlipOptions.Self,
                FlipPovChoice.Others => FlipOptions.Others,
                _ => FlipOptions.Both
            };

            Settings.Instance.Flip.Pov = flipOption;
            Settings.SaveSettings();
            
            await RespondSuccessAsync("flip.pov_set", args: flipOption);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("scenario-merge", "Configure scenario merging")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task ScenarioMergeAsync([Summary("enabled", "Enable or disable scenario merging")] bool enabled)
    {
        try
        {
            Settings.Instance.Scenario.MergeEnabled = enabled;
            Settings.SaveSettings();
            
            string responseKey = enabled ? "flip.scenario_merge_enabled" : "flip.scenario_merge_disabled";
            await RespondSuccessAsync(responseKey);
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("tag-time", "Set tag game timer")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task TagTimeAsync(
        [Summary("player", "Player name or * for all")] string playerName,
        [Summary("minutes", "Minutes (0-65535)")] int minutes,
        [Summary("seconds", "Seconds (0-59)")] int seconds)
    {
        try
        {
            if (ServerService.MainServer == null)
            {
                await RespondErrorAsync("errors.server_unavailable");
                return;
            }

            // Validate time
            if (minutes < 0 || minutes > 65535)
            {
                await RespondErrorAsync("tag.invalid_minutes", args: minutes);
                return;
            }
            
            if (seconds < 0 || seconds > 59)
            {
                await RespondErrorAsync("tag.invalid_seconds", args: seconds);
                return;
            }

            // Find player(s)
            var players = playerName == "*" 
                ? ServerService.MainServer.Clients.Where(c => c.Connected).ToArray()
                : ServerService.MainServer.Clients.Where(c => c.Connected && 
                    c.Name?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true).ToArray();

            if (!players.Any())
            {
                await RespondErrorAsync("teleport.no_players_found", args: playerName);
                return;
            }

            // Send tag packet
            var tagPacket = new TagPacket {
                GameMode = GameMode.Legacy,
                UpdateType = TagPacket.TagUpdate.Time,
                Minutes = (ushort)minutes,
                Seconds = (byte)seconds,
            };

            await Parallel.ForEachAsync(players, async (c, _) => {
                await c.Send(tagPacket);
            });

            var playerNames = string.Join(", ", players.Select(p => p.Name));
            await RespondSuccessAsync("tag.time_set", args: new object[] { minutes, seconds, players.Length, playerNames });
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("tag-seeking", "Set player seeking status")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task TagSeekingAsync(
        [Summary("player", "Player name or * for all")] string playerName,
        [Summary("seeking", "Is player seeking")] bool seeking)
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
                    c.Name?.Equals(playerName, StringComparison.OrdinalIgnoreCase) == true).ToArray();

            if (!players.Any())
            {
                await RespondErrorAsync("teleport.no_players_found", args: playerName);
                return;
            }

            // Send tag packet
            var tagPacket = new TagPacket {
                GameMode = GameMode.Legacy,
                UpdateType = TagPacket.TagUpdate.State,
                IsIt = seeking,
            };

            await Parallel.ForEachAsync(players, async (c, _) => {
                await c.Send(tagPacket);
            });

            var playerNames = string.Join(", ", players.Select(p => p.Name));
            var locale = GetBestLocale();
            string statusKey = seeking ? "tag.seeking_status" : "tag.hiding_status";
            string status = Localization.GetResponse(statusKey, locale);
            await RespondSuccessAsync("tag.seeking_set", args: new object[] { players.Length, status, playerNames });
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }
}

// Enum for flip point of view choices
public enum FlipPovChoice
{
    [ChoiceDisplay("Both")]
    Both,
    [ChoiceDisplay("Self")]
    Self,
    [ChoiceDisplay("Others")]
    Others
}