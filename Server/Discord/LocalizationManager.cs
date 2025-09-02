using System.Globalization;
using System.Text.Json;
using Shared;
using Discord.Interactions;
using Discord;

namespace Server.Discord;

/// <summary>
/// Gestionnaire de localisation avancé pour les commandes Discord avec support de fichiers JSON
/// </summary>
public class LocalizationManager
{
    private readonly Dictionary<string, LocalizationData> _localizations;
    private readonly Logger _logger;
    private readonly string _localizationPath;
    private readonly UserPreferencesManager _userPreferences;
    
    public LocalizationManager()
    {
        _localizations = new Dictionary<string, LocalizationData>();
        _logger = new Logger("LocalizationManager");
        _localizationPath = FindLocalizationPath();
        _userPreferences = new UserPreferencesManager();
        LoadLocalizations();
    }
    
    /// <summary>
    /// Obtient le gestionnaire de préférences utilisateur
    /// </summary>
    public UserPreferencesManager UserPreferences => _userPreferences;
    
    private string FindLocalizationPath()
    {
        // Essayer plusieurs chemins possibles
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Discord", "Localization"),
            Path.Combine(Directory.GetCurrentDirectory(), "Discord", "Localization"),
            Path.Combine(Directory.GetCurrentDirectory(), "Server", "Discord", "Localization"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Server", "Discord", "Localization"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Discord", "Localization")
        };

        foreach (var path in possiblePaths)
        {
            try
            {
                var normalizedPath = Path.GetFullPath(path);
                if (Directory.Exists(normalizedPath))
                {
                    _logger.Info($"Found localization directory: {normalizedPath}");
                    return normalizedPath;
                }
            }
            catch
            {
                // Ignore invalid paths
            }
        }

        // Default fallback
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Discord", "Localization");
    }
    
    private void LoadLocalizations()
    {
        try
        {
            if (!Directory.Exists(_localizationPath))
            {
                _logger.Error($"Localization directory not found: {_localizationPath}");
                return;
            }

            var localizationFiles = Directory.GetFiles(_localizationPath, "*.json");
            
            foreach (var file in localizationFiles)
            {
                var locale = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var jsonContent = File.ReadAllText(file);
                    var localizationData = JsonSerializer.Deserialize<LocalizationData>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (localizationData != null)
                    {
                        _localizations[locale] = localizationData;
                        _logger.Info($"Loaded localization for '{locale}' from {file}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to load localization file {file}: {ex.Message}");
                }
            }
            
            if (!_localizations.ContainsKey("en"))
            {
                _logger.Warn("English localization not found, creating fallback");
                CreateFallbackLocalization();
            }
            
            _logger.Info($"Loaded {_localizations.Count} localizations: {string.Join(", ", _localizations.Keys)}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load localizations: {ex.Message}");
            CreateFallbackLocalization();
        }
    }
    
    private void CreateFallbackLocalization()
    {
        _localizations["en"] = new LocalizationData
        {
            Commands = new CommandLocalizations(),
            Responses = new ResponseLocalizations(),
            Embeds = new EmbedLocalizations()
        };
    }
    
    public string GetCommandName(string path, string locale = "en")
    {
        return GetNestedValue($"commands.{path}.name", locale);
    }
    
    public string GetCommandDescription(string path, string locale = "en")
    {
        return GetNestedValue($"commands.{path}.description", locale);
    }
    
    public string GetResponse(string path, string locale = "en", params object[] args)
    {
        var message = GetNestedValue($"responses.{path}", locale);
        if (args.Length > 0)
        {
            try
            {
                return string.Format(message, args);
            }
            catch
            {
                return message;
            }
        }
        return message;
    }
    
    public string GetEmbedFooter(string locale = "en")
    {
        return GetNestedValue("embeds.footer", locale);
    }
    
    public uint GetEmbedColor(string colorType, string locale = "en")
    {
        var colorHex = GetNestedValue($"embeds.colors.{colorType}", locale);
        if (colorHex.StartsWith("0x"))
        {
            return Convert.ToUInt32(colorHex, 16);
        }
        return 0x0099FF; // Default blue
    }
    
    private string GetNestedValue(string path, string locale)
    {
        if (!_localizations.ContainsKey(locale))
            locale = "en";
            
        if (!_localizations.ContainsKey(locale))
            return path;
            
        var data = _localizations[locale];
        var parts = path.Split('.');
        
        try
        {
            object? current = data;
            
            foreach (var part in parts)
            {
                if (current is LocalizationData locData)
                {
                    current = part switch
                    {
                        "commands" => locData.Commands,
                        "responses" => locData.Responses,
                        "embeds" => locData.Embeds,
                        _ => null
                    };
                }
                else if (current is Dictionary<string, string> dict)
                {
                    dict.TryGetValue(part, out var value);
                    current = value;
                }
                else
                {
                    var prop = current?.GetType().GetProperty(part, 
                        System.Reflection.BindingFlags.IgnoreCase | 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.Instance);
                    current = prop?.GetValue(current);
                }
                
                if (current == null) break;
            }
            
            return current?.ToString() ?? path;
        }
        catch
        {
            return path;
        }
    }
    
    public IEnumerable<string> GetAvailableLocales()
    {
        return _localizations.Keys;
    }
    
    public void ReloadLocalizations()
    {
        _localizations.Clear();
        LoadLocalizations();
    }
    
    public bool IsLocaleSupported(string locale)
    {
        return _localizations.ContainsKey(locale);
    }
}

