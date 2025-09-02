using System.Text;
using System.Text.Json;
using Shared;

namespace Server.Discord;

/// <summary>
/// Générateur de documentation pour le système de localisation
/// </summary>
public class LocalizationDocumentationGenerator
{
    private readonly LocalizationManager _localization;
    private readonly Logger _logger;

    public LocalizationDocumentationGenerator(LocalizationManager localization)
    {
        _localization = localization;
        _logger = new Logger("LocalizationDoc");
    }

    /// <summary>
    /// Génère un rapport de toutes les traductions disponibles
    /// </summary>
    public string GenerateTranslationReport()
    {
        var report = new StringBuilder();
        report.AppendLine("# Translation System Report");
        report.AppendLine();

        var locales = _localization.GetAvailableLocales().ToList();
        report.AppendLine($"## Available Locales: {string.Join(", ", locales)}");
        report.AppendLine();

        foreach (var locale in locales)
        {
            report.AppendLine($"## Locale: {locale.ToUpper()}");
            report.AppendLine();
            
            // Commands section
            report.AppendLine("### Commands");
            GenerateCommandsSection(report, locale);
            report.AppendLine();

            // Responses section
            report.AppendLine("### Responses");
            GenerateResponsesSection(report, locale);
            report.AppendLine();
        }

        return report.ToString();
    }

    private void GenerateCommandsSection(StringBuilder report, string locale)
    {
        var commands = new[]
        {
            ("server.status", "Server Status Command"),
            ("server.players", "Server Players Command"),
            ("admin.ban", "Ban Player Command"),
            ("admin.unban", "Unban Player Command"),
            ("admin.kick", "Kick Player Command"),
            ("admin.teleport", "Teleport Player Command"),
            ("game.shine", "Shine Commands"),
            ("game.flip", "Flip Commands"),
            ("game.tag", "Tag Commands"),
            ("settings.reload", "Reload Settings Command"),
            ("settings.view", "View Settings Command")
        };

        foreach (var (path, description) in commands)
        {
            var name = _localization.GetCommandName(path, locale);
            var desc = _localization.GetCommandDescription(path, locale);
            report.AppendLine($"- **{description}**: `/{name}` - {desc}");
        }
    }

    private void GenerateResponsesSection(StringBuilder report, string locale)
    {
        var responses = new[]
        {
            ("server.status_title", "Server Status Title"),
            ("server.players_title", "Server Players Title"),
            ("admin.admin_only", "Admin Only Message"),
            ("admin.player_banned", "Player Banned Message"),
            ("admin.player_unbanned", "Player Unbanned Message"),
            ("game.shine_synced", "Shine Synced Message"),
            ("game.flip.enabled", "Flip Enabled Message"),
            ("game.flip.disabled", "Flip Disabled Message"),
            ("errors.general", "General Error Message"),
            ("errors.server_unavailable", "Server Unavailable Message")
        };

        foreach (var (path, description) in responses)
        {
            var message = _localization.GetResponse(path, locale);
            report.AppendLine($"- **{description}**: {message}");
        }
    }

    /// <summary>
    /// Valide que toutes les clés de traduction existent pour toutes les langues
    /// </summary>
    public ValidationResult ValidateTranslations()
    {
        var result = new ValidationResult();
        var locales = _localization.GetAvailableLocales().ToList();
        
        if (locales.Count < 2)
        {
            result.Warnings.Add("Less than 2 locales found. Consider adding more language support.");
        }

        // Collect all unique keys from all locales
        var allKeys = new HashSet<string>();
        var localeKeys = new Dictionary<string, HashSet<string>>();

        foreach (var locale in locales)
        {
            var keys = ExtractAllKeys(locale);
            localeKeys[locale] = keys;
            foreach (var key in keys)
            {
                allKeys.Add(key);
            }
        }

        // Find missing keys
        foreach (var locale in locales)
        {
            var keys = localeKeys[locale];
            var missingKeys = allKeys.Except(keys).ToList();
            
            if (missingKeys.Any())
            {
                result.Errors.Add($"Locale '{locale}' is missing {missingKeys.Count} translation keys: {string.Join(", ", missingKeys.Take(5))}");
            }
        }

        // Check for empty translations
        foreach (var locale in locales)
        {
            foreach (var key in allKeys)
            {
                var value = _localization.GetResponse(key, locale);
                if (string.IsNullOrWhiteSpace(value) || value == key)
                {
                    result.Warnings.Add($"Locale '{locale}' has empty or fallback translation for key '{key}'");
                }
            }
        }

        return result;
    }

    private HashSet<string> ExtractAllKeys(string locale)
    {
        var keys = new HashSet<string>();
        
        // This is a simplified key extraction - in a real implementation,
        // you'd want to traverse the JSON structure properly
        var testKeys = new[]
        {
            "server.status_title", "server.players_title", "server.no_players",
            "admin.admin_only", "admin.player_banned", "admin.player_unbanned",
            "admin.invalid_stage", "admin.invalid_scenario",
            "game.shine_synced", "game.shine_cleared",
            "game.flip.enabled", "game.flip.disabled", "game.flip.list_title",
            "errors.general", "errors.server_unavailable"
        };

        foreach (var key in testKeys)
        {
            var value = _localization.GetResponse(key, locale);
            if (!string.IsNullOrEmpty(value) && value != key)
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    /// <summary>
    /// Génère un template pour une nouvelle langue
    /// </summary>
    public string GenerateLanguageTemplate(string baseLocale = "en")
    {
        var template = new StringBuilder();
        template.AppendLine($"// Language template based on '{baseLocale}'");
        template.AppendLine("// Replace all values with translations for your target language");
        template.AppendLine();

        var keys = ExtractAllKeys(baseLocale);
        template.AppendLine("{");
        
        var groupedKeys = keys.GroupBy(k => k.Split('.')[0]).ToList();
        
        foreach (var group in groupedKeys)
        {
            template.AppendLine($"  \"{group.Key}\": {{");
            
            foreach (var key in group)
            {
                var value = _localization.GetResponse(key, baseLocale);
                var subKey = string.Join(".", key.Split('.').Skip(1));
                template.AppendLine($"    \"{subKey}\": \"{EscapeJsonString(value)}\",");
            }
            
            template.AppendLine("  },");
        }
        
        template.AppendLine("}");
        
        return template.ToString();
    }

    private static string EscapeJsonString(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}

/// <summary>
/// Résultat de validation des traductions
/// </summary>
public class ValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    
    public bool IsValid => !Errors.Any();
    
    public string GetReport()
    {
        var report = new StringBuilder();
        
        if (IsValid)
        {
            report.AppendLine("✅ All translations are valid!");
        }
        else
        {
            report.AppendLine("❌ Translation validation failed:");
            foreach (var error in Errors)
            {
                report.AppendLine($"  - {error}");
            }
        }
        
        if (Warnings.Any())
        {
            report.AppendLine("\n⚠️ Warnings:");
            foreach (var warning in Warnings)
            {
                report.AppendLine($"  - {warning}");
            }
        }
        
        return report.ToString();
    }
}