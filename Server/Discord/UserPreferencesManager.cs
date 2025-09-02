using System.Text.Json;
using Shared;

namespace Server.Discord;

/// <summary>
/// Gestionnaire des préférences utilisateur pour la localisation
/// </summary>
public class UserPreferencesManager
{
    private readonly Dictionary<ulong, UserPreferences> _userPreferences;
    private readonly Logger _logger;
    private readonly string _preferencesFilePath;

    public UserPreferencesManager()
    {
        _userPreferences = new Dictionary<ulong, UserPreferences>();
        _logger = new Logger("UserPreferences");
        _preferencesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_preferences.json");
        LoadPreferences();
    }

    /// <summary>
    /// Obtient la langue préférée d'un utilisateur
    /// </summary>
    public string? GetUserLocale(ulong userId)
    {
        return _userPreferences.TryGetValue(userId, out var prefs) ? prefs.Locale : null;
    }

    /// <summary>
    /// Définit la langue préférée d'un utilisateur
    /// </summary>
    public async Task SetUserLocaleAsync(ulong userId, string locale, string username = "Unknown")
    {
        if (!_userPreferences.ContainsKey(userId))
        {
            _userPreferences[userId] = new UserPreferences();
        }

        _userPreferences[userId].Locale = locale;
        _userPreferences[userId].Username = username;
        _userPreferences[userId].LastUpdated = DateTime.UtcNow;

        await SavePreferencesAsync();
        _logger.Info($"Updated locale for user {username} ({userId}) to '{locale}'");
    }

    /// <summary>
    /// Supprime les préférences d'un utilisateur
    /// </summary>
    public async Task RemoveUserPreferencesAsync(ulong userId)
    {
        if (_userPreferences.Remove(userId))
        {
            await SavePreferencesAsync();
            _logger.Info($"Removed preferences for user {userId}");
        }
    }

    /// <summary>
    /// Obtient toutes les préférences utilisateur (pour administration)
    /// </summary>
    public Dictionary<ulong, UserPreferences> GetAllPreferences()
    {
        return new Dictionary<ulong, UserPreferences>(_userPreferences);
    }

    /// <summary>
    /// Obtient les statistiques d'utilisation des langues
    /// </summary>
    public Dictionary<string, int> GetLanguageStats()
    {
        var stats = new Dictionary<string, int>();
        
        foreach (var prefs in _userPreferences.Values)
        {
            if (!string.IsNullOrEmpty(prefs.Locale))
            {
                stats[prefs.Locale] = stats.GetValueOrDefault(prefs.Locale, 0) + 1;
            }
        }

        return stats;
    }

    /// <summary>
    /// Charge les préférences depuis le fichier
    /// </summary>
    private void LoadPreferences()
    {
        try
        {
            if (File.Exists(_preferencesFilePath))
            {
                var json = File.ReadAllText(_preferencesFilePath);
                var preferences = JsonSerializer.Deserialize<Dictionary<string, UserPreferences>>(json);
                
                if (preferences != null)
                {
                    _userPreferences.Clear();
                    foreach (var kvp in preferences)
                    {
                        if (ulong.TryParse(kvp.Key, out var userId))
                        {
                            _userPreferences[userId] = kvp.Value;
                        }
                    }
                }

                _logger.Info($"Loaded preferences for {_userPreferences.Count} users");
            }
            else
            {
                _logger.Info("No existing preferences file found, starting fresh");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load user preferences: {ex.Message}");
        }
    }

    /// <summary>
    /// Sauvegarde les préférences dans le fichier
    /// </summary>
    private async Task SavePreferencesAsync()
    {
        try
        {
            // Convert to string keys for JSON serialization
            var serializableDict = _userPreferences.ToDictionary(
                kvp => kvp.Key.ToString(), 
                kvp => kvp.Value
            );

            var json = JsonSerializer.Serialize(serializableDict, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_preferencesFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save user preferences: {ex.Message}");
        }
    }

    /// <summary>
    /// Nettoie les préférences anciennes (> 30 jours sans mise à jour)
    /// </summary>
    public async Task CleanupOldPreferencesAsync()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        var toRemove = _userPreferences
            .Where(kvp => kvp.Value.LastUpdated < cutoffDate)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var userId in toRemove)
        {
            _userPreferences.Remove(userId);
        }

        if (toRemove.Any())
        {
            await SavePreferencesAsync();
            _logger.Info($"Cleaned up {toRemove.Count} old user preferences");
        }
    }
}

/// <summary>
/// Préférences d'un utilisateur
/// </summary>
public class UserPreferences
{
    public string Locale { get; set; } = "en";
    public string Username { get; set; } = "Unknown";
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}