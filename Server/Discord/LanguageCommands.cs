using Discord;
using Discord.Interactions;

namespace Server.Discord;

/// <summary>
/// Commandes de gestion de la langue du bot
/// </summary>
public class LanguageCommands : ModernDiscordModule
{
    public LanguageCommands(ServerService serverService, LocalizationManager localization) : base(serverService, localization) { }

    [SlashCommand("language", "Manage your bot language preferences")]
    public async Task LanguageAsync()
    {
        try
        {
            // Différer immédiatement la réponse pour éviter les timeouts
            await DeferAsync();

            var currentLocale = GetBestLocale();
            var userPreferredLocale = Localization.UserPreferences.GetUserLocale(Context.User.Id);
            var availableLocales = Localization.GetAvailableLocales().ToList();

            var embed = new EmbedBuilder()
                .WithTitle(Localization.GetResponse("language.title", currentLocale))
                .WithColor(Localization.GetEmbedColor("info", currentLocale))
                .WithFooter(Localization.GetEmbedFooter(currentLocale))
                .WithTimestamp(DateTimeOffset.Now);

            // Information actuelle
            var currentInfo = Localization.GetResponse("language.current_info", currentLocale, 
                GetLocaleName(currentLocale, currentLocale),
                userPreferredLocale != null ? Localization.GetResponse("language.user_set", currentLocale) : Localization.GetResponse("language.auto_detected", currentLocale));
            
            embed.AddField(
                Localization.GetResponse("language.current_language", currentLocale),
                currentInfo,
                false);

            // Langues disponibles
            var availableLanguagesText = string.Join("\n", availableLocales.Select(locale => 
            {
                var flag = GetLanguageFlag(locale);
                var name = GetLocaleName(locale, currentLocale);
                var current = locale == currentLocale ? $" **({Localization.GetResponse("language.current", currentLocale)})**" : "";
                return $"{flag} `{locale}` - {name}{current}";
            }));

            embed.AddField(
                Localization.GetResponse("language.available_languages", currentLocale),
                availableLanguagesText,
                false);

            // Instructions
            embed.AddField(
                Localization.GetResponse("language.how_to_change", currentLocale),
                Localization.GetResponse("language.change_instructions", currentLocale),
                false);

            // Créer les boutons pour changer de langue
            var components = CreateLanguageButtons(availableLocales, currentLocale);

            await FollowupAsync(embed: embed.Build(), components: components);
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine($"[ERROR] Language command timeout: {ex.Message}");
            try
            {
                await RespondAsync("❌ The command timed out. Please try again.", ephemeral: true);
            }
            catch
            {
                // Si même la réponse d'erreur échoue, rien à faire
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Language command error: {ex}");
            try
            {
                if (!Context.Interaction.HasResponded)
                {
                    await RespondAsync($"❌ An error occurred: {ex.Message}", ephemeral: true);
                }
                else
                {
                    await FollowupAsync($"❌ An error occurred: {ex.Message}", ephemeral: true);
                }
            }
            catch (Exception followupEx)
            {
                Console.WriteLine($"[ERROR] Failed to send error message: {followupEx}");
            }
        }
    }

    [SlashCommand("set-language", "Set your preferred bot language")]
    public async Task SetLanguageAsync(
        [Summary("language", "Language code (en, fr, etc.)")][Choice("English", "en")][Choice("Français", "fr")] string locale)
    {
        await SetUserLanguageAsync(locale);
    }

    /// <summary>
    /// Logique partagée pour définir la langue d'un utilisateur
    /// </summary>
    private async Task SetUserLanguageAsync(string locale)
    {
        var currentLocale = GetBestLocale();
        
        // Vérifier que la langue est supportée
        if (!Localization.IsLocaleSupported(locale))
        {
            await RespondErrorAsync("language.unsupported_language", args: locale);
            return;
        }

        // Sauvegarder la préférence
        await Localization.UserPreferences.SetUserLocaleAsync(Context.User.Id, locale, Context.User.Username);

        // Répondre dans la nouvelle langue
        var flag = GetLanguageFlag(locale);
        var languageName = GetLocaleName(locale, locale);
        
        await RespondSuccessAsync("language.language_changed", locale, flag, languageName);
    }

    [SlashCommand("reset-language", "Reset your language to auto-detection")]
    public async Task ResetLanguageAsync()
    {
        var currentLocale = GetBestLocale();
        
        await Localization.UserPreferences.RemoveUserPreferencesAsync(Context.User.Id);
        
        // Déterminer la nouvelle langue après reset
        var newLocale = Context.GetBestLocale(); // Sans les préférences utilisateur
        var flag = GetLanguageFlag(newLocale);
        var languageName = GetLocaleName(newLocale, newLocale);
        
        await RespondSuccessAsync("language.language_reset", newLocale, flag, languageName);
    }

    /// <summary>
    /// Gestionnaire pour les interactions des boutons de langue
    /// </summary>
    [ComponentInteraction("set_language:*")]
    public async Task HandleLanguageButtonAsync(string locale)
    {
        try
        {
            // Différer la réponse pour éviter les timeouts
            await DeferAsync(ephemeral: true);
            
            // Log pour déboggage
            Console.WriteLine($"[DEBUG] Button clicked for locale: {locale}");
            Console.WriteLine($"[DEBUG] User: {Context.User.Username} ({Context.User.Id})");
            
            // Vérifier que la langue est supportée
            if (!Localization.IsLocaleSupported(locale))
            {
                var currentLocale = GetBestLocale();
                var message = Localization.GetResponse("language.unsupported_language", currentLocale, locale);
                await FollowupAsync(message, ephemeral: true);
                return;
            }

            // Sauvegarder la préférence
            await Localization.UserPreferences.SetUserLocaleAsync(Context.User.Id, locale, Context.User.Username);

            // Répondre dans la nouvelle langue
            var flag = GetLanguageFlag(locale);
            var languageName = GetLocaleName(locale, locale);
            var successMessage = Localization.GetResponse("language.language_changed", locale, flag, languageName);
            
            Console.WriteLine($"[DEBUG] Success message: {successMessage}");
            await FollowupAsync(successMessage, ephemeral: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Button handler exception: {ex}");
            try
            {
                await FollowupAsync($"❌ Error: {ex.Message}", ephemeral: true);
            }
            catch (Exception followupEx)
            {
                Console.WriteLine($"[ERROR] Followup exception: {followupEx}");
            }
        }
    }

    /// <summary>
    /// Crée les boutons pour changer de langue
    /// </summary>
    private MessageComponent CreateLanguageButtons(List<string> availableLocales, string currentLocale)
    {
        var builder = new ComponentBuilder();
        
        foreach (var locale in availableLocales.Take(5)) // Discord limite à 5 boutons par row
        {
            var flag = GetLanguageFlag(locale);
            var name = GetLocaleName(locale, currentLocale);
            var isCurrentLanguage = locale == currentLocale;
            
            var button = new ButtonBuilder()
                .WithLabel($"{flag} {name}")
                .WithCustomId($"set_language:{locale}")
                .WithStyle(isCurrentLanguage ? ButtonStyle.Primary : ButtonStyle.Secondary);
                // Temporairement retiré: .WithDisabled(isCurrentLanguage);
                
            builder.WithButton(button);
        }

        return builder.Build();
    }

    /// <summary>
    /// Obtient le drapeau emoji pour une langue
    /// </summary>
    private string GetLanguageFlag(string locale)
    {
        return locale switch
        {
            "en" => "🇺🇸",
            "fr" => "🇫🇷",
            "es" => "🇪🇸",
            "de" => "🇩🇪",
            "it" => "🇮🇹",
            "pt" => "🇵🇹",
            "ru" => "🇷🇺",
            "ja" => "🇯🇵",
            "ko" => "🇰🇷",
            "zh" => "🇨🇳",
            _ => "🌐"
        };
    }

    /// <summary>
    /// Obtient le nom d'une langue dans la langue spécifiée
    /// </summary>
    private string GetLocaleName(string locale, string displayLocale)
    {
        // Utiliser le système de localisation pour les noms de langues
        var localizedName = Localization.GetResponse($"language.names.{locale}", displayLocale);
        
        // Si la clé n'existe pas (retourne la clé complète), fallback au code de langue en majuscules
        if (localizedName == $"responses.language.names.{locale}" || localizedName == $"language.names.{locale}")
        {
            return locale.ToUpperInvariant();
        }
        
        return localizedName;
    }
}