#region Localization Data Models

public class LocalizationData
{
    public CommandLocalizations Commands { get; set; } = new();
    public ResponseLocalizations Responses { get; set; } = new();
    public EmbedLocalizations Embeds { get; set; } = new();
}

public class CommandLocalizations
{
    public ServerCommandsLocalization Server { get; set; } = new();
    public AdminCommandsLocalization Admin { get; set; } = new();
    public GameCommandsLocalization Game { get; set; } = new();
    public SettingsCommandsLocalization Settings { get; set; } = new();
}

public class ServerCommandsLocalization
{
    public CommandInfo Status { get; set; } = new();
    public CommandInfo Players { get; set; } = new();
}

public class AdminCommandsLocalization
{
    public CommandWithOptions Ban { get; set; } = new();
    public CommandWithOptions Unban { get; set; } = new();
    public CommandWithOptions Kick { get; set; } = new();
    public CommandWithOptions Teleport { get; set; } = new();
}

public class GameCommandsLocalization
{
    public CommandWithSubcommands Shine { get; set; } = new();
    public CommandWithOptions Flip { get; set; } = new();
    public CommandWithSubcommands Tag { get; set; } = new();
}

public class SettingsCommandsLocalization
{
    public CommandInfo Reload { get; set; } = new();
    public CommandInfo View { get; set; } = new();
}

public class CommandInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class CommandWithOptions : CommandInfo
{
    public Dictionary<string, OptionInfo> Options { get; set; } = new();
}

public class CommandWithSubcommands : CommandInfo
{
    public Dictionary<string, CommandInfo> Subcommands { get; set; } = new();
}

public class OptionInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class ResponseLocalizations
{
    public ServerResponses Server { get; set; } = new();
    public AdminResponses Admin { get; set; } = new();
    public GameResponses Game { get; set; } = new();
    public SettingsResponses Settings { get; set; } = new();
    public ErrorResponses Errors { get; set; } = new();
    public LanguageResponses Language { get; set; } = new();
    public BanResponses Ban { get; set; } = new();
    public ShineResponses Shine { get; set; } = new();
    public TeleportResponses Teleport { get; set; } = new();
    public TagResponses Tag { get; set; } = new();
    public FlipResponses Flip { get; set; } = new();
}

public class ServerResponses
{
    public string Status_Title { get; set; } = "Server Status";
    public string Status_Info { get; set; } = "**Server:** {0}\n**Port:** {1}\n**Connected Players:** {2}\n**Uptime:** {3}";
    public string Players_Title { get; set; } = "Connected Players";
    public string No_Players { get; set; } = "No players currently connected";
    public string Player_Info { get; set; } = "**{0}** - Stage: {1} | Scenario: {2}";
}

public class AdminResponses
{
    public string Admin_Only { get; set; } = "This command requires administrator permissions";
    public string Player_Banned { get; set; } = "Player **{0}** has been banned";
    public string Player_Unbanned { get; set; } = "Player **{0}** has been unbanned";
    public string Player_Kicked { get; set; } = "Player **{0}** has been kicked";
    public string Player_Not_Found { get; set; } = "Player not found: **{0}**";
    public string Player_Teleported { get; set; } = "Player **{0}** teleported to **{1}**";
    public string Invalid_Stage { get; set; } = "Invalid stage: **{0}**";
}

