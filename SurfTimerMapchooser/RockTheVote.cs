using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;

namespace SurfTimerMapchooser;

public class RockTheVotePlugin : BasePlugin, IPluginConfig<RtvConfig>
{
    public override string ModuleName => "SurfTimer Rock The Vote";
    public override string ModuleVersion => "2.0.3";
    public override string ModuleAuthor => "AlliedModders LLC & SurfTimer Contributors";
    public override string ModuleDescription => "Provides RTV Map Voting for CS2";

    public RtvConfig Config { get; set; } = new();
    
    private readonly HashSet<int> _rtvVotes = new();
    private bool _rtvStarted = false;
    private bool _voteInProgress = false;

    public override void Load(bool hotReload)
    {
        LoadConfig();
        
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        
        AddCommand("css_rtv", "Rock the vote", OnRtvCommand);
        AddCommand("css_nominate", "Nominate a map", OnNominateCommand);
    }

    private void LoadConfig()
    {
        try
        {
            var configPath = Path.Combine(ModuleDirectory, "rtv_config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                Config = JsonSerializer.Deserialize<RtvConfig>(json) ?? new RtvConfig();
            }
            else
            {
                var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[SurfTimer RTV] Error loading config: {ex.Message}");
        }
    }

    public void OnRtvCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;

        if (!Config.Enabled)
        {
            player.PrintToChat($"{Config.ChatPrefix} Rock the Vote is currently disabled.");
            return;
        }

        if (_voteInProgress)
        {
            player.PrintToChat($"{Config.ChatPrefix} A vote is already in progress.");
            return;
        }

        var connectedPlayers = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot);
        if (connectedPlayers < Config.MinPlayers)
        {
            player.PrintToChat($"{Config.ChatPrefix} At least {Config.MinPlayers} players must be connected to start a vote.");
            return;
        }

        if (_rtvVotes.Contains(player.Slot))
        {
            player.PrintToChat($"{Config.ChatPrefix} You have already voted to rock the vote.");
            return;
        }

        _rtvVotes.Add(player.Slot);
        
        var votesNeeded = GetVotesNeeded();
        var currentVotes = _rtvVotes.Count;
        
        Server.PrintToChatAll($"{Config.ChatPrefix} {player.PlayerName} wants to rock the vote! ({currentVotes}/{votesNeeded} votes needed)");

        if (currentVotes >= votesNeeded)
        {
            StartRockTheVote();
        }
    }

    public void OnNominateCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid)
            return;

        // This would integrate with the main mapchooser plugin for nominations
        player.PrintToChat($"{Config.ChatPrefix} Use the main mapchooser plugin for nominations.");
    }

    private int GetVotesNeeded()
    {
        var connectedPlayers = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot);
        return Math.Max(1, (int)Math.Ceiling(connectedPlayers * Config.Percentage));
    }

    private void StartRockTheVote()
    {
        if (_rtvStarted)
            return;

        _rtvStarted = true;
        _voteInProgress = true;
        
        Server.PrintToChatAll($"{Config.ChatPrefix} Vote has started due to Rock the Vote!");
        
        // Here we would trigger the mapchooser vote
        // In a real implementation, this would interface with the main mapchooser plugin
        // For now, we'll just simulate starting a vote
        
        AddTimer(Config.DelayTime, () =>
        {
            // This would call the mapchooser's StartMapVote() method
            Server.PrintToChatAll($"{Config.ChatPrefix} Map vote would start here (integration with mapchooser needed)");
            _voteInProgress = false;
        });
    }

    private void OnMapStart(string mapName)
    {
        _rtvVotes.Clear();
        _rtvStarted = false;
        _voteInProgress = false;
    }

    private void OnClientDisconnect(int playerSlot)
    {
        _rtvVotes.Remove(playerSlot);
    }

    public void OnConfigParsed(RtvConfig config)
    {
        Config = config;
    }
}

public class RtvConfig : BasePluginConfig
{
    public bool Enabled { get; set; } = true;
    public double Percentage { get; set; } = 0.60;
    public int MinPlayers { get; set; } = 2;
    public int DelayTime { get; set; } = 5;
    public string ChatPrefix { get; set; } = "[RTV]";
}