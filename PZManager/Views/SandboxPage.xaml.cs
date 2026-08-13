// sandbox_page.xaml.cs — B42 sandbox settings
// finally using the correct integer values instead of magic -1 random hacks.
// the lua file uses plain integers for everything including "random" options.
// who knew. we do now.
using Microsoft.Win32;
using PZManager.Models;
using PZManager.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PZManager.Views
{
    public partial class SandboxPage : Page
    {
        private SandboxSettings _settings = new();
        private bool _loading = false;

        public SandboxPage()
        {
            InitializeComponent();
            TryAutoLoad();
        }

        private string GetDefaultPath()
            => Path.Combine(MainWindow.AppSettings.ConfigDirectory, MainWindow.AppSettings.ServerConfigName + "_SandboxVars.lua");

        private void TryAutoLoad()
        {
            var path = GetDefaultPath();
            if (File.Exists(path)) LoadFrom(path);
            else LoadFromSettings(new SandboxSettings());
        }

        private void LoadFrom(string path)
        {
            _settings = SandboxService.Load(path);
            LoadFromSettings(_settings);
            if (TbSandboxStatus != null) TbSandboxStatus.Text = $"loaded from {Path.GetFileName(path)}";
        }

        private void LoadFromSettings(SandboxSettings s)
        {
            _loading = true;
            // zombie lore — all plain integers, slider max matches actual PZ enum size
            SlZombies.Value      = s.ZombieCount;
            SlSpeed.Value        = Math.Clamp(s.ZombieSpeed,     1, 4);
            SlSprinterPct.Value  = Math.Clamp(s.SprinterPercentage, 0, 100);
            SlStrength.Value     = Math.Clamp(s.ZombieStrength,  1, 4);
            SlToughness.Value    = Math.Clamp(s.ZombieToughness, 1, 4);
            SlTransmission.Value = Math.Clamp(s.Transmission,    1, 4);
            SlMortality.Value    = Math.Clamp(s.Mortality,       1, 7);
            SlReanimate.Value    = Math.Clamp(s.Reanimate,       1, 6);
            SlCognition.Value    = Math.Clamp(s.ZombieCognition, 1, 4);
            SlDoorPct.Value      = Math.Clamp(s.DoorOpeningPercentage, 0, 100);
            SlCrawl.Value        = Math.Clamp(s.ZombieCrawlUnderVehicle, 1, 7);
            SlMemory.Value       = Math.Clamp(s.ZombieMemory,   1, 6);
            SlSight.Value        = Math.Clamp(s.ZombieSight,    1, 5);
            SlHearing.Value      = Math.Clamp(s.ZombieHearing,  1, 5);
            // loot
            SlFoodLoot.Value     = s.FoodLoot;
            SlWeaponLoot.Value   = s.WeaponLoot;
            SlOtherLoot.Value    = s.OtherLoot;
            SlGenerators.Value   = s.GeneratorSpawning;
            SlVehicles.Value     = s.VehicleSpawning;
            // world
            SlStartMonth.Value   = s.StartMonth;
            SlStartYear.Value    = s.StartYear;
            SlWater.Value        = s.WaterShutoffDays;
            SlElec.Value         = s.ElectricityShutoffDays;
            SlErosion.Value      = s.ErosionSpeed;
            CbFireSpread.IsChecked = s.FireSpread;
            // player
            SlXp.Value           = s.XpMultiplier;
            SlRespawn.Value      = s.RespawnHours;
            CbNightsQuick.IsChecked  = s.NightsAreQuick;
            CbSleepAllowed.IsChecked = s.SleepAllowed;
            CbSleepNeeded.IsChecked  = s.SleepNeeded;
            CbSurvivalMode.IsChecked = s.SurvivalMode;
            // multiplayer
            CbPvp.IsChecked          = s.Pvp;
            CbSafehouse.IsChecked    = s.Safehouse;
            CbFactions.IsChecked     = s.Factions;
            CbAntiCheat.IsChecked    = s.AntiCheat;
            CbConstruction.IsChecked = s.ConstructionPreventsRespawn;
            SlMaxAccounts.Value      = s.MaxAccountPerUser;
            // animals
            SlAnimals.Value          = s.AnimalPopulation;
            CbAnimalTracking.IsChecked = s.AnimalTracking;
            _loading = false;
        }

        private void ApplyToSettings()
        {
            _settings.ZombieCount              = (int)SlZombies.Value;
            _settings.ZombieSpeed              = (int)SlSpeed.Value;
            _settings.SprinterPercentage       = (int)SlSprinterPct.Value;
            _settings.ZombieStrength           = (int)SlStrength.Value;
            _settings.ZombieToughness          = (int)SlToughness.Value;
            _settings.Transmission             = (int)SlTransmission.Value;
            _settings.Mortality                = (int)SlMortality.Value;
            _settings.Reanimate                = (int)SlReanimate.Value;
            _settings.ZombieCognition          = (int)SlCognition.Value;
            _settings.DoorOpeningPercentage    = (int)SlDoorPct.Value;
            _settings.ZombieCrawlUnderVehicle  = (int)SlCrawl.Value;
            _settings.ZombieMemory             = (int)SlMemory.Value;
            _settings.ZombieSight              = (int)SlSight.Value;
            _settings.ZombieHearing            = (int)SlHearing.Value;
            _settings.FoodLoot                 = (int)SlFoodLoot.Value;
            _settings.WeaponLoot               = (int)SlWeaponLoot.Value;
            _settings.OtherLoot                = (int)SlOtherLoot.Value;
            _settings.GeneratorSpawning        = (int)SlGenerators.Value;
            _settings.VehicleSpawning          = (int)SlVehicles.Value;
            _settings.StartMonth               = (int)SlStartMonth.Value;
            _settings.StartYear                = (int)SlStartYear.Value;
            _settings.WaterShutoffDays         = (int)SlWater.Value;
            _settings.ElectricityShutoffDays   = (int)SlElec.Value;
            _settings.ErosionSpeed             = (int)SlErosion.Value;
            _settings.FireSpread               = CbFireSpread.IsChecked == true;
            _settings.XpMultiplier             = (int)SlXp.Value;
            _settings.RespawnHours             = (int)SlRespawn.Value;
            _settings.NightsAreQuick           = CbNightsQuick.IsChecked == true;
            _settings.SleepAllowed             = CbSleepAllowed.IsChecked == true;
            _settings.SleepNeeded              = CbSleepNeeded.IsChecked == true;
            _settings.SurvivalMode             = CbSurvivalMode.IsChecked == true;
            _settings.Pvp                      = CbPvp.IsChecked == true;
            _settings.Safehouse                = CbSafehouse.IsChecked == true;
            _settings.Factions                 = CbFactions.IsChecked == true;
            _settings.AntiCheat                = CbAntiCheat.IsChecked == true;
            _settings.ConstructionPreventsRespawn = CbConstruction.IsChecked == true;
            _settings.MaxAccountPerUser        = (int)SlMaxAccounts.Value;
            _settings.AnimalPopulation         = (int)SlAnimals.Value;
            _settings.AnimalTracking           = CbAnimalTracking.IsChecked == true;
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Lua files|*.lua|All files|*.*", Title = "Select SandboxVars.lua" };
            if (Directory.Exists(MainWindow.AppSettings.ConfigDirectory))
                dlg.InitialDirectory = MainWindow.AppSettings.ConfigDirectory;
            if (dlg.ShowDialog() == true) LoadFrom(dlg.FileName);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ApplyToSettings();
            var path = GetDefaultPath();
            try
            {
                SandboxService.Save(path, _settings);
                TbSandboxStatus.Text = $"saved ✓ — {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"save failed:\n{ex.Message}", "error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_loading && TbSandboxStatus != null) TbSandboxStatus.Text = "unsaved changes";
        }

        private void Toggle_Changed(object sender, RoutedEventArgs e)
        {
            if (!_loading && TbSandboxStatus != null) TbSandboxStatus.Text = "unsaved changes";
        }
    }
}