public class GameResponses
{
    public string Shine_Synced { get; set; } = "Moon data synchronized";
    public string Shine_Cleared { get; set; } = "All collected moons cleared";
    public string Flip_Applied { get; set; } = "Flip effect **{0}** applied to **{1}**";
    public string Tag_Started { get; set; } = "Tag game started!";
    public string Tag_Stopped { get; set; } = "Tag game stopped";
    public string Tag_Seeker_Set { get; set; } = "**{0}** is now the seeker";
    public string Tag_Not_Active { get; set; } = "No tag game is currently active";
}

public class SettingsResponses
{
    public string Reloaded { get; set; } = "Server settings reloaded successfully";
    public string Reload_Error { get; set; } = "Error reloading settings: {0}";
    public string Settings_Title { get; set; } = "Server Settings";
    public string Settings_Info { get; set; } = "**Max Players:** {0}\n**Persist Shines:** {1}\n**Flip Players:** {2}";
}

public class ErrorResponses
{
    public string General { get; set; } = "An error occurred: {0}";
    public string Command_Failed { get; set; } = "Command failed to execute";
    public string Internal_Error { get; set; } = "Internal server error";
}

public class LanguageResponses
{
    public string Title { get; set; } = "🌐 Language Settings";
    public string Current_Language { get; set; } = "Current Language";
    public string Current_Info { get; set; } = "{0} ({1})";
    public string User_Set { get; set; } = "User preference";
    public string Auto_Detected { get; set; } = "Auto-detected";
    public string Current { get; set; } = "current";
    public string Available_Languages { get; set; } = "Available Languages";
    public string How_To_Change { get; set; } = "How to Change";
    public string Change_Instructions { get; set; } = "Use the buttons below or `/set-language` command";
    public string Language_Changed { get; set; } = "✅ {0} Language changed to **{1}**";
    public string Language_Reset { get; set; } = "✅ {0} Language reset to **{1}** (auto-detection)";
    public string Unsupported_Language { get; set; } = "❌ Language `{0}` is not supported";
    public Dictionary<string, string> Names { get; set; } = new();
}

public class BanResponses
{
    public string Title { get; set; } = "🔨 Ban List";
    public string System_Status { get; set; } = "**Ban System:** {0}";
    public string Enabled { get; set; } = "✅ Enabled";
    public string Disabled { get; set; } = "❌ Disabled";
    public string Banned_Players { get; set; } = "**Banned Players:**";
    public string Banned_Ips { get; set; } = "**Banned IP Addresses:**";
    public string Banned_Stages { get; set; } = "**Banned Stages:**";
    public string Banned_Gamemodes { get; set; } = "**Banned Game Modes:**";
    public string No_Bans { get; set; } = "*No bans currently active.*";
    public string Player_Banned { get; set; } = "🔨 Player **{0}** has been banned (Profile + IP)";
    public string Unbanned { get; set; } = "✅ Unbanned: {0}";
    public string Nothing_Unbanned { get; set; } = "❌ Nothing was unbanned";
    public string Multiple_Params_Error { get; set; } = "❌ You cannot specify both `player` and `player_id` at the same time";
    public string No_Params_Error { get; set; } = "❌ You must specify at least one parameter: `player`, `player_id`, or `ip`";
    public string Player_Not_In_Banlist { get; set; } = "❌ Player **{0}** was not found in ban list";
    public string Playerid_Not_In_Banlist { get; set; } = "❌ Player ID **{0}** was not found in ban list";
    public string Ip_Not_In_Banlist { get; set; } = "❌ IP address **{0}** was not found in ban list";
    public string Invalid_Playerid_Format { get; set; } = "❌ Invalid player ID format: **{0}**";
    public string Invalid_Ip_Format { get; set; } = "❌ Invalid IP address format: **{0}**";
    public string Player_Crashed { get; set; } = "💥 Player **{0}** has been crashed";
    public string Player_Rejoined { get; set; } = "🔄 Player **{0}** has been forced to rejoin";
    public string Maxplayers_Set { get; set; } = "✅ Maximum players set to **{0}**. All players have been disconnected to apply changes.";
    public string Invalid_Maxplayers { get; set; } = "❌ Invalid player count: {0}. Must be between 1 and {1}";
}

public class ShineResponses
{
    public string Title { get; set; } = "🌙 Moon Collection Status";
    public string Collected_Moons { get; set; } = "**Collected Moons:** {0}";
    public string Excluded_Moons { get; set; } = "**Excluded Moons:** {0}";
    public string Data_Unavailable { get; set; } = "❌ Shine data is not available";
    public string Cleared { get; set; } = "✅ **All collected moons have been cleared**";
    public string Synced { get; set; } = "✅ **Moon data synchronized automatically**";
    public string Sent { get; set; } = "🌙 Sent moon **{0}** to **{1}** player(s): {2}";
    public string Sync_Enabled { get; set; } = "🌙 Moon synchronization has been **enabled**";
    public string Sync_Disabled { get; set; } = "🌙 Moon synchronization has been **disabled**";
    public string Excluded { get; set; } = "🚫 Moon **{0}** has been excluded from synchronization";
    public string Included { get; set; } = "✅ Moon **{0}** has been included in synchronization";
    public string Server_Unavailable { get; set; } = "❌ Server or shine data is not available";
}

