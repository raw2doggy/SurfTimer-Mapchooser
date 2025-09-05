using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using MySqlConnector;
using System.Text.Json;

namespace SurfTimerMapchooser;

public class NominationsPlugin : BasePlugin, IPluginConfig<NominationsConfig>
{
    public override string ModuleName => "SurfTimer Nominations";
    public override string ModuleVersion => "2.0.3"; 
    public override string ModuleAuthor => "AlliedModders LLC & SurfTimer Contributors";
    public override string ModuleDescription => "Provides Map Nominations for CS2";

    public NominationsConfig Config { get; set; } = new();
    
    private readonly List<string> _mapList = new();
    private readonly List<int> _mapTierList = new();
    private readonly Dictionary<string, int> _mapStatusList = new();
    private readonly List<string> _nominatedMaps = new();
    private readonly List<int> _nominatedBy = new();
    private string? _connectionString;

    public override void Load(bool hotReload)
    {
        LoadConfig();
        SetupDatabase();
        
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        
        AddCommand("css_nominate", "Nominate a map", OnNominateCommand);
        AddCommand("css_nm", "Nominate a map (alias)", OnNominateCommand);
        
        LoadMapList();
    }

    private void LoadConfig()
    {
        try
        {
            var configPath = Path.Combine(ModuleDirectory, "nominations_config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                Config = JsonSerializer.Deserialize<NominationsConfig>(json) ?? new NominationsConfig();
            }
            else
            {
                var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[SurfTimer Nominations] Error loading config: {ex.Message}");
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
            Server.PrintToConsole("[SurfTimer Nominations] Database connection configured");
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[SurfTimer Nominations] Database setup error: {ex.Message}");
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

            string query = Config.ServerTier > 0 ? 
                "SELECT ck_zones.mapname, tier FROM `ck_zones` INNER JOIN ck_maptier on ck_zones.mapname = ck_maptier.mapname WHERE tier >= @tierMin AND tier <= @tierMax GROUP BY mapname, tier ORDER BY tier ASC, mapname ASC" :
                "SELECT ck_zones.mapname, tier FROM `ck_zones` INNER JOIN ck_maptier on ck_zones.mapname = ck_maptier.mapname GROUP BY mapname, tier ORDER BY tier ASC, mapname ASC";

            using var command = new MySqlCommand(query, connection);
            
            if (Config.ServerTier > 0)
            {
                command.Parameters.AddWithValue("@tierMin", Config.ServerTier);
                command.Parameters.AddWithValue("@tierMax", Config.ServerTierMax > Config.ServerTier ? Config.ServerTierMax : Config.ServerTier);
            }

            using var reader = await command.ExecuteReaderAsync();
            
            _mapList.Clear();
            _mapTierList.Clear();
            _mapStatusList.Clear();
            
            while (await reader.ReadAsync())
            {
                var mapName = reader.GetString("mapname");
                var tier = reader.GetInt32("tier");
                
                if (Server.IsMapValid(mapName))
                {
                    _mapList.Add(mapName);
                    _mapTierList.Add(tier);
                    _mapStatusList[mapName] = 1; // MAPSTATUS_ENABLED
                }
            }
            
            Server.PrintToConsole($"[SurfTimer Nominations] Loaded {_mapList.Count} maps from database");
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[SurfTimer Nominations] Error loading map list: {ex.Message}");
        }
    }

    public void OnNominateCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;

        var args = commandInfo.GetCommandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (args.Length > 1)
        {
            var mapName = args[1];
            AttemptNominate(player, mapName);
        }
        else
        {
            ShowNominationMenu(player);
        }
    }

    private void ShowNominationMenu(CCSPlayerController player)
    {
        if (Config.TieredMenu)
        {
            ShowTieredNominationMenu(player);
        }
        else
        {
            ShowSimpleNominationMenu(player);
        }
    }

    private void ShowTieredNominationMenu(CCSPlayerController player)
    {
        var menu = new ChatMenu("Nominate Menu");
        
        var tiers = _mapTierList.Distinct().OrderBy(x => x).ToList();
        
        foreach (var tier in tiers)
        {
            var mapsInTier = _mapList.Where((map, index) => 
                _mapTierList[index] == tier && 
                IsMapAvailable(map)).Count();
                
            if (mapsInTier > 0)
            {
                menu.AddMenuOption($"Tier {tier} ({mapsInTier} maps)", (controller, option) =>
                {
                    ShowTierMaps(controller, tier);
                });
            }
        }
        
        MenuManager.OpenChatMenu(player, menu);
    }

