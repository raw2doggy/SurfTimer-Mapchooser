# SurfTimer MapChooser for CS2 with CounterStrikeSharp

This is a CounterStrikeSharp (CS2) plugin that provides map voting, nominations, rock-the-vote, and map extension functionality for CS2 servers. **Now updated to work with SharpTimer!**

## ⚡ Quick Start

This repository contains a modern mapchooser plugin designed for **SharpTimer integration**.

## 🎯 SharpTimer Integration

### Features

- **MapChooser**: Automated map voting at the end of rounds/time limits
- **Nominations**: Players can nominate maps with `!nominate` or `!nm`
- **Rock the Vote (RTV)**: Players can vote to start a map vote early with `!rtv`
- **Map Extensions**: Option to extend the current map
- **SharpTimer Database Integration**: Loads maps from SharpTimer's database
- **Fallback System**: Uses maplist.txt if database unavailable

### Requirements

- **CS2 Dedicated Server**
- **CounterStrikeSharp v263+** ([Download](https://github.com/roflmuffin/CounterStrikeSharp))
- **SurfTimer for CS2** (when available)
- **MySQL Database** (MySQL 5.7+, MySQL 8+, or MariaDB)
- **.NET 8.0 Runtime**

### Installation

1. **Install CounterStrikeSharp** on your CS2 server
2. **Download/Build the Plugin**:
   ```bash
   # Clone this repository
   git clone https://github.com/raw2doggy/SurfTimer-Mapchooser.git
   cd SurfTimer-Mapchooser
   
   # Build the CS2 version
   dotnet build SurfTimerMapchooser.sln --configuration Release
   ```

3. **Install Plugin**:
   ```bash
   # Copy to your CS2 server
   cp -r SurfTimerMapchooser/bin/Release/* /path/to/cs2/game/csgo/addons/counterstrikesharp/plugins/SurfTimerMapchooser/
   ```

4. **Configure**:
   - Edit `config.json` with your database credentials
   - Set server tier preferences
   - Configure voting options

5. **Restart** your CS2 server

### Configuration Example

```json
{
  "DatabaseHost": "localhost",
  "DatabaseName": "surftimer",
  "DatabaseUser": "surftimer",
  "DatabasePassword": "password",
  "DatabasePort": 3306,
  "ServerTier": 1,
  "ServerTierMax": 3,
  "StartTime": 10,
  "IncludeMaps": 5,
  "Extend": true,
  "VoteDuration": 30,
  "ChatPrefix": "[MapChooser]"
}
```

### Commands (CS2 Version)

- `!nominate [mapname]` - Nominate a map
- `!rtv` - Rock the vote  
- `!ve` or `!voteextend` - Vote to extend current map
- `!nextmap` - Show next map

## 📁 SourceMod Version (Legacy)

The original SourceMod plugins are still available in the `addons/sourcemod/` directory for CS:GO servers.

### Requirements (SourceMod)
- [SurfTimer](https://github.com/surftimer/Surftimer-Official)
- SourceMod 1.11+
- MySQL Database

### Installation (SourceMod)
See the original README.md for SourceMod installation instructions.

## 🔧 Development

### Building from Source

```bash
# Prerequisites: .NET 8.0 SDK
dotnet build SurfTimerMapchooser.sln --configuration Release
```

### Project Structure

```
SurfTimerMapchooser/
├── SurfTimerMapchooser.cs    # Main mapchooser functionality
├── Nominations.cs            # Map nomination system  
├── RockTheVote.cs           # RTV functionality
├── VoteExtend.cs            # Vote extend functionality
├── config.json              # Configuration file
└── lang/                    # Language files
    └── en/
        └── mapchooser.json
```

## 🚀 Key Improvements in CS2 Version

### Technical Improvements
- **Async Database Operations**: Better performance with non-blocking MySQL queries
- **Modern C# Features**: Leverages .NET 8.0 capabilities
- **JSON Configuration**: More flexible and readable than ConVars
- **Modular Design**: Separate plugins for each feature
- **Type Safety**: Strong typing with C# vs SourcePawn

### Feature Parity
- ✅ Map voting with tier filtering
- ✅ Player-based nominations
- ✅ Rock the vote functionality
- ✅ Vote extend capability
- ✅ Database integration with SurfTimer
- ✅ Player requirements (points/rank)
- ✅ Tiered nomination menus

### Differences from SourceMod
- Commands use CSS-style `css_` prefix internally
- JSON configuration instead of ConVar-based config
- ChatMenu system instead of SourceMod panels
- Event-driven architecture with CounterStrikeSharp

## 🤝 Contributing

This is a community-driven port. Contributions welcome!

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test on a CS2 server
5. Submit a pull request

## 📄 License

Maintains GPL v3 license from original SourceMod plugins.

## 🙏 Credits

- **Original SourceMod Plugins**: AlliedModders LLC & SurfTimer Contributors  
- **CS2 Port**: Community contributors
- **CounterStrikeSharp**: roflmuffin and contributors
- **SurfTimer**: SurfTimer development team
