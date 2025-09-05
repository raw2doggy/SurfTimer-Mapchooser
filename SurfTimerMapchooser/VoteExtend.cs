using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;

namespace SurfTimerMapchooser;

public class VoteExtendPlugin : BasePlugin, IPluginConfig<VoteExtendConfig>
{
    public override string ModuleName => "SurfTimer Vote Extend";
    public override string ModuleVersion => "2.0.3";
    public override string ModuleAuthor => "AlliedModders LLC & SurfTimer Contributors";  
    public override string ModuleDescription => "Provides Vote Extend functionality for CS2";

    public VoteExtendConfig Config { get; set; } = new();
    
    private readonly HashSet<int> _extendVotes = new();
    private bool _extendVoteActive = false;
    private bool _hasExtended = false;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _extendVoteTimer;
    private ChatMenu? _extendVoteMenu;

    public override void Load(bool hotReload)
    {
        LoadConfig();
        
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
        
        AddCommand("css_ve", "Vote to extend current map", OnVoteExtendCommand);
        AddCommand("css_voteextend", "Vote to extend current map", OnVoteExtendCommand);
        AddCommand("css_extend", "Vote to extend current map", OnVoteExtendCommand);
    }

    private void LoadConfig()
    {
        try
        {
            var configPath = Path.Combine(ModuleDirectory, "voteextend_config.json");
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                Config = JsonSerializer.Deserialize<VoteExtendConfig>(json) ?? new VoteExtendConfig();
            }
            else
            {
                var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
        }
        catch (Exception ex)
        {
            Server.PrintToConsole($"[SurfTimer VoteExtend] Error loading config: {ex.Message}");
        }
    }

    public void OnVoteExtendCommand(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (player == null || !player.IsValid || player.IsBot)
            return;

        if (!Config.Enabled)
        {
            player.PrintToChat($"{Config.ChatPrefix} Vote extend is currently disabled.");
            return;
        }

        if (_hasExtended)
        {
            player.PrintToChat($"{Config.ChatPrefix} The map has already been extended.");
            return;
        }

        if (_extendVoteActive)
        {
            // Player wants to vote in active extend vote
            VoteForExtend(player);
            return;
        }

        var connectedPlayers = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot);
        if (connectedPlayers < Config.MinPlayers)
        {
            player.PrintToChat($"{Config.ChatPrefix} At least {Config.MinPlayers} players must be connected to start an extend vote.");
            return;
        }

        // Check if we're close enough to map end to allow extend vote
        if (!IsExtendVoteAllowed())
        {
            player.PrintToChat($"{Config.ChatPrefix} Extend vote can only be started near the end of the map.");
            return;
        }

