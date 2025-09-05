using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using MySqlConnector;
using Dapper;
using System.Text.Json;

namespace SurfTimerMapchooser;

public class SurfTimerMapchooser : BasePlugin, IPluginConfig<MapchooserConfig>
{
    public override string ModuleName => "SurfTimer MapChooser";
    public override string ModuleVersion => "3.0.0";
    public override string ModuleAuthor => "AlliedModders LLC & SurfTimer Contributors & SharpTimer Contributors";
    public override string ModuleDescription => "Automated Map Voting for CS2 with SharpTimer Integration";

    public MapchooserConfig Config { get; set; } = new();
    
    private readonly List<string> _mapList = new();
    private readonly List<string> _nominatedMaps = new();
    private readonly List<int> _nominatedBy = new();
    private readonly List<string> _excludedMaps = new();
    
    private bool _voteInProgress = false;
    private bool _hasVoteStarted = false;
    private bool _mapVoteCompleted = false;
    private int _extends = 0;
    private string? _connectionString;
    
    private CounterStrikeSharp.API.Modules.Timers.Timer? _voteTimer;
    
    // Map vote menu
    private ChatMenu? _voteMenu;

    public override void Load(bool hotReload)
    {
        LoadConfig();
        SetupDatabase();
        
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        
        AddCommand("css_nominate", "Nominate a map", OnNominateCommand);
        AddCommand("css_nm", "Nominate a map (alias)", OnNominateCommand);
        AddCommand("css_rtv", "Rock the vote", OnRtvCommand);
        AddCommand("css_nextmap", "Show next map", OnNextMapCommand);
        
        AddTimer(5.0f, CheckMapVoteStart, TimerFlags.REPEAT);
        
        LoadMapList();
    }

