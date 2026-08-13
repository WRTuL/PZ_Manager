// sandbox_service.cs
// reads and writes SandboxVars.lua.
// it's not really lua. it's a flat key=value list with lua syntax bolted on top
// so technically it IS valid lua but nobody actually runs it as lua so who cares.
// mods upend themselves after all the main sandbox options, i mean its ok??? 1 file for all the config is nice but it makes parsing a bit more annoying.
// thanks TiS. really keeping us on our toes here.
using PZManager.Models;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PZManager.Services
{
    public static class SandboxService
    {
        // another banger by Claude
        private static readonly Regex lua_var_regex = new(@"(\w+)\s*=\s*([^,\n\r]+)", RegexOptions.Compiled);

        public static Dictionary<string, string> ReadLua(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return dict;
            foreach (Match m in lua_var_regex.Matches(File.ReadAllText(path)))
                dict[m.Groups[1].Value.Trim()] = m.Groups[2].Value.Trim().TrimEnd(',').Trim();
            return dict;
        }

        private static int  pi(Dictionary<string, string> d, string k, int  fb) => d.TryGetValue(k, out var v) && int.TryParse(v, out var r) ? r : fb;
        private static bool pb(Dictionary<string, string> d, string k, bool fb) => d.TryGetValue(k, out var v) ? v.Equals("true", StringComparison.OrdinalIgnoreCase) : fb;

        public static SandboxSettings Load(string lua_path)
        {
            var d = ReadLua(lua_path);
            return new SandboxSettings
            {
                // zombie lore — all plain integers, Random is just another value not a magic string
                ZombieCount             = pi(d, "Zombies",               3),
                ZombieDistribution      = pi(d, "Distribution",          2),
                ZombieSpeed             = pi(d, "Speed",                 4),   // 4 = Random
                SprinterPercentage      = pi(d, "SprinterPercentage",    0),
                ZombieStrength          = pi(d, "Strength",              4),   // 4 = Random
                ZombieToughness         = pi(d, "Toughness",             4),   // 4 = Random
                Transmission            = pi(d, "Transmission",          2),   // 2 = Saliva Only
                Mortality               = pi(d, "Mortality",             5),   // 5 = 2-3 Days
                Reanimate               = pi(d, "Reanimate",             3),   // 3 = 0-1 Minutes
                ZombieCognition         = pi(d, "Cognition",             4),   // 4 = Random
                DoorOpeningPercentage   = pi(d, "DoorOpeningPercentage", 0),
                ZombieCrawlUnderVehicle = pi(d, "CrawlUnderVehicle",    5),   // 5 = Often
                ZombieMemory            = pi(d, "Memory",                2),   // 2 = Normal
                ZombieSight             = pi(d, "Sight",                 5),   // 5 = Random(Normal-Poor)
                ZombieHearing           = pi(d, "Hearing",               5),   // 5 = Random(Normal-Poor)
                // loot
                FoodLoot                = pi(d, "FoodLoot",              3),
                WeaponLoot              = pi(d, "WeaponLoot",            3),
                OtherLoot               = pi(d, "OtherLoot",             3),
                GeneratorSpawning       = pi(d, "GeneratorSpawning",     3),
                VehicleSpawning         = pi(d, "VehicleSpawning",       3),
                // world
                StartMonth              = pi(d, "StartMonth",            7),
                StartTime               = pi(d, "StartTime",             9),
                StartYear               = pi(d, "StartYear",             1),
                WaterShutoffDays        = pi(d, "WaterShutModifier",     14),
                ElectricityShutoffDays  = pi(d, "ElecShutModifier",      14),
                RainDays                = pi(d, "Rain",                  2),
                ErosionSpeed            = pi(d, "ErosionSpeed",          2),
                ErosionDays             = pi(d, "ErosionDays",           0),
                FireSpread              = pb(d, "FireSpread",            true),
                // player
                XpMultiplier            = pi(d, "XPMultiplier",          1),
                NightsAreQuick          = pb(d, "NightsAreQuick",        false),
                SleepAllowed            = pb(d, "SleepAllowed",          false),
                SleepNeeded             = pb(d, "SleepNeeded",           false),
                SurvivalMode            = pb(d, "SurvivalMode",          false),
                RespawnHours            = pi(d, "HoursForCorpseRemoval", 0),
                RespawnUnseenHours      = pi(d, "HoursForWorldItemRemoval", 0),
                // multiplayer
                Pvp                     = pb(d, "PVP",                   false),
                Safehouse               = pb(d, "Safehouse",             true),
                Factions                = pb(d, "Faction",               true),
                MapVisibility           = pi(d, "MapAllKnown",           1),
                ConstructionPreventsRespawn = pb(d, "ConstructionPreventsSpawn", true),
                MaxAccountPerUser       = pi(d, "MaxAccountsPerUser",    0),
                AntiCheat               = pb(d, "AntiCheatProtectionType", true),
                DisplayUserName         = pi(d, "DisplayUserName",       2),
                ChatStream              = pi(d, "ChatStreams",            1),
                // nature / animals (B42)
                AnimalPopulation        = pi(d, "AnimalCount",           3),
                AnimalTracking          = pb(d, "AnimalTracking",        true),
            };
        }

        public static void Save(string lua_path, SandboxSettings s)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SandboxVars = {");
            // zombie lore
            sb.AppendLine($"    Zombies = {s.ZombieCount},");
            sb.AppendLine($"    Distribution = {s.ZombieDistribution},");
            sb.AppendLine($"    Speed = {s.ZombieSpeed},");
            sb.AppendLine($"    SprinterPercentage = {s.SprinterPercentage},");
            sb.AppendLine($"    Strength = {s.ZombieStrength},");
            sb.AppendLine($"    Toughness = {s.ZombieToughness},");
            sb.AppendLine($"    Transmission = {s.Transmission},");
            sb.AppendLine($"    Mortality = {s.Mortality},");
            sb.AppendLine($"    Reanimate = {s.Reanimate},");
            sb.AppendLine($"    Cognition = {s.ZombieCognition},");
            sb.AppendLine($"    DoorOpeningPercentage = {s.DoorOpeningPercentage},");
            sb.AppendLine($"    CrawlUnderVehicle = {s.ZombieCrawlUnderVehicle},");
            sb.AppendLine($"    Memory = {s.ZombieMemory},");
            sb.AppendLine($"    Sight = {s.ZombieSight},");
            sb.AppendLine($"    Hearing = {s.ZombieHearing},");
            // loot
            sb.AppendLine($"    FoodLoot = {s.FoodLoot},");
            sb.AppendLine($"    WeaponLoot = {s.WeaponLoot},");
            sb.AppendLine($"    OtherLoot = {s.OtherLoot},");
            sb.AppendLine($"    GeneratorSpawning = {s.GeneratorSpawning},");
            sb.AppendLine($"    VehicleSpawning = {s.VehicleSpawning},");
            // world
            sb.AppendLine($"    StartMonth = {s.StartMonth},");
            sb.AppendLine($"    StartTime = {s.StartTime},");
            sb.AppendLine($"    StartYear = {s.StartYear},");
            sb.AppendLine($"    WaterShutModifier = {s.WaterShutoffDays},");
            sb.AppendLine($"    ElecShutModifier = {s.ElectricityShutoffDays},");
            sb.AppendLine($"    Rain = {s.RainDays},");
            sb.AppendLine($"    ErosionSpeed = {s.ErosionSpeed},");
            sb.AppendLine($"    ErosionDays = {s.ErosionDays},");
            sb.AppendLine($"    FireSpread = {(s.FireSpread ? "true" : "false")},");
            // player
            sb.AppendLine($"    XPMultiplier = {s.XpMultiplier},");
            sb.AppendLine($"    NightsAreQuick = {(s.NightsAreQuick ? "true" : "false")},");
            sb.AppendLine($"    SleepAllowed = {(s.SleepAllowed ? "true" : "false")},");
            sb.AppendLine($"    SleepNeeded = {(s.SleepNeeded ? "true" : "false")},");
            sb.AppendLine($"    SurvivalMode = {(s.SurvivalMode ? "true" : "false")},");
            sb.AppendLine($"    HoursForCorpseRemoval = {s.RespawnHours},");
            sb.AppendLine($"    HoursForWorldItemRemoval = {s.RespawnUnseenHours},");
            // multiplayer
            sb.AppendLine($"    PVP = {(s.Pvp ? "true" : "false")},");
            sb.AppendLine($"    Safehouse = {(s.Safehouse ? "true" : "false")},");
            sb.AppendLine($"    Faction = {(s.Factions ? "true" : "false")},");
            sb.AppendLine($"    MapAllKnown = {s.MapVisibility},");
            sb.AppendLine($"    ConstructionPreventsSpawn = {(s.ConstructionPreventsRespawn ? "true" : "false")},");
            sb.AppendLine($"    MaxAccountsPerUser = {s.MaxAccountPerUser},");
            sb.AppendLine($"    AntiCheatProtectionType = {(s.AntiCheat ? "true" : "false")},");
            sb.AppendLine($"    DisplayUserName = {s.DisplayUserName},");
            sb.AppendLine($"    ChatStreams = {s.ChatStream},");
            // animals (B42)
            sb.AppendLine($"    AnimalCount = {s.AnimalPopulation},");
            sb.AppendLine($"    AnimalTracking = {(s.AnimalTracking ? "true" : "false")},");
            sb.AppendLine("}");
            File.WriteAllText(lua_path, sb.ToString(), Encoding.UTF8);
        }
    }
}
