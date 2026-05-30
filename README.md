![](banner.png)

[![AGPL-3.0 License](https://img.shields.io/static/v1?label=Licence&message=AGPL-3.0&color=green)](https://opensource.org/licenses/AGPL-3.0) [![GitHub Release](https://img.shields.io/static/v1?label=Version&message=1.1.1&color=blue)]() [![Patreon](https://img.shields.io/badge/Patreon-FFFFFF)](https://patreon.com/FrykesFiddlings)

This is the repository for DiscordRcon, coded by Fryke (fryke) on Discord.

DiscordRcon is a V Rising dedicated server mod that bridges Discord to your game server via ScarletRCON. It provides `/rcon` and `/help` slash commands with role-based access control, so you can manage your server from Discord without giving everyone admin access.

## Features

- **`/rcon` Command** - Execute any RCON command directly from Discord. Supports base commands and plugin commands with dotted names (e.g., `examplemod.player.mute`).
- **`/help` Command** - Browse all available RCON commands or get details for a specific command. Uses a cached command index with smart search: exact match, prefix matching (e.g., `/help command: examplemod.player` lists all player subcommands), and substring fallback.
- **"Did You Mean?" Suggestions** - If an unknown command is entered via `/rcon`, the bot searches the command index and suggests similar commands.
- **Role-Based Access Control** - Configure which Discord roles can use which commands via a simple JSON config. Admin roles grant access to everything; per-command roles add granular control.
- **ANSI to Markdown Conversion** - Terminal color codes are converted to Discord markdown (bold for headers, plain text for descriptions).
- **Connect-Per-Command RCON** - Each command opens a fresh RCON connection and closes it immediately after. Doesn't hog the connection slot, so you can still use your own RCON client alongside the bot.
- **Automatic Command Indexing** - On startup, the bot queries the server for available commands and builds a searchable index. No manual configuration needed.
- **Long Response Handling** - Responses over 2000 characters are split into multiple Discord messages using webhook followups.
- **Custom Slash Commands** - Define your own slash commands in a JSON config that map to any RCON command. Great for shortcuts to frequently used commands.

## Requirements

- [BepInEx](https://docs.bepinex.dev/) for V Rising IL2CPP
- [ScarletRCON](https://github.com/markvaaz/ScarletRCON) (hard dependency - DiscordRcon will not load without it)
- A Discord bot with the **Server Members Intent** privileged intent enabled

## How ScarletRCON Is Used

V Rising servers include a built-in RCON server, but it has limited functionality. [ScarletRCON](https://github.com/markvaaz/ScarletRCON) enhances it by allowing server mods to register custom RCON commands and providing a better help system. DiscordRcon depends on ScarletRCON for these features:

- **Command Execution** - All commands sent via `/rcon` and custom slash commands are forwarded through ScarletRCON's enhanced RCON server, which routes them to the appropriate mod handlers.
- **Command Discovery** - On startup, DiscordRcon runs ScarletRCON's `help` command to discover all registered commands (including those from other mods) and build a searchable index for `/help` and "Did you mean?" suggestions.
- **Response Formatting** - ScarletRCON returns command output with ANSI terminal color codes, which DiscordRcon converts to Discord markdown.
- **Connection Model** - ScarletRCON only allows one RCON connection at a time, so DiscordRcon opens a fresh connection per command and closes it immediately after, leaving the slot free for other RCON clients. Commands from multiple Discord users are automatically queued and processed one at a time.

## Commands

| Command | Description | Example |
|---|---|---|
| `/rcon command: <text>` | Execute any RCON command | `/rcon command: kick fryke` |
| `/help` | Show all available RCON commands | `/help` |
| `/help command: <name>` | Show help for a specific command | `/help command: examplemod.player.mute` |

## Custom Commands

Custom slash commands are configured in `BepInEx/config/DiscordRcon/discordrcon_commands.json`. This file is created automatically on first run with some common defaults:

```json
[
  {
    "name": "kick",
    "description": "Kick a player from the server",
    "rconCommand": "kick"
  },
  {
    "name": "ban",
    "description": "Ban a player from the server",
    "rconCommand": "ban"
  },
  {
    "name": "listplayers",
    "description": "List all online players",
    "rconCommand": "listplayers"
  }
]
```

You can add your own shortcuts for plugin commands too:

```json
[
  {
    "name": "mute",
    "description": "Mute a player",
    "rconCommand": "examplemod.player.mute"
  }
]
```

- **name** - The slash command name (e.g., `/kick`). Must be lowercase, 1-32 characters, and can only contain letters, numbers, hyphens, and underscores. Cannot be `rcon` or `help`.
- **description** - Shown in Discord's command picker.
- **rconCommand** - The RCON command to execute. Permission checks use this value, not the slash command name.

Each custom command gets an `arguments` option in Discord. For example, `/kick arguments: fryke` would run `kick fryke` via RCON.

Custom commands share the same permission system as `/rcon` - access is controlled by the `rconCommand` value in `discordrcon_roles.json`.

## Role Configuration

Role access is configured in `BepInEx/config/DiscordRcon/discordrcon_roles.json`. This file is created automatically on first run.

```json
{
  "adminRoles": [
    "1505734816149274734"
  ],
  "commandRoles": {
    "kick": ["1505734816149274734", "1234567890123456789"],
    "ban": ["1234567890123456789"],
    "examplemod.player.mute": ["1234567890123456789"]
  }
}
```

- **adminRoles** - Discord role IDs that can use every command. Users with one of these roles bypass per-command restrictions.
- **commandRoles** - Per-command role grants. Use the full RCON command path (e.g., `examplemod.player.mute`). Users with one of the listed roles can use that specific command, even if they are not an admin. This adds access, it does not replace admin access.

To get a Discord role ID: enable Developer Mode in Discord (Settings - Advanced), then right-click a role and click "Copy Role ID".

## Configuration

Main config is stored in `BepInEx/config/io.vrising.DiscordRcon.cfg`.

### Discord

| Key | Default | Description |
|---|---|---|
| `Discord.BotToken` | *(empty)* | Discord bot token from https://discord.com/developers/applications |
| `Discord.GuildId` | `0` | Discord server (guild) ID for registering slash commands |

### RCON

| Key | Default | Description |
|---|---|---|
| `RCON.Host` | `127.0.0.1` | RCON server hostname |
| `RCON.Port` | `25575` | RCON server port |
| `RCON.Password` | *(empty)* | RCON server password |
| `RCON.CommandTimeoutMs` | `10000` | Timeout in milliseconds for RCON command responses |

### Debug

| Key | Default | Description |
|---|---|---|
| `Debug.LogDiscordEvents` | `false` | Verbose logging of Discord gateway events |
| `Debug.LogRconEvents` | `false` | Verbose logging of RCON events |

## Data Storage

| File | Path |
|---|---|
| BepInEx config | `BepInEx/config/io.vrising.DiscordRcon.cfg` |
| Role config | `BepInEx/config/DiscordRcon/discordrcon_roles.json` |
| Custom commands | `BepInEx/config/DiscordRcon/discordrcon_commands.json` |

## Installation

1. Install [BepInEx](https://docs.bepinex.dev/) on your V Rising dedicated server.
2. Install [ScarletRCON](https://github.com/markvaaz/ScarletRCON) and configure it.
3. Place `DiscordRcon.dll` in your `BepInEx/plugins/` directory.
4. Create a Discord application:
    - Go to [Discord Developer Portal](https://discord.com/developers/applications) and create a new application.
    - Go to the **Bot** tab, click **Reset Token**, and copy the token.
    - Scroll down to **Privileged Gateway Intents** and enable **Server Members Intent**.
    - Go to the **Installation** tab, copy the pre-made URL, and open it in your browser to add the application to your server.
5. Start the server once to generate the config files, then stop it.
6. Edit `BepInEx/config/io.vrising.DiscordRcon.cfg`:
   - Set `Discord.BotToken` to your bot token.
   - Set `Discord.GuildId` to your Discord server ID (enable Developer Mode in Discord, right-click your server icon, Copy Server ID).
   - Set `RCON.Password` to your ScarletRCON password.
7. Edit `BepInEx/config/DiscordRcon/discordrcon_roles.json` and add your Discord role IDs.
8. Restart the server.

The bot will connect to Discord, register `/rcon`, `/help`, and any custom commands as slash commands, and automatically index available RCON commands after 60 seconds.

## Attribution

This project is licensed under AGPL-3.0.

This project depends on the following libraries:

- [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus) - Licensed under MIT
- [ScarletRCON](https://github.com/markvaaz/ScarletRCON) - by markvaaz

This is an independent project with its own purpose and functionality. It is not a fork, modification, or derivative of any of the above projects.
