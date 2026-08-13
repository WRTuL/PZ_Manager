// converters.cs — wpf value converters. boring but necessary.
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PZManager.Converters
{
    public class StringToColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex)
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); } catch { }
            return Brushes.Gray;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    public class MonthNameConverter : IValueConverter
    {
        private static readonly string[] months = { "Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= 12 ? months[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Speed: 1=Sprinters 2=Fast Shamblers 3=Shamblers 4=Random
    public class ZombieSpeedConverter : IValueConverter
    {
        private static readonly string[] labels = { "Sprinters", "Fast Shamblers", "Shamblers", "Random" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Strength: 1=Superhuman 2=Normal 3=Weak 4=Random
    public class ZombieStrengthConverter : IValueConverter
    {
        private static readonly string[] labels = { "Superhuman", "Normal", "Weak", "Random" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Toughness: 1=Tough 2=Normal 3=Fragile 4=Random
    public class ZombieToughnessConverter : IValueConverter
    {
        private static readonly string[] labels = { "Tough", "Normal", "Fragile", "Random" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Cognition: 1=Navigate+Doors 2=Navigate 3=Basic Navigation 4=Random
    public class CognitionConverter : IValueConverter
    {
        private static readonly string[] labels = { "Navigate + Use Doors", "Navigate", "Basic Navigation", "Random" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Memory: 1=Long 2=Normal 3=Short 4=None 5=Random 6=Random(Normal-None)
    public class MemoryConverter : IValueConverter
    {
        private static readonly string[] labels = { "Long", "Normal", "Short", "None", "Random", "Rnd Normal-None" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Sight: 1=Eagle 2=Normal 3=Poor 4=Random 5=Random(Normal-Poor)
    public class SightConverter : IValueConverter
    {
        private static readonly string[] labels = { "Eagle", "Normal", "Poor", "Random", "Rnd Normal-Poor" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Hearing: 1=Pinpoint 2=Normal 3=Poor 4=Random 5=Random(Normal-Poor)
    public class HearingConverter : IValueConverter
    {
        private static readonly string[] labels = { "Pinpoint", "Normal", "Poor", "Random", "Rnd Normal-Poor" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Transmission: 1=Blood+Saliva 2=SalivaOnly 3=EveryoneInfected 4=None
    public class TransmissionConverter : IValueConverter
    {
        private static readonly string[] labels = { "Blood and Saliva", "Saliva Only", "Everyone's Infected", "None" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Mortality: 1=Instant 2=0-30s 3=0-1min 4=0-12hr 5=2-3days 6=1-2wks 7=Never
    public class MortalityConverter : IValueConverter
    {
        private static readonly string[] labels = { "Instant", "0-30 Seconds", "0-1 Minutes", "0-12 Hours", "2-3 Days", "1-2 Weeks", "Never" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Reanimate: 1=Instant 2=0-30s 3=0-1min 4=0-12hr 5=2-3days 6=1-2wks
    public class ReanimateConverter : IValueConverter
    {
        private static readonly string[] labels = { "Instant", "0-30 Seconds", "0-1 Minutes", "0-12 Hours", "2-3 Days", "1-2 Weeks" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // CrawlUnderVehicle: 1=CrawlersOnly 2=ExtremelyRare 3=Rare 4=Sometimes 5=Often 6=VeryOften 7=Always
    public class CrawlConverter : IValueConverter
    {
        private static readonly string[] labels = { "Crawlers Only", "Extremely Rare", "Rare", "Sometimes", "Often", "Very Often", "Always" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // loot abundance 1-5
    public class LootAbundanceConverter : IValueConverter
    {
        private static readonly string[] labels = { "None", "Insane", "Scarce", "Normal", "Abundant" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // generic 1-5 population scale
    public class PopulationConverter : IValueConverter
    {
        private static readonly string[] labels = { "None", "Low", "Normal", "High", "Very High" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // vehicle spawn 1-5
    public class VehicleSpawnConverter : IValueConverter
    {
        private static readonly string[] labels = { "None", "Very Low", "Low", "Normal", "High" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // kept for compatibility — old SenseConverter used for generic 1-4 sense fields
    public class SenseConverter : IValueConverter
    {
        private static readonly string[] labels = { "Poor", "Normal", "Good", "Eagle" };
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var i = value is double d ? (int)d : value is int n ? n : 0;
            return i >= 1 && i <= labels.Length ? labels[i - 1] : "";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