        StartExtendVote(player);
    }

    private bool IsExtendVoteAllowed()
    {
        var timeLimitCvar = ConVar.Find("mp_timelimit");
        if (timeLimitCvar == null)
            return false;

        var timeLimit = timeLimitCvar.GetPrimitiveValue<float>();
        if (timeLimit <= 0)
            return false;

        var currentTime = Server.CurrentTime;
        var timeRemaining = (timeLimit * 60) - currentTime;
        
        return timeRemaining <= (Config.AllowTimeRemaining * 60);
    }

    private void StartExtendVote(CCSPlayerController initiator)
    {
        if (_extendVoteActive)
            return;

        _extendVoteActive = true;
        _extendVotes.Clear();
        
        // Add initiator's vote
        _extendVotes.Add(initiator.Slot);
        
        var votesNeeded = GetVotesNeeded();
        var currentVotes = _extendVotes.Count;
        
        Server.PrintToChatAll($"{Config.ChatPrefix} {initiator.PlayerName} started a vote to extend the map! ({currentVotes}/{votesNeeded} votes needed)");
        
        CreateExtendVoteMenu();
        ShowExtendVoteToAll();
        
        _extendVoteTimer = AddTimer(Config.VoteDuration, () =>
        {
            EndExtendVote();
        });
        
        // Check if we already have enough votes
        if (currentVotes >= votesNeeded)
        {
            ExtendMap();
        }
    }

    private void CreateExtendVoteMenu()
    {
        _extendVoteMenu = new ChatMenu("Extend Current Map?");
        
        _extendVoteMenu.AddMenuOption("Yes - Extend", (player, option) =>
        {
            VoteForExtend(player);
        });
        
        _extendVoteMenu.AddMenuOption("No - Don't Extend", (player, option) =>
        {
            player.PrintToChat($"{Config.ChatPrefix} You voted against extending the map.");
        });
    }

    private void ShowExtendVoteToAll()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot);
        foreach (var player in players)
        {
            if (_extendVoteMenu != null)
                MenuManager.OpenChatMenu(player, _extendVoteMenu);
        }
    }

    private void VoteForExtend(CCSPlayerController player)
    {
        if (!_extendVoteActive)
        {
            player.PrintToChat($"{Config.ChatPrefix} No extend vote is currently active.");
            return;
        }

        if (_extendVotes.Contains(player.Slot))
        {
            player.PrintToChat($"{Config.ChatPrefix} You have already voted to extend the map.");
            return;
        }

        _extendVotes.Add(player.Slot);
        
        var votesNeeded = GetVotesNeeded();
        var currentVotes = _extendVotes.Count;
        
        Server.PrintToChatAll($"{Config.ChatPrefix} {player.PlayerName} voted to extend! ({currentVotes}/{votesNeeded} votes needed)");

        if (currentVotes >= votesNeeded)
        {
            ExtendMap();
        }
    }

    private int GetVotesNeeded()
    {
        var connectedPlayers = Utilities.GetPlayers().Count(p => p.IsValid && !p.IsBot);
        return Math.Max(1, (int)Math.Ceiling(connectedPlayers * Config.Percentage));
    }

    private void ExtendMap()
    {
        if (_hasExtended)
            return;

        _hasExtended = true;
        _extendVoteActive = false;
        
        _extendVoteTimer?.Kill();
        
        var timeLimitCvar = ConVar.Find("mp_timelimit");
        if (timeLimitCvar != null)
        {
            var currentTimeLimit = timeLimitCvar.GetPrimitiveValue<float>();
            timeLimitCvar.SetValue(currentTimeLimit + Config.ExtendTime);
        }
        
        Server.PrintToChatAll($"{Config.ChatPrefix} Vote passed! Map extended by {Config.ExtendTime} minutes!");
    }

    private void EndExtendVote()
    {
        if (!_extendVoteActive)
            return;

        _extendVoteActive = false;
        
        var votesNeeded = GetVotesNeeded();
        var currentVotes = _extendVotes.Count;
        
        if (currentVotes >= votesNeeded)
        {
            ExtendMap();
        }
        else
        {
            Server.PrintToChatAll($"{Config.ChatPrefix} Extend vote failed. ({currentVotes}/{votesNeeded} votes received)");
        }
        
        _extendVotes.Clear();
    }

    private void OnMapStart(string mapName)
    {
        _extendVotes.Clear();
        _extendVoteActive = false;
        _hasExtended = false;
        
        _extendVoteTimer?.Kill();
        _extendVoteTimer = null;
    }

    private void OnClientDisconnect(int playerSlot)
    {
        _extendVotes.Remove(playerSlot);
        
        // Check if we still have enough votes after someone leaves
        if (_extendVoteActive)
        {
            var votesNeeded = GetVotesNeeded();
            var currentVotes = _extendVotes.Count;
            
            if (currentVotes >= votesNeeded && !_hasExtended)
            {
                ExtendMap();
            }
        }
    }

    public void OnConfigParsed(VoteExtendConfig config)
    {
        Config = config;
    }
}

public class VoteExtendConfig : BasePluginConfig
{
    public bool Enabled { get; set; } = true;
    public double Percentage { get; set; } = 0.60;
    public int MinPlayers { get; set; } = 2;
    public int VoteDuration { get; set; } = 30;
    public int ExtendTime { get; set; } = 15;
    public int AllowTimeRemaining { get; set; } = 10;
    public string ChatPrefix { get; set; } = "[VoteExtend]";
}