    private void LoadConfig()
    {
        try
        {
            var configPath = Path.Combine(ModuleDirectory, "config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                Config = JsonSerializer.Deserialize<MapchooserConfig>(json) ?? new MapchooserConfig();
            }
            else
            {
                // Create default config
                var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[SurfTimer MapChooser] Error loading config: {ex.Message}");
        }
    }

    private void SetupDatabase()
    {
        try
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = Config.DatabaseHost,
                Database = Config.DatabaseName,
                UserID = Config.DatabaseUser,
                Password = Config.DatabasePassword,
                Port = (uint)Config.DatabasePort,
                Pooling = true,
                MinimumPoolSize = 0,
                MaximumPoolSize = 640,
                ConnectionIdleTimeout = 30
            };
            
            _connectionString = builder.ConnectionString;
            Server.PrintToConsole("[SurfTimer MapChooser] Database connection configured");
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[SurfTimer MapChooser] Database setup error: {ex.Message}");
        }
    }

    private async void LoadMapList()
    {
        if (string.IsNullOrEmpty(_connectionString))
        {
            LoadMapListFromFile();
            return;
        }

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Query SharpTimer's PlayerRecords table to get available maps
            string query = @"
                SELECT DISTINCT MapName 
                FROM PlayerRecords 
                WHERE MapName NOT LIKE '%bonus%' 
                ORDER BY MapName ASC";

            var maps = await connection.QueryAsync<string>(query);
            
            _mapList.Clear();
            
            foreach (var mapName in maps)
            {
                // Check if map exists in mapcycle or on server
                if (Server.IsMapValid(mapName))
                {
                    _mapList.Add(mapName);
                }
            }
            
            // If no maps found in database, fall back to file-based approach
            if (_mapList.Count == 0)
            {
                LoadMapListFromFile();
            }
            else
            {
                Server.PrintToConsole($"[SurfTimer MapChooser] Loaded {_mapList.Count} maps from SharpTimer database");
            }
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[SurfTimer MapChooser] Error loading map list from database: {ex.Message}");
            LoadMapListFromFile();
        }
    }

    private void LoadMapListFromFile()
    {
        try
        {
            var mapListPath = Path.Combine(ModuleDirectory, "maplist.txt");
            if (File.Exists(mapListPath))
            {
                var maps = File.ReadAllLines(mapListPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("//"))
                    .Select(line => line.Trim())
                    .Where(Server.IsMapValid);

                _mapList.AddRange(maps);
                Server.PrintToConsole($"[SurfTimer MapChooser] Loaded {_mapList.Count} maps from file");
            }
            else
            {
                // Create default maplist file
                var defaultMaps = new[] { "surf_beginner", "surf_kitsune", "surf_ski_2" };
                File.WriteAllLines(mapListPath, defaultMaps);
                _mapList.AddRange(defaultMaps.Where(Server.IsMapValid));
                Server.PrintToConsole("[SurfTimer MapChooser] Created default maplist.txt");
            }
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[SurfTimer MapChooser] Error loading maplist from file: {ex.Message}");
        }
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        return HookResult.Continue;
    }

    public void OnNominateCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid)
            return;

        var args = commandInfo.GetCommandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (args.Length > 1)
        {
            var mapName = args[1];
            NominateMap(player, mapName);
        }
        else
        {
            ShowNominationMenu(player);
        }
    }

    private void ShowNominationMenu(CCSPlayerController player)
    {
        var menu = new ChatMenu("Map Nominations");
        
        foreach (var map in _mapList)
        {
            if (_nominatedMaps.Contains(map) || _excludedMaps.Contains(map))
                continue;
                
            menu.AddMenuOption(map, (controller, option) =>
            {
                NominateMap(controller, map);
            });
        }
        
        MenuManager.OpenChatMenu(player, menu);
    }

    private void NominateMap(CCSPlayerController player, string mapName)
    {
        // Find map with partial matching
        var matchedMap = _mapList.FirstOrDefault(m => 
            m.Equals(mapName, StringComparison.OrdinalIgnoreCase) ||
            m.Contains(mapName, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrEmpty(matchedMap))
        {
            player.PrintToChat($"{Config.ChatPrefix} Map '{mapName}' not found.");
            return;
        }

        if (_nominatedMaps.Contains(matchedMap))
        {
            player.PrintToChat($"{Config.ChatPrefix} Map '{matchedMap}' already nominated.");
            return;
        }

        if (_excludedMaps.Contains(matchedMap))
        {
            player.PrintToChat($"{Config.ChatPrefix} Map '{matchedMap}' is excluded from nominations.");
            return;
        }

        // Check if player already has a nomination
        var playerSlot = player.Slot;
        if (_nominatedBy.Contains(playerSlot))
        {
            var existingIndex = _nominatedBy.IndexOf(playerSlot);
            if (existingIndex >= 0 && existingIndex < _nominatedMaps.Count)
            {
                var oldMap = _nominatedMaps[existingIndex];
                _nominatedMaps[existingIndex] = matchedMap;
                
                Server.PrintToChatAll($"{Config.ChatPrefix} {player.PlayerName} changed their nomination from {oldMap} to {matchedMap}.");
                return;
            }
        }

        // Add new nomination
        _nominatedMaps.Add(matchedMap);
        _nominatedBy.Add(playerSlot);
        
        Server.PrintToChatAll($"{Config.ChatPrefix} {player.PlayerName} nominated {matchedMap}.");
    }

    public void OnRtvCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid)
            return;

        if (_voteInProgress)
        {
            player.PrintToChat($"{Config.ChatPrefix} A vote is already in progress.");
            return;
        }

        if (_mapVoteCompleted)
        {
            player.PrintToChat($"{Config.ChatPrefix} Map vote has already completed for this map.");
            return;
        }

        var connectedPlayers = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot);
        if (connectedPlayers < Config.MinPlayersForRTV)
        {
            player.PrintToChat($"{Config.ChatPrefix} At least {Config.MinPlayersForRTV} players needed for RTV.");
            return;
        }

        // For simplicity, start the vote immediately
        // In a more complete implementation, you'd track RTV votes
        StartMapVote();
    }

    public void OnNextMapCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid)
            return;

        var nextMap = "Not set";
        player.PrintToChat($"{Config.ChatPrefix} Next map: {nextMap}");
    }

    private void OnMapStart(string mapName)
    {
        _hasVoteStarted = false;
        _mapVoteCompleted = false;
        _voteInProgress = false;
        _extends = 0;
        
        _nominatedMaps.Clear();
        _nominatedBy.Clear();
        
        // Add current map to exclusion list
        _excludedMaps.Clear();
        _excludedMaps.Add(mapName);
        
        LoadMapList();
    }

    private void OnMapEnd()
    {
        _voteTimer?.Kill();
    }

    private void OnClientDisconnect(int playerSlot)
    {
        // Remove nominations from disconnected player
        for (int i = _nominatedBy.Count - 1; i >= 0; i--)
        {
            if (_nominatedBy[i] == playerSlot)
            {
                var removedMap = _nominatedMaps[i];
                _nominatedMaps.RemoveAt(i);
                _nominatedBy.RemoveAt(i);
                
                Server.PrintToChatAll($"{Config.ChatPrefix} Nomination for {removedMap} removed (player disconnected).");
                break;
            }
        }
    }

    private void CheckMapVoteStart()
    {
        if (_hasVoteStarted || _mapVoteCompleted || _voteInProgress)
            return;

        // Check if conditions are met to start vote
        var currentTime = Server.CurrentTime;
        var mapTimeLimit = ConVar.Find("mp_timelimit")?.GetPrimitiveValue<float>() ?? 0;
        
        if (mapTimeLimit > 0)
        {
            var timeLeft = mapTimeLimit * 60 - currentTime;
            if (timeLeft <= Config.StartTime * 60)
            {
                StartMapVote();
            }
        }
    }

    private void StartMapVote()
    {
        if (_hasVoteStarted)
            return;

        _hasVoteStarted = true;
        _voteInProgress = true;
        
        CreateVoteMenu();
        ShowVoteToAllPlayers();
        
        _voteTimer = AddTimer(Config.VoteDuration, () =>
        {
            EndVote();
        });
        
        Server.PrintToChatAll($"{Config.ChatPrefix} Map vote started! Use the menu to vote.");
    }

    private void CreateVoteMenu()
    {
        _voteMenu = new ChatMenu("Map Vote");
        
        var mapsToInclude = new List<string>();
        
        // Add nominated maps first
        mapsToInclude.AddRange(_nominatedMaps.Take(Config.IncludeMaps));
        
        // Fill remaining slots with random maps
        var remainingSlots = Config.IncludeMaps - mapsToInclude.Count;
        if (remainingSlots > 0)
        {
            var availableMaps = _mapList.Where(m => !mapsToInclude.Contains(m) && !_excludedMaps.Contains(m)).ToList();
            var random = new Random();
            
            for (int i = 0; i < remainingSlots && availableMaps.Any(); i++)
            {
                var randomIndex = random.Next(availableMaps.Count);
                var randomMap = availableMaps[randomIndex];
                availableMaps.RemoveAt(randomIndex);
                mapsToInclude.Add(randomMap);
            }
        }
        
        // Add maps to menu
        foreach (var map in mapsToInclude)
        {
            _voteMenu.AddMenuOption(map, (player, option) =>
            {
                VoteForMap(player, map);
            });
        }
        
        // Add extend option if enabled
        if (Config.Extend && _extends < Config.MaxExtends)
        {
            _voteMenu.AddMenuOption("Extend Current Map", (player, option) =>
            {
                VoteForMap(player, "##extend##");
            });
        }
    }

    private void ShowVoteToAllPlayers()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot);
        foreach (var player in players)
        {
            if (_voteMenu != null)
                MenuManager.OpenChatMenu(player, _voteMenu);
        }
    }

    private readonly Dictionary<int, string> _playerVotes = new();

    private void VoteForMap(CCSPlayerController player, string mapName)
    {
        _playerVotes[player.Slot] = mapName;
        player.PrintToChat($"{Config.ChatPrefix} You voted for: {(mapName == "##extend##" ? "Extend Current Map" : mapName)}");
    }

    private void EndVote()
    {
        _voteInProgress = false;
        
        if (!_playerVotes.Any())
        {
            Server.PrintToChatAll($"{Config.ChatPrefix} No votes received. Map will not change.");
            return;
        }
        
        // Count votes
        var voteCounts = new Dictionary<string, int>();
        foreach (var vote in _playerVotes.Values)
        {
            if (voteCounts.ContainsKey(vote))
                voteCounts[vote]++;
            else
                voteCounts[vote] = 1;
        }
        
        // Find winner
        var winner = voteCounts.OrderByDescending(x => x.Value).First();
        var winnerMap = winner.Key;
        var winnerVotes = winner.Value;
        
        Server.PrintToChatAll($"{Config.ChatPrefix} Vote ended! Winner: {(winnerMap == "##extend##" ? "Extend Current Map" : winnerMap)} ({winnerVotes} votes)");
        
        if (winnerMap == "##extend##")
        {
            ExtendMap();
        }
        else
        {
            ChangeToMap(winnerMap);
        }
        
        _mapVoteCompleted = true;
        _playerVotes.Clear();
    }

    private void ExtendMap()
    {
        _extends++;
        var extendTime = Config.ExtendTimeStep;
        
        var timeLimitCvar = ConVar.Find("mp_timelimit");
        if (timeLimitCvar != null)
        {
            var currentTimeLimit = timeLimitCvar.GetPrimitiveValue<float>();
            timeLimitCvar.SetValue(currentTimeLimit + extendTime);
        }
        
        Server.PrintToChatAll($"{Config.ChatPrefix} Map extended by {extendTime} minutes!");
    }

    private void ChangeToMap(string mapName)
    {
        Server.PrintToChatAll($"{Config.ChatPrefix} Changing to {mapName} in 5 seconds...");
        
        AddTimer(5.0f, () =>
        {
            Server.ExecuteCommand($"changelevel {mapName}");
        });
    }

    public void OnConfigParsed(MapchooserConfig config)
    {
        Config = config;
    }
}

public class MapchooserConfig : BasePluginConfig
{
    public string DatabaseHost { get; set; } = "localhost";
    public string DatabaseName { get; set; } = "database";
    public string DatabaseUser { get; set; } = "user";
    public string DatabasePassword { get; set; } = "password";
    public int DatabasePort { get; set; } = 3306;
    
    public int StartTime { get; set; } = 10;
    public int IncludeMaps { get; set; } = 5;
    public int ExcludeMaps { get; set; } = 3;
    public bool Extend { get; set; } = true;
    public int MaxExtends { get; set; } = 2;
    public int ExtendTimeStep { get; set; } = 15;
    public int VoteDuration { get; set; } = 30;
    public int MinPlayersForRTV { get; set; } = 2;
    
    public string ChatPrefix { get; set; } = "[MapChooser]";
}