using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace SimpleAppForComPort;

public partial class MainWindow : Window
{
    private readonly SerialPortService _serial = new();
    private readonly DispatcherTimer _roundTimer = new();
    private TimeSpan _remaining;
    private bool _roundActive;
    private bool _firstColorCaptured;
    private AppSettings _settings = new();

    private readonly Dictionary<string, string> _colorMap = new()
    {
        { "01", "Синяя" },
        { "11", "Зелёная" },
        { "21", "Красная" },
        { "31", "Жёлтая" },
    };

    public MainWindow()
    {
        InitializeComponent();
        InitUi();
        _settings = AppSettings.LoadOrDefault();
        InitUi();
        HookSerialEvents();
    }

    private void InitUi()
    {
        RefreshPorts();
        BtnDisconnect.IsEnabled = false;
        LabelTimer.Content = FormatTime(TimeSpan.Zero);
        LabelFirstColor.Content = "—";
        LabelMode.Content = "—";
        TextStatus.Text = "Отключен";

        _roundTimer.Interval = TimeSpan.FromSeconds(1);
        _roundTimer.Tick += OnRoundTimerTick;
    }

    private void HookSerialEvents()
    {
        _serial.MessageReceived += OnMessageReceived;
        _serial.DataReceived += OnDataReceived;
        _serial.ErrorOccurred += msg => AppendLog($"! Ошибка: {msg}");
    }

    private void RefreshPorts()
    {
        ComboPorts.ItemsSource = _serial.GetAvailablePorts();
        if (ComboPorts.Items.Count > 0)
            ComboPorts.SelectedIndex = 0;
    }

    private void AppendLog(string line)
    {
        var stamp = DateTime.Now.ToString("HH:mm:ss");
        TextLog.AppendText($"[{stamp}] {line}\n");
        TextLog.ScrollToEnd();
    }

    private string FormatTime(TimeSpan ts) => ts.ToString(@"mm\:ss");

    // Event Handlers
    private void OnRefreshPortsClick(object sender, RoutedEventArgs e) => RefreshPorts();

    private void OnConnectClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ComboPorts.SelectedItem is string portName)
            {
                _serial.Connect(portName); // дефолтные параметры
                TextStatus.Text = $"Подключен ({portName})";
                BtnConnect.IsEnabled = false;
                BtnDisconnect.IsEnabled = true;
                AppendLog($"→ Открыт порт {portName}");
            }
            else
            {
                MessageBox.Show("Выберите COM-порт", "COM", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"! Не удалось подключиться: {ex.Message}");
        }
    }

    private void OnDisconnectClick(object sender, RoutedEventArgs e)
    {
        try
        {
            _serial.Disconnect();
            TextStatus.Text = "Отключен";
            BtnConnect.IsEnabled = true;
            BtnDisconnect.IsEnabled = false;
            AppendLog("→ Порт закрыт");
        }
        catch (Exception ex)
        {
            AppendLog($"! Не удалось отключиться: {ex.Message}");
        }
    }

    private void OnStartRoundClick(object sender, RoutedEventArgs e)
    {
        if (!_serial.IsOpen)
        {
            MessageBox.Show("Сначала подключитесь к порту", "Раунд", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _serial.SendAscii("r");
            AppendLog("→ Команда: r (начать раунд)");

            _remaining = TimeSpan.FromSeconds(_settings.RoundTimeSeconds);
            LabelTimer.Content = FormatTime(_remaining);
            LabelFirstColor.Content = "—";
            _firstColorCaptured = false;

            _roundActive = true;
            _roundTimer.Start();
        }
        catch (Exception ex)
        {
            AppendLog($"! Ошибка отправки команды r: {ex.Message}");
        }
    }

    private void OnModeAClick(object sender, RoutedEventArgs e)
    {
        SendMode(_settings.ModeACode, "A");
    }

    private void OnModeBClick(object sender, RoutedEventArgs e)
    {
        SendMode(_settings.ModeBCode, "B");
    }

    private void SendMode(char code, string label)
    {
        if (!_serial.IsOpen)
        {
            MessageBox.Show("Сначала подключитесь к порту", "Режим", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            _serial.SendAscii(code.ToString());
            LabelMode.Content = $"Режим {label}";
            AppendLog($"→ Команда режима: '{code}' (Режим {label})");
        }
        catch (Exception ex)
        {
            AppendLog($"! Ошибка отправки режима {label}: {ex.Message}");
        }
    }

    private void OnRoundTimerTick(object? sender, EventArgs e)
    {
        if (_remaining.TotalSeconds <= 1)
        {
            _roundTimer.Stop();
            _roundActive = false;
            _remaining = TimeSpan.Zero;
            LabelTimer.Content = FormatTime(_remaining);
            AppendLog("✓ Раунд завершён");
        }
        else
        {
            _remaining = _remaining - TimeSpan.FromSeconds(1);
            LabelTimer.Content = FormatTime(_remaining);
        }
    }

    private void OnDataReceived(byte[] data)
    {
        // Выполняется в потоке порта: маршаллинг в UI
        Dispatcher.Invoke(() =>
        {
            // Парсим как ASCII код "01", "11" ... если пришло не-текст, просто покажем hex
            var text = Encoding.ASCII.GetString(data);
            AppendLog($"← Пришло: {text.Trim()} ({BitConverter.ToString(data)})");

            if (_roundActive && !_firstColorCaptured)
            {
                var token = text.Trim();
                if (_colorMap.TryGetValue(token, out var colorName))
                {
                    LabelFirstColor.Content = colorName;
                    _firstColorCaptured = true;
                    AppendLog($"✓ Зафиксирована первая: {colorName}");
                }
            }
        });
    }

    private void OnMessageReceived(string msg)
    {
        // Дополнительная обработка строк, если нужно
    }

    private void OnSimulateClick(object sender, RoutedEventArgs e)
    {
        // Симулируем входящий код '01'
        var fake = Encoding.ASCII.GetBytes("01");
        OnDataReceived(fake);
    }

    private void OnClearLogClick(object sender, RoutedEventArgs e)
    {
        TextLog.Clear();
    }

    private void OnExitClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _settings = dlg.Settings;
            AppendLog($"Настройки обновлены: Round={_settings.RoundTimeSeconds}s, Lock={_settings.LockTimeSeconds}s, BlockAfter={_settings.BlockAfterRound}, ModeA='{_settings.ModeACode}', ModeB='{_settings.ModeBCode}'");

            // Отправим в контроллер параметры при сохранении
            if (_serial.IsOpen)
            {
                try
                {
                    _serial.SendSetting(1, (byte)_settings.LockTimeSeconds);
                    _serial.SendSetting(2, (byte)_settings.RoundTimeSeconds);
                    _serial.SendSetting(3, (byte)(_settings.BlockAfterRound ? 1 : 0));
                    AppendLog("→ Отправлены настройки (s + индекс + значение)");
                }
                catch (Exception ex)
                {
                    AppendLog($"! Ошибка отправки настроек: {ex.Message}");
                }
            }
        }
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Простое WPF-приложение для работы с COM-портом: старт раунда, режимы, индикация.", "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    protected override void OnClosed(EventArgs e)
    {
        _roundTimer.Stop();
        _serial.Disconnect();
        base.OnClosed(e);
    }
}