# SurfTimer MapChooser for SharpTimer

A modern CounterStrikeSharp plugin that provides automated map voting functionality designed to work with SharpTimer.

## Features

- **Map Nominations**: Players can nominate maps using `!nominate` or `!nm`
- **Rock the Vote (RTV)**: Players can start a vote using `!rtv`
- **Automatic End-of-Map Voting**: Votes start automatically near the end of maps
- **Map Extension**: Option to extend the current map
- **SharpTimer Integration**: Loads available maps from SharpTimer's database
- **Fallback System**: Uses maplist.txt file if database is unavailable

## Installation

1. Place the `SurfTimerMapchooser.dll` in your `addons/counterstrikesharp/plugins/` directory
2. Configure the `config.json` file with your database settings
3. Optionally create a `maplist.txt` file for fallback map listing
4. Restart your server or use `css_plugins reload`

## Configuration

The plugin creates a `config.json` file with the following options:

```json
{
  "DatabaseHost": "localhost",
  "DatabaseName": "database",
  "DatabaseUser": "user", 
  "DatabasePassword": "password",
  "DatabasePort": 3306,
  "StartTime": 10,
  "IncludeMaps": 5,
  "ExcludeMaps": 3,
  "Extend": true,
  "MaxExtends": 2,
  "ExtendTimeStep": 15,
  "VoteDuration": 30,
  "MinPlayersForRTV": 2,
  "ChatPrefix": "[MapChooser]"
}
```

### Configuration Options

- `DatabaseHost`: MySQL server hostname
- `DatabaseName`: Database name (should match your SharpTimer database)
- `DatabaseUser`: Database username
- `DatabasePassword`: Database password
- `DatabasePort`: Database port (default: 3306)
- `StartTime`: How many minutes before map end to start vote
- `IncludeMaps`: Number of maps to include in vote
- `ExcludeMaps`: Number of recent maps to exclude
- `Extend`: Allow map extension option
- `MaxExtends`: Maximum number of extends per map
- `ExtendTimeStep`: Minutes to extend map by
- `VoteDuration`: How long the vote lasts (seconds)
- `MinPlayersForRTV`: Minimum players needed for RTV
- `ChatPrefix`: Chat prefix for plugin messages

## Commands

- `!nominate <mapname>` or `!nm <mapname>` - Nominate a map
- `!nominate` or `!nm` - Open nomination menu
- `!rtv` - Rock the vote (start a map vote)
- `!nextmap` - Show the next map

## SharpTimer Integration

This plugin is designed to work with SharpTimer and will:

1. **Load maps from database**: Queries the `PlayerRecords` table to find available maps
2. **Fallback to file**: Uses `maplist.txt` if database is unavailable
3. **Filter bonus maps**: Excludes maps with "bonus" in the name
4. **Validate maps**: Only includes maps that exist on the server

## Differences from Original SurfTimer Plugin

- **No tier filtering**: Removed SurfTimer-specific tier system
- **Simplified database queries**: Uses SharpTimer's simpler database structure
- **Better fallback system**: Graceful degradation to file-based map listing
- **Modern C# patterns**: Updated to use Dapper for database access
- **Cleaner configuration**: Removed SurfTimer-specific options

## Requirements

- CounterStrikeSharp 1.0.305+
- SharpTimer plugin installed and configured
- MySQL database (optional, falls back to file-based system)

## Troubleshooting

### Maps not loading
1. Check your database configuration
2. Ensure SharpTimer is installed and has records
3. Verify `maplist.txt` exists and contains valid maps
4. Check console for error messages

### Vote not starting
1. Ensure map has a time limit set (`mp_timelimit`)
2. Check if enough time remains (see `StartTime` config)
3. Verify no vote is already in progress

### RTV not working
1. Ensure minimum players are connected (see `MinPlayersForRTV`)
2. Check if a vote has already completed for the current map

## Support

This plugin is designed to work specifically with SharpTimer. For issues:
1. Check the console for error messages
2. Verify your SharpTimer installation is working
3. Ensure database connectivity if using database mode