public class TeleportResponses
{
    public string No_Players_Found { get; set; } = "❌ No players found matching: {0}";
    public string Teleported { get; set; } = "📍 Teleported **{0}** player(s) ({1}) to **{2}** (scenario {3})";
    public string Teleported_All { get; set; } = "📍 Teleported all **{0}** player(s) to **{1}**";
    public string No_Players_Connected { get; set; } = "❌ No players currently connected";
    public string Invalid_Stage_With_Help { get; set; } = "❌ Invalid stage name: {0}\\n```{1}```";
}

public class TagResponses
{
    public string Time_Set { get; set; } = "⏱️ Set tag timer to **{0}:{1:D2}** for **{2}** player(s): {3}";
    public string Seeking_Set { get; set; } = "👁️ Set **{0}** player(s) to **{1}**: {2}";
    public string Seeking_Status { get; set; } = "seeking";
    public string Hiding_Status { get; set; } = "hiding";
    public string Invalid_Minutes { get; set; } = "❌ Invalid minutes: {0} (range: 0-65535)";
    public string Invalid_Seconds { get; set; } = "❌ Invalid seconds: {0} (range: 0-59)";
}

public class FlipResponses
{
    public string Pov_Set { get; set; } = "🔄 Flip point of view set to **{0}**";
    public string Scenario_Merge_Enabled { get; set; } = "🎬 Scenario merging has been **enabled**";
    public string Scenario_Merge_Disabled { get; set; } = "🎬 Scenario merging has been **disabled**";
}

public class EmbedLocalizations
{
    public Dictionary<string, string> Colors { get; set; } = new()
    {
        ["success"] = "0x00FF00",
        ["error"] = "0xFF0000",
        ["info"] = "0x0099FF",
        ["warning"] = "0xFFAA00"
    };
    public string Footer { get; set; } = "Super Mario Odyssey Online Server";
}

#endregion

/// <summary>
/// Attribut pour les commandes slash localisées
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class LocalizedSlashCommandAttribute : SlashCommandAttribute
{
    public string LocalizationPath { get; }
    
    public LocalizedSlashCommandAttribute(string localizationPath, string defaultName, string defaultDescription) 
        : base(defaultName, defaultDescription)
    {
        LocalizationPath = localizationPath;
    }
}

/// <summary>
/// Attribut pour les options de commandes localisées
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class LocalizedSummaryAttribute : SummaryAttribute
{
    public string LocalizationPath { get; }
    
    public LocalizedSummaryAttribute(string localizationPath, string defaultName, string defaultDescription) 
        : base(defaultName, defaultDescription)
    {
        LocalizationPath = localizationPath;
    }
}

/// <summary>
/// Extensions pour la gestion des locales Discord
/// </summary>
public static class LocaleExtensions
{
    /// <summary>
    /// Obtient la locale préférée de l'utilisateur Discord
    /// </summary>
    public static string GetUserLocale(this SocketInteractionContext context)
    {
        var userLocale = context.Interaction.UserLocale ?? "en";
        return userLocale.StartsWith("fr") ? "fr" : "en";
    }
    
    /// <summary>
    /// Obtient la locale préférée du serveur Discord
    /// </summary>
    public static string GetGuildLocale(this SocketInteractionContext context)
    {
        var guildLocale = context.Interaction.GuildLocale ?? "en";
        return guildLocale.StartsWith("fr") ? "fr" : "en";
    }
    
    /// <summary>
    /// Obtient la meilleure locale à utiliser (priorité: préférences utilisateur > anglais par défaut)
    /// </summary>
    public static string GetBestLocale(this SocketInteractionContext context, UserPreferencesManager? userPreferences = null)
    {
        // 1. Préférences utilisateur explicites (si disponibles)
        if (userPreferences != null)
        {
            var userPref = userPreferences.GetUserLocale(context.User.Id);
            if (!string.IsNullOrEmpty(userPref))
            {
                return userPref;
            }
        }
        
        // 2. Anglais par défaut (plus de détection automatique)
        return "en";
    }
}