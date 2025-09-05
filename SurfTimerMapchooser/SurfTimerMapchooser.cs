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
using System.Text.Json;

namespace SurfTimerMapchooser;

public class SurfTimerMapchooser : BasePlugin, IPluginConfig<MapchooserConfig>
{
    public override string ModuleName => "SurfTimer MapChooser";
    public override string ModuleVersion => "2.0.3";
    public override string ModuleAuthor => "AlliedModders LLC & SurfTimer Contributors";
    public override string ModuleDescription => "Automated Map Voting for CS2";

    public MapchooserConfig Config { get; set; } = new();
    
    private readonly List<string> _mapList = new();
    private readonly List<int> _mapTierList = new();
    private readonly List<string> _nominatedMaps = new();
    private readonly List<int> _nominatedBy = new();
    private readonly List<string> _excludedMaps = new();
    
    private bool _voteInProgress = false;
    private bool _hasVoteStarted = false;
    private bool _mapVoteCompleted = false;
    private int _extends = 0;
    private string? _connectionString;
    
    private CounterStrikeSharp.API.Modules.Timers.Timer? _voteTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _retryTimer;
    
    // Map vote menu
    private ChatMenu? _voteMenu;
    
    public override void Load(bool hotReload)
    {
        LoadConfig();
        SetupDatabase();
        
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        
        AddCommand("css_nominate", "Nominate a map", OnNominateCommand);
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
            return;

        try
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            string query;
            if (Config.ServerTier > 0)
            {
                // Load specific tier or tier range
                if (Config.ServerTierMax > Config.ServerTier)
                {
                    query = "SELECT ck_zones.mapname, tier, count(ck_zones.zonetype = 3), bonus FROM `ck_zones` " +
                           "INNER JOIN ck_maptier on ck_zones.mapname = ck_maptier.mapname " +
                           "LEFT JOIN ( SELECT mapname as map_2, MAX(ck_zones.zonegroup) as bonus FROM ck_zones GROUP BY mapname ) as a on ck_zones.mapname = a.map_2 " +
                           "WHERE (zonegroup = 0 AND zonetype = 1 or zonetype = 3 or zonetype = 5) AND tier >= @tierMin AND tier <= @tierMax " +
                           "GROUP BY mapname, tier, bonus ORDER BY mapname ASC";
                }
                else
                {
                    query = "SELECT ck_zones.mapname, tier, count(ck_zones.zonetype = 3), bonus FROM `ck_zones` " +
                           "INNER JOIN ck_maptier on ck_zones.mapname = ck_maptier.mapname " +
                           "LEFT JOIN ( SELECT mapname as map_2, MAX(ck_zones.zonegroup) as bonus FROM ck_zones GROUP BY mapname ) as a on ck_zones.mapname = a.map_2 " +
                           "WHERE (zonegroup = 0 AND zonetype = 1 or zonetype = 3 or zonetype = 5) AND tier = @tier " +
                           "GROUP BY mapname, tier, bonus ORDER BY mapname ASC";
                }
            }
            else
            {
                // Load all tiers
                query = "SELECT ck_zones.mapname, tier, count(ck_zones.zonetype = 3), bonus FROM `ck_zones` " +
                       "INNER JOIN ck_maptier on ck_zones.mapname = ck_maptier.mapname " +
                       "LEFT JOIN ( SELECT mapname as map_2, MAX(ck_zones.zonegroup) as bonus FROM ck_zones GROUP BY mapname ) as a on ck_zones.mapname = a.map_2 " +
                       "WHERE (zonegroup = 0 AND zonetype = 1 or zonetype = 3 or zonetype = 5) " +
                       "GROUP BY mapname, tier, bonus ORDER BY mapname ASC";
            }

            using var command = new MySqlCommand(query, connection);
            
            if (Config.ServerTier > 0)
            {
                if (Config.ServerTierMax > Config.ServerTier)
                {
                    command.Parameters.AddWithValue("@tierMin", Config.ServerTier);
                    command.Parameters.AddWithValue("@tierMax", Config.ServerTierMax);
                }
                else
                {
                    command.Parameters.AddWithValue("@tier", Config.ServerTier);
                }
            }

            using var reader = await command.ExecuteReaderAsync();
            
            _mapList.Clear();
            _mapTierList.Clear();
            
            while (await reader.ReadAsync())
            {
                var mapName = reader.GetString("mapname");
                var tier = reader.GetInt32("tier");
                
                // Check if map exists in mapcycle or on server
                if (Server.IsMapValid(mapName))
                {
                    _mapList.Add(mapName);
                    _mapTierList.Add(tier);
                }
            }
            
            Server.PrintToConsole($"[SurfTimer MapChooser] Loaded {_mapList.Count} maps from database");
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[SurfTimer MapChooser] Error loading map list: {ex.Message}");
        }
    }

    [GameEventHandler]
    public HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        // Check player requirements async
        _ = Task.Run(async () => await CheckPlayerRequirements(player));
        
        return HookResult.Continue;
    }

    private async Task CheckPlayerRequirements(CCSPlayerController player)
    {
        if (string.IsNullOrEmpty(_connectionString) || !player.IsValid)
            return;

        try
        {
            var steamId = player.SteamID.ToString();
            
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            // Check player points
            if (Config.PointsRequirement > 0)
            {
                var pointsQuery = "SELECT points FROM ck_playerrank WHERE steamid = @steamid AND style = 0";
                using var pointsCommand = new MySqlCommand(pointsQuery, connection);
                pointsCommand.Parameters.AddWithValue("@steamid", steamId);
                
                var pointsResult = await pointsCommand.ExecuteScalarAsync();
                var points = pointsResult != null ? Convert.ToInt32(pointsResult) : 0;
                
                // Store player points check result (you'd implement this based on your needs)
            }

            // Check player rank
            if (Config.RankRequirement > 0)
            {
                var rankQuery = "SELECT COUNT(*) FROM ck_playerrank WHERE style = 0 AND points >= (SELECT points FROM ck_playerrank WHERE steamid = @steamid AND style = 0)";
                using var rankCommand = new MySqlCommand(rankQuery, connection);
                rankCommand.Parameters.AddWithValue("@steamid", steamId);
                
                var rankResult = await rankCommand.ExecuteScalarAsync();
                var rank = rankResult != null ? Convert.ToInt32(rankResult) : 0;
                
                // Store player rank check result (you'd implement this based on your needs)
            }
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[SurfTimer MapChooser] Error checking player requirements: {ex.Message}");
        }
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
                
            var index = _mapList.IndexOf(map);
            var tier = index >= 0 && index < _mapTierList.Count ? _mapTierList[index] : 0;
            
            menu.AddMenuOption($"Tier {tier} - {map}", (controller, option) =>
            {
                NominateMap(controller, map);
            });
        }
        
        MenuManager.OpenChatMenu(player, menu);
    }

    private void NominateMap(CCSPlayerController player, string mapName)
    {
        if (!_mapList.Contains(mapName))
        {
            player.PrintToChat($"{Config.ChatPrefix} Map '{mapName}' not found.");
            return;
        }

        if (_nominatedMaps.Contains(mapName))
        {
            player.PrintToChat($"{Config.ChatPrefix} Map '{mapName}' already nominated.");
            return;
        }

        if (_excludedMaps.Contains(mapName))
        {
            player.PrintToChat($"{Config.ChatPrefix} Map '{mapName}' is excluded from nominations.");
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
                _nominatedMaps[existingIndex] = mapName;
                
                Server.PrintToChatAll($"{Config.ChatPrefix} {player.PlayerName} changed their nomination from {oldMap} to {mapName}.");
                return;
            }
        }

        // Add new nomination
        _nominatedMaps.Add(mapName);
        _nominatedBy.Add(playerSlot);
        
        Server.PrintToChatAll($"{Config.ChatPrefix} {player.PlayerName} nominated {mapName}.");
    }

    public void OnRtvCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid)
            return;

        // RTV functionality would be implemented here
        player.PrintToChat($"{Config.ChatPrefix} Rock the Vote functionality coming soon!");
    }

    public void OnNextMapCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid)
            return;

        var nextMap = "";
        // In CounterStrikeSharp, we'd need to access this differently
        // For now, return a placeholder
        if (string.IsNullOrEmpty(nextMap))
            nextMap = "Not set";
            
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
        _retryTimer?.Kill();
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
        
        Server.PrintToChatAll($"{Config.ChatPrefix} Map vote started! Type !vote to participate.");
    }

    private void CreateVoteMenu()
    {
        _voteMenu = new ChatMenu("Map Vote");
        
        // Add nominated maps first
        foreach (var map in _nominatedMaps.Take(Config.IncludeMaps))
        {
            _voteMenu.AddMenuOption(map, (player, option) =>
            {
                VoteForMap(player, map);
            });
        }
        
        // Fill remaining slots with random maps
        var remainingSlots = Config.IncludeMaps - _nominatedMaps.Count;
        if (remainingSlots > 0)
        {
            var availableMaps = _mapList.Where(m => !_nominatedMaps.Contains(m) && !_excludedMaps.Contains(m)).ToList();
            var random = new Random();
            
            for (int i = 0; i < remainingSlots && availableMaps.Any(); i++)
            {
                var randomIndex = random.Next(availableMaps.Count);
                var randomMap = availableMaps[randomIndex];
                availableMaps.RemoveAt(randomIndex);
                
                _voteMenu.AddMenuOption(randomMap, (player, option) =>
                {
                    VoteForMap(player, randomMap);
                });
            }
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
    public string DatabaseName { get; set; } = "surftimer";
    public string DatabaseUser { get; set; } = "surftimer";
    public string DatabasePassword { get; set; } = "password";
    public int DatabasePort { get; set; } = 3306;
    
    public int ServerTier { get; set; } = 0;
    public int ServerTierMax { get; set; } = 0;
    public int StartTime { get; set; } = 10;
    public int IncludeMaps { get; set; } = 5;
    public int ExcludeMaps { get; set; } = 3;
    public bool Extend { get; set; } = true;
    public int MaxExtends { get; set; } = 2;
    public int ExtendTimeStep { get; set; } = 15;
    public int VoteDuration { get; set; } = 30;
    public int PointsRequirement { get; set; } = 0;
    public int RankRequirement { get; set; } = 0;
    public bool VipOverwriteRequirements { get; set; } = true;
    
    public string ChatPrefix { get; set; } = "[MapChooser]";
}