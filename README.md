![](banner.png)

[![AGPL-3.0 License](https://img.shields.io/static/v1?label=Licence&message=AGPL-3.0&color=green)](https://opensource.org/licenses/AGPL-3.0) [![GitHub Release](https://img.shields.io/static/v1?label=Version&message=1.0.0&color=blue)]() [![Patreon](https://img.shields.io/badge/Patreon-FFFFFF)](https://patreon.com/FrykesFiddlings)

This is the repository for DiscordRcon, coded by Fryke (fryke) on Discord.

DiscordRcon is a V Rising dedicated server mod that bridges Discord to your game server via ScarletRCON. It exposes all RCON commands as Discord slash commands and supports role-based access control, so you can manage your server from Discord without giving everyone admin access.

## Features

- **Slash Command Auto-Discovery** - Automatically reads ScarletRCON's `help` output and registers every command as a Discord slash command. No manual configuration needed.
- **Bulk Command Sync** - On each discovery cycle, the full command list is pushed to Discord. Commands that are removed from ScarletRCON are automatically cleaned up.
- **Role-Based Access Control** - Configure which Discord roles can use which commands via a simple JSON config. Default roles apply to all commands, and per-command overrides are supported.
- **Prefix Commands** - Use `!rcon <command>` in any channel the bot can see, in addition to slash commands.
- **ANSI to Markdown Conversion** - ScarletRCON's terminal color codes are converted to Discord markdown (bold for headers, plain text for descriptions).
- **Connect-Per-Command RCON** - Each command opens a fresh RCON connection and closes it immediately after. Doesn't hog the connection slot, so you can still use your own RCON client alongside the bot.
- **Automatic Retries** - If ScarletRCON isn't ready when the bot starts, discovery keeps retrying every 30 seconds until it succeeds.
- **Long Response Handling** - Responses over 2000 characters are split into multiple Discord messages using webhook followups.

## Requirements

- [BepInEx](https://docs.bepinex.dev/) for V Rising IL2CPP
- [ScarletRCON](https://github.com/markvaaz/ScarletRCON) (hard dependency - DiscordRcon will not load without it)
- A Discord bot with the **GUILD_MEMBERS** privileged intent enabled

## Commands

All ScarletRCON commands are automatically available as slash commands in Discord. You can also use prefix commands:

| Usage | Example |
|---|---|
| `/commandname arguments` | `/help` or `/kick fryke` |
| `!rcon command arguments` | `!rcon help` or `!rcon kick fryke` |

The prefix (`!rcon` by default) is configurable in the BepInEx config.

## Role Configuration

Role access is configured in `BepInEx/config/DiscordRcon/discordrcon_roles.json`. This file is created automatically on first run.

```json
{
  "DefaultRoles": [
    "1505734816149274734"
  ],
  "CommandOverrides": {
    "kick": ["1505734816149274734", "1234567890123456789"],
    "shutdown": ["1234567890123456789"]
  }
}
```

- **DefaultRoles** - Discord role IDs that can use any command. Users must have at least one of these roles unless a command has an override.
- **CommandOverrides** - Per-command role ID lists that override the default. If a command has an override, only users with one of the listed roles can use it.

To get a Discord role ID: enable Developer Mode in Discord (Settings → Advanced), then right-click a role and click "Copy Role ID".

## Configuration

Main config is stored in `BepInEx/config/io.vrising.DiscordRcon.cfg`.

### Discord

| Key | Default | Description |
|---|---|---|
| `Discord.BotToken` | *(empty)* | Discord bot token from the Developer Portal |
| `Discord.GuildId` | `0` | Discord server (guild) ID for registering slash commands |
| `Discord.CommandPrefix` | `!rcon` | Prefix for text commands in Discord |

### RCON

| Key | Default | Description |
|---|---|---|
| `RCON.Host` | `127.0.0.1` | RCON server hostname |
| `RCON.Port` | `25575` | RCON server port |
| `RCON.Password` | *(empty)* | RCON server password |
| `RCON.CommandTimeoutMs` | `10000` | Timeout in milliseconds for RCON command responses |

### Discovery

| Key | Default | Description |
|---|---|---|
| `Discovery.Enabled` | `true` | Enable dynamic slash command discovery from ScarletRCON help output |
| `Discovery.PollIntervalSeconds` | `300` | How often to re-sync commands from ScarletRCON (seconds) |

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

## Installation

1. Install [BepInEx](https://docs.bepinex.dev/) on your V Rising dedicated server.
2. Install [ScarletRCON](https://github.com/markvaaz/ScarletRCON) and configure it with your RCON password and port.
3. Place `DiscordRcon.dll` in your `BepInEx/plugins/` directory.
4. Create a Discord bot:
   - Go to [Discord Developer Portal](https://discord.com/developers/applications) and create a new application.
   - Go to the **Bot** tab, click **Add Bot**, and copy the token.
   - Under **Privileged Gateway Intents**, enable **Server Members Intent**.
   - Go to **OAuth2 → URL Generator**, select `bot` scope and `Send Messages` permission, then use the generated URL to invite the bot to your server.
5. Start the server once to generate the config files, then stop it.
6. Edit `BepInEx/config/io.vrising.DiscordRcon.cfg`:
   - Set `Discord.BotToken` to your bot token.
   - Set `Discord.GuildId` to your Discord server ID (enable Developer Mode in Discord, right-click your server icon, Copy Server ID).
   - Set `RCON.Password` to your ScarletRCON password.
7. Edit `BepInEx/config/DiscordRcon/discordrcon_roles.json` and add your Discord role IDs.
8. Restart the server.

The bot will connect to Discord, wait 60 seconds for ScarletRCON to initialize, then auto-discover and register all commands as slash commands.

## Attribution

This project is licensed under AGPL-3.0.

This project depends on the following libraries:

- [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus) - Licensed under MIT
- [ScarletRCON](https://github.com/markvaaz/ScarletRCON) - by markvaaz

This is an independent project with its own purpose and functionality. It is not a fork, modification, or derivative of any of the above projects.