    private void ShowTierMaps(CCSPlayerController player, int tier)
    {
        var menu = new ChatMenu($"Tier {tier} Maps");
        
        for (int i = 0; i < _mapList.Count; i++)
        {
            if (_mapTierList[i] == tier && IsMapAvailable(_mapList[i]))
            {
                var mapName = _mapList[i];
                menu.AddMenuOption(mapName, (controller, option) =>
                {
                    AttemptNominate(controller, mapName);
                });
            }
        }
        
        MenuManager.OpenChatMenu(player, menu);
    }

    private void ShowSimpleNominationMenu(CCSPlayerController player)
    {
        var menu = new ChatMenu("Nominate Menu");
        
        for (int i = 0; i < _mapList.Count; i++)
        {
            if (IsMapAvailable(_mapList[i]))
            {
                var mapName = _mapList[i];
                var tier = _mapTierList[i];
                
                menu.AddMenuOption($"Tier {tier} - {mapName}", (controller, option) =>
                {
                    AttemptNominate(controller, mapName);
                });
            }
        }
        
        MenuManager.OpenChatMenu(player, menu);
    }

    private bool IsMapAvailable(string mapName)
    {
        // Check if map is already nominated
        if (_nominatedMaps.Contains(mapName))
            return false;

        // Check if map is current map and exclude current is enabled
        if (Config.ExcludeCurrent && Server.MapName.Equals(mapName, StringComparison.OrdinalIgnoreCase))
            return false;

        // Check map status
        if (_mapStatusList.TryGetValue(mapName, out var status))
        {
            return (status & 1) != 0; // MAPSTATUS_ENABLED
        }

        return false;
    }

    private void AttemptNominate(CCSPlayerController player, string mapName)
    {
        if (!_mapList.Contains(mapName, StringComparer.OrdinalIgnoreCase))
        {
            player.PrintToChat($"{Config.ChatPrefix} Map '{mapName}' not found.");
            return;
        }

        if (_nominatedMaps.Contains(mapName))
        {
            player.PrintToChat($"{Config.ChatPrefix} Map '{mapName}' already nominated.");
            return;
        }

        if (!IsMapAvailable(mapName))
        {
            player.PrintToChat($"{Config.ChatPrefix} Map '{mapName}' is not available for nomination.");
            return;
        }

        var playerSlot = player.Slot;
        
        // Check if player already has a nomination
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

        // Check nomination limit
        if (_nominatedMaps.Count >= Config.MaxNominations)
        {
            player.PrintToChat($"{Config.ChatPrefix} The maximum allowed nominations has been reached.");
            return;
        }

        // Add new nomination
        _nominatedMaps.Add(mapName);
        _nominatedBy.Add(playerSlot);
        
        Server.PrintToChatAll($"{Config.ChatPrefix} {player.PlayerName} nominated {mapName}.");
    }

    private void OnMapStart(string mapName)
    {
        _nominatedMaps.Clear();
        _nominatedBy.Clear();
        LoadMapList();
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

    // Public API for mapchooser integration
    public List<string> GetNominatedMaps() => new(_nominatedMaps);
    public List<int> GetNominatedBy() => new(_nominatedBy);

    public void OnConfigParsed(NominationsConfig config)
    {
        Config = config;
    }
}

public class NominationsConfig : BasePluginConfig
{
    public string DatabaseHost { get; set; } = "localhost";
    public string DatabaseName { get; set; } = "surftimer";
    public string DatabaseUser { get; set; } = "surftimer";
    public string DatabasePassword { get; set; } = "password";
    public int DatabasePort { get; set; } = 3306;
    
    public int ServerTier { get; set; } = 0;
    public int ServerTierMax { get; set; } = 0;
    public bool ExcludeCurrent { get; set; } = true;
    public bool ExcludeOld { get; set; } = true;
    public bool TieredMenu { get; set; } = true;
    public int MaxNominations { get; set; } = 5;
    
    public string ChatPrefix { get; set; } = "[Nominations]";
}