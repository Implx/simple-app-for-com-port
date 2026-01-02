using System;
using System.Windows;
using System.IO;
using System.Text.Json;

namespace SimpleAppForComPort;

public partial class SettingsWindow : Window
{
    public AppSettings Settings { get; private set; }

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        Settings = new AppSettings
        {
            RoundTimeSeconds = current.RoundTimeSeconds,
            LockTimeSeconds = current.LockTimeSeconds,
            BlockAfterRound = current.BlockAfterRound,
            ModeACode = current.ModeACode,
            ModeBCode = current.ModeBCode
        };

        // Populate UI
        TextRoundTime.Text = Settings.RoundTimeSeconds.ToString();
        TextLockTime.Text = Settings.LockTimeSeconds.ToString();
        CheckBlockAfterRound.IsChecked = Settings.BlockAfterRound;
        TextModeA.Text = Settings.ModeACode.ToString();
        TextModeB.Text = Settings.ModeBCode.ToString();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (int.TryParse(TextRoundTime.Text, out var rt)) Settings.RoundTimeSeconds = Math.Max(1, rt);
            if (int.TryParse(TextLockTime.Text, out var lt)) Settings.LockTimeSeconds = Math.Max(0, lt);
            Settings.BlockAfterRound = CheckBlockAfterRound.IsChecked == true;
    
            if (!string.IsNullOrWhiteSpace(TextModeA.Text))
                Settings.ModeACode = TextModeA.Text.Trim()[0];
            if (!string.IsNullOrWhiteSpace(TextModeB.Text))
                Settings.ModeBCode = TextModeB.Text.Trim()[0];
    
            // Persist immediately
            AppSettings.Save(Settings);
    
            DialogResult = true;
            Close();
        }
        catch
        {
            MessageBox.Show("Проверьте корректность введённых значений.", "Настройки", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class AppSettings
{
    public int RoundTimeSeconds { get; set; } = 60;
    public int LockTimeSeconds { get; set; } = 6;
    public bool BlockAfterRound { get; set; } = true;
    public char ModeACode { get; set; } = 'b';
    public char ModeBCode { get; set; } = 'p';

    public static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpleAppForComPort", "settings.json");

    public static AppSettings LoadOrDefault()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // ignore and return defaults
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // swallow errors to avoid breaking UX; logging can be added later
        }
    }
}