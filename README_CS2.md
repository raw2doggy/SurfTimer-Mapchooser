# SurfTimer MapChooser for CS2 with CounterStrikeSharp

This is a CounterStrikeSharp (CS2) port of the popular SurfTimer MapChooser plugins originally designed for SourceMod. This plugin collection provides map voting, nominations, rock-the-vote, and vote extend functionality for CS2 SurfTimer servers.

## Features

- **MapChooser**: Automated map voting at the end of rounds/time limits
- **Nominations**: Players can nominate maps for the next vote
- **Rock the Vote (RTV)**: Players can vote to start a map vote early
- **Vote Extend**: Players can vote to extend the current map

## Requirements

- **CS2 Dedicated Server**
- **CounterStrikeSharp v263+** (https://github.com/roflmuffin/CounterStrikeSharp)
- **SurfTimer for CS2** (when available)
- **MySQL Database** (MySQL 5.7+, MySQL 8+, or MariaDB)
- **.NET 8.0 Runtime**

## Installation

1. **Install CounterStrikeSharp** on your CS2 server following their installation guide

2. **Download the Plugin**:
   - Download the latest release or build from source
   - Extract to your CS2 server's `game/csgo/addons/counterstrikesharp/plugins/` directory

3. **Configure Database**:
   - Edit `SurfTimerMapchooser/config.json` with your database credentials
   - Ensure your SurfTimer database is set up with the required tables

4. **Configure Server**:
   - Edit the configuration files in the plugin directory
   - Set your server tier and other preferences

5. **Restart Server**:
   - Restart your CS2 server to load the plugin

## Configuration

### Main Config (`config.json`)
```json
{
  "DatabaseHost": "localhost",
  "DatabaseName": "surftimer",
  "DatabaseUser": "surftimer",
  "DatabasePassword": "password",
  "DatabasePort": 3306,
  "ServerTier": 0,
  "ServerTierMax": 0,
  "StartTime": 10,
  "IncludeMaps": 5,
  "ExcludeMaps": 3,
  "Extend": true,
  "MaxExtends": 2,
  "ExtendTimeStep": 15,
  "VoteDuration": 30,
  "PointsRequirement": 0,
  "RankRequirement": 0,
  "VipOverwriteRequirements": true,
  "ChatPrefix": "[MapChooser]"
}
```

### Server Tier Configuration
- `ServerTier`: Set to `0` for all tiers, or specific tier number (e.g., `1` for tier 1 only)
- `ServerTierMax`: For tier ranges (e.g., `ServerTier: 1, ServerTierMax: 3` for tiers 1-3)

## Commands

### Player Commands
- `!nominate [mapname]` or `!nm [mapname]` - Nominate a map
- `!rtv` - Rock the vote (start map vote early)
- `!ve` or `!voteextend` - Vote to extend current map
- `!nextmap` - Show the next map

### Admin Commands
- `css_nominate_addmap <mapname>` - Force add a map to nominations (requires CHANGEMAP flag)

## Differences from SourceMod Version

### Architecture Changes
- **Language**: Converted from SourcePawn to C#
- **Framework**: Uses CounterStrikeSharp instead of SourceMod
- **Configuration**: JSON-based configuration instead of ConVars
- **Menus**: Uses CounterStrikeSharp's ChatMenu system

### Database Integration
- Maintains compatibility with existing SurfTimer database schema
- Uses MySqlConnector for .NET instead of SourceMod's MySQL extension
- Async database operations for better performance

### Command Changes
- Commands now use `css_` prefix instead of `sm_`
- Chat commands maintain the same `!` prefix format

## Building from Source

1. **Prerequisites**:
   - .NET 8.0 SDK
   - Visual Studio or VS Code with C# extension

2. **Build**:
   ```bash
   dotnet build SurfTimerMapchooser.sln --configuration Release
   ```

3. **Output**:
   - Compiled plugin will be in `SurfTimerMapchooser/bin/Release/net8.0/`

## Plugin Structure

The plugin is organized into several components:

- `SurfTimerMapchooser.cs` - Main mapchooser functionality
- `Nominations.cs` - Map nomination system
- `RockTheVote.cs` - RTV functionality
- `VoteExtend.cs` - Vote extend functionality

## Integration with SurfTimer

This plugin is designed to work with SurfTimer for CS2. It requires:
- SurfTimer database tables (`ck_zones`, `ck_maptier`, `ck_playerrank`, etc.)
- SurfTimer's player ranking and points system
- Map tier information from the database

## Troubleshooting

### Common Issues

1. **Database Connection Failed**:
   - Check database credentials in `config.json`
   - Ensure MySQL server is running and accessible
   - Verify database contains SurfTimer tables

2. **Maps Not Loading**:
   - Ensure maps exist in your `mapcycle.txt`
   - Check that map names in database match actual map files
   - Verify server tier configuration

3. **Commands Not Working**:
   - Ensure CounterStrikeSharp is properly installed
   - Check that the plugin loaded without errors in server console
   - Verify player permissions

### Console Commands for Debugging
- Check if plugin loaded: Look for "SurfTimer MapChooser" in server startup logs
- Database errors will be printed to server console

## Contributing

This is a community port of the original SourceMod plugins. Contributions are welcome!

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly
5. Submit a pull request

## Credits

- **Original SourceMod Plugins**: AlliedModders LLC & SurfTimer Contributors
- **CS2 Port**: Community contributors
- **CounterStrikeSharp Framework**: roflmuffin and contributors

## License

This project maintains the same GPL v3 license as the original SourceMod plugins.