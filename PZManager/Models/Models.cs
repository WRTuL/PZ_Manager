// models.cs — all the little data buckets the app passes around.
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PZManager.Models
{
    public enum FetchStatus { Idle, Fetching, Ok, Failed }

    // one row in the mod table.
    // WorkshopId  = the big number steam gives it
    // ScrapedName = what we ripped off the workshop page
    // OverrideName = what the user decided to call it because we were wrong
    // ModFolderId = the ACTUAL id pz cares about. not the number. not the name. the *folder*. thanks TiS.
    public class ModEntry : INotifyPropertyChanged
    {
        private string _workshop_id = "";
        private string _scraped_name = "";
        private string _override_name = "";
        private string _mod_folder_id = "";
        private string _version = "";
        private List<string> _dependencies = new();
        private FetchStatus _status = FetchStatus.Idle;

        public string WorkshopId   { get => _workshop_id;   set { _workshop_id = value;   OnPropertyChanged(); } }
        public string ScrapedName  { get => _scraped_name;  set { _scraped_name = value;  OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); } }
        public string OverrideName { get => _override_name; set { _override_name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); } }
        public string ModFolderId  { get => _mod_folder_id; set { _mod_folder_id = value; OnPropertyChanged(); } }

        // version string from mod.info on disk — empty until server has run once
        public string Version
        {
            get => _version;
            set { _version = value; OnPropertyChanged(); OnPropertyChanged(nameof(VersionDisplay)); }
        }

        // dependencies from Steam Workshop required items + mod.info require= field
        public List<string> Dependencies
        {
            get => _dependencies;
            set { _dependencies = value; OnPropertyChanged(); OnPropertyChanged(nameof(DepsDisplay)); OnPropertyChanged(nameof(HasDeps)); }
        }

        public string VersionDisplay => string.IsNullOrWhiteSpace(Version) ? "—" : Version;

        public bool HasDeps => Dependencies.Count > 0;

        // comma-separated dep names for the grid cell tooltip
        public string DepsDisplay => Dependencies.Count == 0
            ? "none"
            : string.Join(", ", Dependencies);

        public FetchStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public string DisplayName => !string.IsNullOrWhiteSpace(OverrideName) ? OverrideName
                                   : !string.IsNullOrWhiteSpace(ScrapedName)  ? ScrapedName
                                   : WorkshopId;

        public string StatusText => Status switch
        {
            FetchStatus.Fetching => "● fetching",
            FetchStatus.Ok       => "● fetched",
            FetchStatus.Failed   => "● failed",
            _                    => "● idle"
        };

        public string StatusColor => Status switch
        {
            FetchStatus.Ok       => "#FF4F9E6F",
            FetchStatus.Failed   => "#FF9E3A3A",
            FetchStatus.Fetching => "#FF9E7C2A",
            _                    => "#FF4A4F62"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Build 42 Sandbox Settings ─────────────────────────────────────────────────
    // updated for B42 stable — new categories, new fields, new pain, same jank.
    // old B41 fields kept where they still exist in B42.
    public class SandboxSettings : INotifyPropertyChanged
    {
        // ── Zombie Lore ──
        // all values are plain integers matching exactly what PZ writes in SandboxVars.lua.
        // "Random" options are just another integer value
        //
        // Speed:      1=Sprinters 2=Fast Shamblers 3=Shamblers 4=Random
        // Strength:   1=Superhuman 2=Normal 3=Weak 4=Random
        // Toughness:  1=Tough 2=Normal 3=Fragile 4=Random
        // Cognition:  1=Navigate+Doors 2=Navigate 3=Basic 4=Random
        // Memory:     1=Long 2=Normal 3=Short 4=None 5=Random 6=Random(Normal-None)
        // Sight:      1=Eagle 2=Normal 3=Poor 4=Random 5=Random(Normal-Poor)
        // Hearing:    1=Pinpoint 2=Normal 3=Poor 4=Random 5=Random(Normal-Poor)
        // Transmission: 1=BloodSaliva 2=SalivaOnly 3=EveryoneInfected 4=None
        // Mortality:  1=Instant 2=0-30s 3=0-1min 4=0-12hr 5=2-3days 6=1-2wks 7=Never
        // Reanimate:  1=Instant 2=0-30s 3=0-1min 4=0-12hr 5=2-3days 6=1-2wks
        private int _zombie_count               = 3;
        private int _zombie_distribution        = 2;
        private int _zombie_speed               = 4;  // default: Random
        private int _sprinter_percentage        = 0;
        private int _zombie_strength            = 4;  // default: Random
        private int _zombie_toughness           = 4;  // default: Random
        private int _zombie_cognition           = 4;  // default: Random
        private int _zombie_crawl_under_vehicle = 5;  // default: Often
        private int _door_opening_percentage    = 0;
        private int _zombie_memory              = 2;  // default: Normal
        private int _zombie_sight               = 5;  // default: Random(Normal-Poor)
        private int _zombie_hearing             = 5;  // default: Random(Normal-Poor)
        private int _transmission               = 2;  // default: Saliva Only
        private int _mortality                  = 5;  // default: 2-3 Days
        private int _reanimate                  = 3;  // default: 0-1 Minutes

        // ── Loot ──
        private int _food_loot = 3;
        private int _weapon_loot = 3;
        private int _other_loot = 3;
        private int _generator_spawning = 3;
        private int _vehicle_spawning = 3;

        // ── World ──
        private int _start_month = 7;
        private int _start_time = 9;
        private int _start_year = 1;
        private int _water_shutoff_days = 14;
        private int _electricity_shutoff_days = 14;
        private int _rain_days = 2;
        private int _erosion_speed = 2;
        private int _erosion_days = 0;
        private bool _fire_spread = true;

        // ── Player ──
        private int _xp_multiplier = 1;
        private bool _nights_are_quick = false;
        private bool _sleep_allowed = false;
        private bool _sleep_needed = false;
        private bool _survival_mode = false;
        private int _respawn_hours = 0;
        private int _respawn_unseen_hours = 16;

        // ── Multiplayer / Server ──
        private bool _pvp = false;
        private bool _safehouse = true;
        private bool _factions = true;
        private int _map_visibility = 1;
        private bool _construction_prevents_respawn = true;
        private int _max_account_per_user = 0;
        private bool _anti_cheat = true;
        private int _display_user_name = 2;
        private int _chat_stream = 1;

        // ── Nature / Animals (new in B42) ──
        private int _animal_population = 3;
        private bool _animal_tracking = true;

        public int ZombieCount              { get => _zombie_count;               set { _zombie_count = value;               OnPropertyChanged(); } }
        public int ZombieDistribution       { get => _zombie_distribution;        set { _zombie_distribution = value;        OnPropertyChanged(); } }
        public int ZombieSpeed              { get => _zombie_speed;               set { _zombie_speed = value;               OnPropertyChanged(); } }
        public int SprinterPercentage       { get => _sprinter_percentage;        set { _sprinter_percentage = value;        OnPropertyChanged(); } }
        public int ZombieStrength           { get => _zombie_strength;            set { _zombie_strength = value;            OnPropertyChanged(); } }
        public int ZombieToughness          { get => _zombie_toughness;           set { _zombie_toughness = value;           OnPropertyChanged(); } }
        public int ZombieCognition          { get => _zombie_cognition;           set { _zombie_cognition = value;           OnPropertyChanged(); } }
        public int ZombieCrawlUnderVehicle  { get => _zombie_crawl_under_vehicle; set { _zombie_crawl_under_vehicle = value; OnPropertyChanged(); } }
        public int DoorOpeningPercentage    { get => _door_opening_percentage;    set { _door_opening_percentage = value;    OnPropertyChanged(); } }
        public int ZombieMemory             { get => _zombie_memory;              set { _zombie_memory = value;              OnPropertyChanged(); } }
        public int ZombieSight              { get => _zombie_sight;               set { _zombie_sight = value;               OnPropertyChanged(); } }
        public int ZombieHearing            { get => _zombie_hearing;             set { _zombie_hearing = value;             OnPropertyChanged(); } }
        public int Transmission             { get => _transmission;               set { _transmission = value;               OnPropertyChanged(); } }
        public int Mortality                { get => _mortality;                   set { _mortality = value;                  OnPropertyChanged(); } }
        public int Reanimate                { get => _reanimate;                   set { _reanimate = value;                  OnPropertyChanged(); } }
        public int FoodLoot                 { get => _food_loot;                 set { _food_loot = value;                 OnPropertyChanged(); } }
        public int WeaponLoot               { get => _weapon_loot;               set { _weapon_loot = value;               OnPropertyChanged(); } }
        public int OtherLoot                { get => _other_loot;                set { _other_loot = value;                OnPropertyChanged(); } }
        public int GeneratorSpawning        { get => _generator_spawning;        set { _generator_spawning = value;        OnPropertyChanged(); } }
        public int VehicleSpawning          { get => _vehicle_spawning;          set { _vehicle_spawning = value;          OnPropertyChanged(); } }
        public int StartMonth               { get => _start_month;               set { _start_month = value;               OnPropertyChanged(); } }
        public int StartTime                { get => _start_time;                set { _start_time = value;                OnPropertyChanged(); } }
        public int StartYear                { get => _start_year;                set { _start_year = value;                OnPropertyChanged(); } }
        public int WaterShutoffDays         { get => _water_shutoff_days;        set { _water_shutoff_days = value;        OnPropertyChanged(); } }
        public int ElectricityShutoffDays   { get => _electricity_shutoff_days;  set { _electricity_shutoff_days = value;  OnPropertyChanged(); } }
        public int RainDays                 { get => _rain_days;                 set { _rain_days = value;                 OnPropertyChanged(); } }
        public int ErosionSpeed             { get => _erosion_speed;             set { _erosion_speed = value;             OnPropertyChanged(); } }
        public int ErosionDays              { get => _erosion_days;              set { _erosion_days = value;              OnPropertyChanged(); } }
        public bool FireSpread              { get => _fire_spread;               set { _fire_spread = value;               OnPropertyChanged(); } }
        public int XpMultiplier             { get => _xp_multiplier;             set { _xp_multiplier = value;             OnPropertyChanged(); } }
        public bool NightsAreQuick          { get => _nights_are_quick;          set { _nights_are_quick = value;          OnPropertyChanged(); } }
        public bool SleepAllowed            { get => _sleep_allowed;             set { _sleep_allowed = value;             OnPropertyChanged(); } }
        public bool SleepNeeded             { get => _sleep_needed;              set { _sleep_needed = value;              OnPropertyChanged(); } }
        public bool SurvivalMode            { get => _survival_mode;             set { _survival_mode = value;             OnPropertyChanged(); } }
        public int RespawnHours             { get => _respawn_hours;             set { _respawn_hours = value;             OnPropertyChanged(); } }
        public int RespawnUnseenHours       { get => _respawn_unseen_hours;      set { _respawn_unseen_hours = value;      OnPropertyChanged(); } }
        public bool Pvp                     { get => _pvp;                       set { _pvp = value;                       OnPropertyChanged(); } }
        public bool Safehouse               { get => _safehouse;                 set { _safehouse = value;                 OnPropertyChanged(); } }
        public bool Factions                { get => _factions;                  set { _factions = value;                  OnPropertyChanged(); } }
        public int MapVisibility            { get => _map_visibility;            set { _map_visibility = value;            OnPropertyChanged(); } }
        public bool ConstructionPreventsRespawn { get => _construction_prevents_respawn; set { _construction_prevents_respawn = value; OnPropertyChanged(); } }
        public int MaxAccountPerUser        { get => _max_account_per_user;      set { _max_account_per_user = value;      OnPropertyChanged(); } }
        public bool AntiCheat               { get => _anti_cheat;                set { _anti_cheat = value;                OnPropertyChanged(); } }
        public int DisplayUserName          { get => _display_user_name;         set { _display_user_name = value;         OnPropertyChanged(); } }
        public int ChatStream               { get => _chat_stream;               set { _chat_stream = value;               OnPropertyChanged(); } }
        public int AnimalPopulation         { get => _animal_population;         set { _animal_population = value;         OnPropertyChanged(); } }
        public bool AnimalTracking          { get => _animal_tracking;           set { _animal_tracking = value;           OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Server Config ─────────────────────────────────────────────────────────────
    public class ServerConfig : INotifyPropertyChanged
    {
        private string _public_name = "My Server";
        private string _public_description = "";
        private string _server_welcome_message = "Welcome!";
        private bool _public_server = false;
        private string _password = "";
        private string _admin_password = "admin";
        private int _max_players = 16;
        private int _port = 16261;
        private int _udp_port = 16262;
        private int _rcon_port = 27015;
        private string _rcon_password = "";
        private bool _pause_empty = true;
        private int _save_world_interval = 0;
        private string _jvm_args = "-Xmx10g";

        public string PublicName            { get => _public_name;            set { _public_name = value;            OnPropertyChanged(); } }
        public string PublicDescription     { get => _public_description;     set { _public_description = value;     OnPropertyChanged(); } }
        public string ServerWelcomeMessage  { get => _server_welcome_message; set { _server_welcome_message = value; OnPropertyChanged(); } }
        public bool PublicServer            { get => _public_server;          set { _public_server = value;          OnPropertyChanged(); } }
        public string Password              { get => _password;               set { _password = value;               OnPropertyChanged(); } }
        public string AdminPassword         { get => _admin_password;         set { _admin_password = value;         OnPropertyChanged(); } }
        public int MaxPlayers               { get => _max_players;            set { _max_players = value;            OnPropertyChanged(); } }
        public int Port                     { get => _port;                   set { _port = value;                   OnPropertyChanged(); } }
        public int UdpPort                  { get => _udp_port;               set { _udp_port = value;               OnPropertyChanged(); } }
        public int RconPort                 { get => _rcon_port;              set { _rcon_port = value;              OnPropertyChanged(); } }
        public string RconPassword          { get => _rcon_password;          set { _rcon_password = value;          OnPropertyChanged(); } }
        public bool PauseEmpty              { get => _pause_empty;            set { _pause_empty = value;            OnPropertyChanged(); } }
        public int SaveWorldInterval        { get => _save_world_interval;    set { _save_world_interval = value;    OnPropertyChanged(); } }
        public string JvmArgs               { get => _jvm_args;               set { _jvm_args = value;               OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── App Settings ──────────────────────────────────────────────────────────────
    public class AppSettings
    {
        // where StartServer64.bat lives — the steamcmd install dir
        public string ServerDirectory  { get; set; } = @"D:\Games\SteamCMD\steamapps\common\Project Zomboid Dedicated Server";
        // where servertest.ini, SandboxVars.lua, and Logs live — NOT the same place. thanks TiS.
        public string ConfigDirectory  { get; set; } = @"C:\Users\DVMAdmin\Zomboid\Server";
        public string ServerConfigName { get; set; } = "servertest";
        public string RconHost         { get; set; } = "127.0.0.1";
        public bool AutoFetchModNames  { get; set; } = true;
        // auto-restart config — because mods update and the server just sits there blissfully unaware
        public bool AutoRestartEnabled  { get; set; } = false;
        public double AutoRestartHours  { get; set; } = 6.0;
        public string RestartWarningMsg { get; set; } = "Server restarting in {minutes} minutes for maintenance.";
        // optional Steam Web API key — enables proper dependency resolution via IPublishedFileService
        // get one free at https://steamcommunity.com/dev/apikey (any domain name works)
        public string SteamApiKey       { get; set; } = "";
    }

    // one line in the rcon console log
    public class ConsoleLogEntry
    {
        public string Timestamp { get; set; } = "";
        public string Message   { get; set; } = "";
        public string Color     { get; set; } = "#FFC0C4D4";
    }

    // ── Mod Sandbox Schema (Option B) ─────────────────────────────────────────────
    // represents one option parsed from a mod's sandbox-options.txt
    // these files live at: {workshopDir}\{workshopId}\media\sandbox-options.txt
    // format from PZwiki: option ModName.OptionName { type = integer, min = 0, max = 10, default = 5, ... }

    public enum ModOptionType { Integer, Boolean, Enum, Double, String }

    public class ModSandboxOption : INotifyPropertyChanged
    {
        private string _current_value = "";

        public string FullKey    { get; set; } = ""; // "ModName.OptionName"
        public string ModName    { get; set; } = ""; // "ModName"
        public string OptionName { get; set; } = ""; // "OptionName"
        public string Label      { get; set; } = ""; // human-readable label (from translation or OptionName)
        public ModOptionType Type { get; set; } = ModOptionType.Integer;
        public double Min        { get; set; } = 0;
        public double Max        { get; set; } = 10;
        public string DefaultValue { get; set; } = "";
        public List<string> EnumValues { get; set; } = new(); // for Type = Enum

        public string CurrentValue
        {
            get => _current_value;
            set { _current_value = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    // one mod's worth of sandbox options, grouped for display
    public class ModSandboxSection
    {
        public string ModId      { get; set; } = "";
        public string ModName    { get; set; } = ""; // friendly name from scrape
        public string WorkshopId { get; set; } = "";
        public List<ModSandboxOption> Options { get; set; } = new();
    }
}
