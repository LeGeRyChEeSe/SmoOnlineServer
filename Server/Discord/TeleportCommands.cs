using Discord;
using Discord.Interactions;
using Shared;
using Shared.Packet.Packets;

namespace Server.Discord;

// Commandes de téléportation
public class TeleportCommands : ModernDiscordModule
{
    public TeleportCommands(ServerService serverService, LocalizationManager localization) : base(serverService, localization) { }

    [LocalizedSlashCommand("admin.teleport", "send", "Teleport a player to a specific stage")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SendAsync(
        [LocalizedSummary("admin.teleport.stage", "stage", "Stage name")] string stageName,
        [LocalizedSummary("admin.teleport.id", "id", "Stage ID")] string stageId,
        [LocalizedSummary("admin.teleport.scenario", "scenario", "Scenario number (-1 to 127)")] int scenario,
        [LocalizedSummary("admin.teleport.player", "player", "Player name to teleport")] string playerName)
    {
        try
        {
            if (ServerService.MainServer == null)
            {
                await RespondErrorAsync("errors.server_unavailable");
                return;
            }

            // Validate stage
            string? stage = Stages.Input2Stage(stageName);
            if (stage == null)
            {
                await RespondErrorAsync("admin.invalid_stage", args: stageName);
                await FollowupAsync($"```{Stages.KingdomAliasMapping()}```", ephemeral: true);
                return;
            }

            // Validate scenario
            if (scenario < -1 || scenario > 127)
            {
                await RespondErrorAsync("admin.invalid_scenario", args: scenario);
                return;
            }

            // Find player(s)
            var players = playerName == "*" 
                ? ServerService.MainServer.Clients.Where(c => c.Connected).ToArray()
                : ServerService.MainServer.Clients.Where(c => c.Connected && 
                    (c.Name?.StartsWith(playerName, StringComparison.OrdinalIgnoreCase) == true || 
                     (Guid.TryParse(playerName, out Guid result) && result == c.Id))).ToArray();

            if (!players.Any())
            {
                await RespondErrorAsync("teleport.no_players_found", args: playerName);
                return;
            }

            // Send teleport packet
            await Parallel.ForEachAsync(players, async (c, _) => {
                await c.Send(new ChangeStagePacket {
                    Stage = stage,
                    Id = stageId,
                    Scenario = (sbyte)scenario,
                    SubScenarioType = 0
                });
            });

            var playerNames = string.Join(", ", players.Select(p => p.Name));
            await RespondSuccessAsync("teleport.teleported", args: new object[] { players.Length, playerNames, stage, scenario });
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }

    [SlashCommand("sendall", "Teleport all players to a stage")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task SendAllAsync([Summary("stage", "Stage name")] string stageName)
    {
        try
        {
            if (ServerService.MainServer == null)
            {
                await RespondErrorAsync("errors.server_unavailable");
                return;
            }

            // Validate stage
            string? stage = Stages.Input2Stage(stageName);
            if (stage == null)
            {
                await RespondErrorAsync("teleport.invalid_stage_with_help", args: new object[] { stageName, Stages.KingdomAliasMapping() });
                return;
            }

            var players = ServerService.MainServer.Clients.Where(c => c.Connected).ToArray();

            if (!players.Any())
            {
                await RespondErrorAsync("teleport.no_players_connected");
                return;
            }

            // Send teleport packet to all players
            await Parallel.ForEachAsync(players, async (c, _) => {
                await c.Send(new ChangeStagePacket {
                    Stage = stage,
                    Id = "",
                    Scenario = -1,
                    SubScenarioType = 0
                });
            });

            await RespondSuccessAsync("teleport.teleported_all", args: new object[] { players.Length, stage });
        }
        catch (Exception ex)
        {
            await RespondErrorAsync("errors.general", args: ex.Message);
        }
    }
}