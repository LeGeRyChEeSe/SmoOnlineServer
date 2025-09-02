# Système de Traduction Discord Bot

Ce document décrit le système complet de traduction implémenté pour le bot Discord du serveur Super Mario Odyssey Online.

## Architecture

### Structure des Fichiers

```
Server/Discord/
├── Localization/
│   ├── en.json           # Traductions anglaises
│   ├── fr.json           # Traductions françaises
│   └── [autre-langue].json
├── LocalizationManager.cs
├── LocalizationDocumentationGenerator.cs
└── ModernDiscordBot.cs (mis à jour avec méthodes helper)
```

### Composants Principaux

#### LocalizationManager
- **Chargement automatique** des fichiers JSON de traduction
- **Gestion des langues** avec fallback vers l'anglais
- **API simple** pour récupérer les traductions
- **Support des paramètres** avec `string.Format`

#### ModernDiscordModule (étendu)
- **Méthodes helper** pour réponses localisées :
  - `RespondLocalizedAsync()` - Messages simples
  - `RespondWithLocalizedEmbedAsync()` - Embeds avec couleurs
  - `RespondErrorAsync()` - Messages d'erreur
  - `RespondSuccessAsync()` - Messages de succès
  - `RespondWarningAsync()` - Messages d'avertissement

#### Attributs de Localisation
- `[LocalizedSlashCommand]` - Pour les commandes slash
- `[LocalizedSummary]` - Pour les paramètres de commandes

## Utilisation

### 1. Commandes Discord

```csharp
[LocalizedSlashCommand("game.flip.list", "flip-list", "List all flipped players")]
public async Task FlipListAsync()
{
    // Le système détecte automatiquement la langue de l'utilisateur
    await RespondLocalizedAsync("game.flip.no_players");
}
```

### 2. Messages avec Paramètres

```csharp
await RespondSuccessAsync("admin.player_banned", args: playerName);
```

### 3. Embeds Personnalisés

```csharp
await RespondWithLocalizedEmbedAsync(
    "server.status_title", 
    "server.status_info",
    colorType: "info",
    args: serverName, port, playerCount, uptime);
```

### 4. Gestion des Erreurs

```csharp
try 
{
    // Code...
}
catch (Exception ex)
{
    await RespondErrorAsync("errors.general", args: ex.Message);
}
```

## Détection de Langue

Le système priorise les langues dans cet ordre :
1. **Langue de l'utilisateur Discord** (UserLocale)
2. **Langue du serveur Discord** (GuildLocale)  
3. **Anglais par défaut**

```csharp
// Détection automatique
var locale = Context.GetBestLocale();

// Ou manuel
await RespondLocalizedAsync("message.key", locale: "fr");
```

## Structure des Fichiers JSON

### Format Standard

```json
{
  "commands": {
    "server": {
      "status": {
        "name": "status",
        "description": "Show server status"
      }
    },
    "admin": {
      "ban": {
        "name": "ban",
        "description": "Ban a player",
        "options": {
          "player": {
            "name": "player",
            "description": "Player to ban"
          }
        }
      }
    }
  },
  "responses": {
    "server": {
      "status_title": "Server Status",
      "status_info": "**Server:** {0}\\n**Players:** {1}"
    },
    "admin": {
      "player_banned": "✅ Player **{0}** has been banned"
    },
    "errors": {
      "general": "❌ An error occurred: {0}"
    }
  },
  "embeds": {
    "colors": {
      "success": "0x00FF00",
      "error": "0xFF0000",
      "info": "0x0099FF",
      "warning": "0xFFAA00"
    },
    "footer": "Super Mario Odyssey Online Server"
  }
}
```

## Ajouter une Nouvelle Langue

### 1. Créer le Fichier JSON

Créer `Server/Discord/Localization/[code-langue].json` en copiant la structure de `en.json`.

### 2. Traduire le Contenu

```json
{
  "commands": {
    "server": {
      "status": {
        "name": "statut",
        "description": "Afficher le statut du serveur"
      }
    }
  },
  "responses": {
    "server": {
      "status_title": "Statut du Serveur"
    }
  }
}
```

### 3. Mettre à Jour la Détection

Modifier `LocaleExtensions.GetBestLocale()` pour supporter la nouvelle langue :

```csharp
public static string GetBestLocale(this SocketInteractionContext context)
{
    var userLocale = context.Interaction.UserLocale ?? "en";
    
    // Support pour plus de langues
    if (userLocale.StartsWith("fr")) return "fr";
    if (userLocale.StartsWith("es")) return "es";
    if (userLocale.StartsWith("de")) return "de";
    
    return "en"; // Fallback
}
```

## Validation et Documentation

### Générateur de Documentation

```csharp
var generator = new LocalizationDocumentationGenerator(localizationManager);

// Rapport complet
string report = generator.GenerateTranslationReport();

// Validation
var validation = generator.ValidateTranslations();
Console.WriteLine(validation.GetReport());

// Template pour nouvelle langue
string template = generator.GenerateLanguageTemplate("en");
```

### Validation Automatique

Le `LocalizationDocumentationGenerator` peut :
- **Détecter les clés manquantes** entre langues
- **Identifier les traductions vides**
- **Générer des rapports** de couverture
- **Créer des templates** pour nouvelles langues

## Meilleures Pratiques

### 1. Nommage des Clés

```
category.subcategory.action
admin.ban.player_banned
game.flip.enabled
errors.server_unavailable
```

### 2. Paramètres

Utiliser `{0}`, `{1}`, etc. pour les paramètres dynamiques :

```json
"player_teleported": "✅ Player **{0}** teleported to **{1}**"
```

### 3. Émojis et Formatage

Inclure émojis et formatage Discord directement dans les traductions :

```json
"success_message": "✅ **Success!** Operation completed",
"error_message": "❌ **Error:** {0}"
```

### 4. Couleurs d'Embed

Définir des couleurs sémantiques :

```json
"embeds": {
  "colors": {
    "success": "0x00FF00",
    "error": "0xFF0000", 
    "info": "0x0099FF",
    "warning": "0xFFAA00"
  }
}
```

## Migration du Code Existant

### Avant (Hardcodé)

```csharp
await RespondAsync("❌ Player not found: " + playerName, ephemeral: true);
```

### Après (Localisé)

```csharp
await RespondErrorAsync("admin.player_not_found", args: playerName);
```

### Avant (Embed Hardcodé)

```csharp
var embed = new EmbedBuilder()
    .WithTitle("Server Status")
    .WithColor(Color.Green)
    .WithDescription($"Players: {count}")
    .Build();
await RespondAsync(embed: embed);
```

### Après (Embed Localisé)

```csharp
await RespondWithLocalizedEmbedAsync(
    "server.status_title", 
    "server.status_info",
    colorType: "success",
    args: count);
```

## Configuration

### Settings.json

Ajouter une section pour la configuration de localisation :

```json
{
  "Discord": {
    "DefaultLocale": "en",
    "SupportedLocales": ["en", "fr"],
    "FallbackToEnglish": true
  }
}
```

### Rechargement à Chaud

```csharp
// Recharger les traductions sans redémarrer
localizationManager.ReloadLocalizations();
```

Ce système offre une solution complète et extensible pour la localisation du bot Discord, permettant un support multilingue robuste avec une maintenance simplifiée.