using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace hoerhilfe;

public partial class Form1 : Form
{
    private const int MicrophoneMode = 0;
    private const int SystemAudioMode = 1;
    private const double WizardStartDb = -80;
    private const double WizardMaxDb = -10;
    private const double WizardStepDb = 1.5;

    private static readonly int[] TestFrequencies = [250, 500, 1000, 2000, 3000, 4000, 6000, 8000];
    private static readonly string ProfileDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Hoerhilfe");
    private static readonly string ProfilePath = Path.Combine(ProfileDirectory, "profil.json");
    private static readonly string ProfilesDirectory = Path.Combine(ProfileDirectory, "Profiles");
    private static readonly string StatePath = Path.Combine(ProfileDirectory, "state.json");
    private const string DefaultProfileName = "Standard";

    private readonly BindingList<HearingBand> _bands = [];
    private readonly DataGridView _curveGrid = new();
    private readonly CurvePanel _curvePanel;
    private readonly ComboBox _profileCombo = CreateComboBox();
    private readonly ComboBox _themeCombo = CreateComboBox();
    private readonly Button _saveProfileButton = new();
    private readonly Button _saveAsProfileButton = new();
    private readonly Button _deleteProfileButton = new();
    private readonly Label _wizardFrequencyLabel = new();
    private readonly Label _wizardLevelLabel = new();
    private readonly Label _wizardHintLabel = new();
    private readonly ProgressBar _wizardProgress = new();
    private readonly Button _wizardStartButton = new();
    private readonly Button _wizardHeardButton = new();
    private readonly Button _wizardStopButton = new();
    private readonly System.Windows.Forms.Timer _wizardTimer = new() { Interval = 260 };
    private readonly ComboBox _sourceModeCombo = CreateComboBox();
    private readonly ComboBox _sourceDeviceCombo = CreateComboBox();
    private readonly ComboBox _outputDeviceCombo = CreateComboBox();
    private readonly TrackBar _masterGainTrack = new();
    private readonly Label _masterGainLabel = new();
    private readonly TrackBar _gateTrack = new();
    private readonly Label _gateLabel = new();
    private readonly Button _toggleSupportButton = new();
    private readonly Button _testOutputButton = new();
    private readonly Button _refreshAudioDevicesButton = new();
    private readonly Label _levelLabel = new();
    private readonly Label _statusLabel = new();
    private readonly ToolTip _toolTip = new() { AutoPopDelay = 15000, InitialDelay = 250, ReshowDelay = 100 };
    private readonly NotifyIcon _notifyIcon = new();
    private readonly ContextMenuStrip _trayMenu = new();

    private WasapiOut? _toneOut;
    private WasapiCapture? _capture;
    private WasapiOut? _supportOut;
    private BufferedWaveProvider? _buffer;
    private WaveFormat? _captureFormat;
    private EqualizerProcessor? _processor;
    private WizardToneSampleProvider? _wizardToneProvider;
    private int _wizardFrequencyIndex;
    private int _wizardEarIndex;
    private double _wizardLevelDb = WizardStartDb;
    private bool _wizardRunning;
    private bool _loadingProfiles;
    private string _currentProfileName = DefaultProfileName;
    private bool _darkMode;
    private bool _allowClose;
    private DateTime _lastLevelUiUpdate = DateTime.MinValue;
    private DateTime _lastSignalAt = DateTime.MinValue;
    private volatile bool _supportRunning;

    public Form1()
    {
        Text = "Hoerfix - Frequenzen verstaerken";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        KeyPreview = true;
        MinimumSize = new Size(1400, 900);
        Size = new Size(1500, 940);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        Icon = LoadAppIcon();
        KeyDown += FormKeyDown;
        _wizardTimer.Tick += WizardTimerTick;
        Resize += FormResize;

        _curvePanel = new CurvePanel(_bands);
        BuildBands();
        ConfigureTray();
        BuildUi();
        PopulateAudioDevices();
        LoadProfiles();
        UpdateComputedGains();
        UpdateLabels();
        ConfigureToolTips();
        ApplyTheme();
        UpdateUiState();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        StopSupport();
        StopWizard();
        StopTone();
        SaveProfile();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayMenu.Dispose();
        base.OnClosing(e);
    }

    private void ConfigureTray()
    {
        var openItem = new ToolStripMenuItem("Oeffnen", null, (_, _) => RestoreMainWindow());
        var stopItem = new ToolStripMenuItem("Hoerunterstuetzung stoppen", null, (_, _) => StopSupport());
        var exitItem = new ToolStripMenuItem("Beenden", null, (_, _) =>
        {
            _allowClose = true;
            Close();
        });
        _trayMenu.Items.Add(openItem);
        _trayMenu.Items.Add(stopItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(exitItem);

        _notifyIcon.Icon = LoadAppIcon();
        _notifyIcon.Text = "Hoerfix";
        _notifyIcon.ContextMenuStrip = _trayMenu;
        _notifyIcon.Visible = true;
        _notifyIcon.DoubleClick += (_, _) => RestoreMainWindow();
    }

    private void FormResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        _notifyIcon.BalloonTipTitle = "Hoerfix laeuft weiter";
        _notifyIcon.BalloonTipText = "Doppelklick auf das Tray-Symbol oeffnet das Fenster wieder.";
        _notifyIcon.ShowBalloonTip(1200);
    }

    public void RestoreMainWindow()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        BringToFront();
        Activate();
    }

    private static Icon LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "hoerfix.ico");
        return File.Exists(iconPath)
            ? new Icon(iconPath)
            : Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
    }

    private void BuildBands()
    {
        foreach (var frequency in TestFrequencies)
        {
            _bands.Add(new HearingBand
            {
                FrequencyHz = frequency,
                LeftThresholdDb = -45,
                RightThresholdDb = -45,
                LeftGainDb = 0,
                RightGainDb = 0
            });
        }
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            ColumnCount = 2,
            RowCount = 1,
            BackColor = BackColor
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        Controls.Add(root);

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.Controls.Add(left, 0, 0);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 320));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(right, 1, 0);

        left.Controls.Add(CreateIntroPanel(), 0, 0);
        left.Controls.Add(CreateCurveGrid(), 0, 1);
        left.Controls.Add(_curvePanel, 0, 2);

        right.Controls.Add(CreateHearingTestPanel(), 0, 0);
        right.Controls.Add(CreateSupportPanel(), 0, 1);
        right.Controls.Add(CreateSafetyPanel(), 0, 2);
        right.Controls.Add(CreateStatusPanel(), 0, 3);
    }

    private Control CreateIntroPanel()
    {
        var panel = CreatePanel();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(layout);

        var title = new Label
        {
            Text = "Hoerkurve aufnehmen",
            Dock = DockStyle.Fill,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 33, 46)
        };

        var profileRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        profileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        profileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _profileCombo.SelectedIndexChanged += ProfileComboSelectedIndexChanged;
        profileRow.Controls.Add(CreateMutedLabel("Profil"), 0, 0);
        profileRow.Controls.Add(_profileCombo, 1, 0);

        var themeRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        themeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        themeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _themeCombo.Items.Add("Hell");
        _themeCombo.Items.Add("Dunkel");
        _themeCombo.SelectedIndex = 0;
        _themeCombo.SelectedIndexChanged += (_, _) =>
        {
            _darkMode = _themeCombo.SelectedIndex == 1;
            ApplyTheme();
            if (!_loadingProfiles)
            {
                SaveProfile();
            }
        };
        themeRow.Controls.Add(CreateMutedLabel("Design"), 0, 0);
        themeRow.Controls.Add(_themeCombo, 1, 0);

        var buttonRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        _saveProfileButton.Text = "Speichern";
        _saveAsProfileButton.Text = "Speichern als";
        _deleteProfileButton.Text = "Loeschen";
        StyleSecondaryButton(_saveProfileButton);
        StyleSecondaryButton(_saveAsProfileButton);
        StyleSecondaryButton(_deleteProfileButton);
        _saveProfileButton.Click += (_, _) => SaveCurrentProfileWithStatus();
        _saveAsProfileButton.Click += (_, _) => SaveProfileAs();
        _deleteProfileButton.Click += (_, _) => DeleteCurrentProfile();
        buttonRow.Controls.Add(_saveProfileButton, 0, 0);
        buttonRow.Controls.Add(_saveAsProfileButton, 1, 0);
        buttonRow.Controls.Add(_deleteProfileButton, 2, 0);

        var text = new Label
        {
            Text = "Wizard misst links/rechts. Space druecken, sobald der Ton hoerbar ist.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(65, 74, 86)
        };

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(profileRow, 0, 1);
        layout.Controls.Add(themeRow, 0, 2);
        layout.Controls.Add(buttonRow, 0, 3);
        layout.Controls.Add(text, 0, 4);
        return panel;
    }

    private Control CreateCurveGrid()
    {
        _curveGrid.Dock = DockStyle.Fill;
        _curveGrid.AutoGenerateColumns = false;
        _curveGrid.DataSource = _bands;
        _curveGrid.AllowUserToAddRows = false;
        _curveGrid.AllowUserToDeleteRows = false;
        _curveGrid.RowHeadersVisible = false;
        _curveGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _curveGrid.MultiSelect = false;
        _curveGrid.BackgroundColor = Color.White;
        _curveGrid.BorderStyle = BorderStyle.None;
        _curveGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _curveGrid.CellEndEdit += (_, _) =>
        {
            UpdateComputedGains();
            SaveProfile();
        };

        _curveGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HearingBand.FrequencyHz),
            HeaderText = "Frequenz (Hz)",
            ReadOnly = true,
            FillWeight = 26
        });
        _curveGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HearingBand.LeftThresholdDb),
            HeaderText = "Links hoerbar",
            FillWeight = 28
        });
        _curveGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HearingBand.RightThresholdDb),
            HeaderText = "Rechts hoerbar",
            FillWeight = 28
        });
        _curveGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HearingBand.LeftGainDb),
            HeaderText = "Gain L",
            ReadOnly = true,
            FillWeight = 18
        });
        _curveGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(HearingBand.RightGainDb),
            HeaderText = "Gain R",
            ReadOnly = true,
            FillWeight = 18
        });

        return WrapWithTitle("Profil", _curveGrid);
    }

    private Control CreateHearingTestPanel()
    {
        var panel = CreatePanel();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        panel.Controls.Add(layout);

        _wizardFrequencyLabel.Dock = DockStyle.Fill;
        _wizardFrequencyLabel.Font = new Font(Font.FontFamily, 18F, FontStyle.Bold);
        _wizardFrequencyLabel.TextAlign = ContentAlignment.MiddleLeft;
        _wizardFrequencyLabel.ForeColor = Color.FromArgb(22, 33, 46);

        _wizardLevelLabel.Dock = DockStyle.Fill;
        _wizardLevelLabel.TextAlign = ContentAlignment.MiddleLeft;
        _wizardLevelLabel.ForeColor = Color.FromArgb(65, 74, 86);

        _wizardHintLabel.Dock = DockStyle.Fill;
        _wizardHintLabel.ForeColor = Color.FromArgb(65, 74, 86);
        _wizardHintLabel.Text = "Start druecken. Erst linkes Ohr, dann rechtes Ohr. Sobald du den Ton hoerst: Space.";

        _wizardProgress.Dock = DockStyle.Fill;
        _wizardProgress.Minimum = 0;
        _wizardProgress.Maximum = TestFrequencies.Length * 2;

        _wizardStartButton.Text = "Wizard starten";
        StyleActionButton(_wizardStartButton, Color.FromArgb(34, 111, 84));
        _wizardStartButton.Click += (_, _) => StartWizard();

        _wizardHeardButton.Text = "Gehoert (Space)";
        StyleActionButton(_wizardHeardButton, Color.FromArgb(27, 100, 156));
        _wizardHeardButton.Enabled = false;
        _wizardHeardButton.Click += (_, _) => MarkWizardFrequencyHeard();

        _wizardStopButton.Text = "Stop";
        StyleActionButton(_wizardStopButton, Color.FromArgb(158, 55, 55));
        _wizardStopButton.Enabled = false;
        _wizardStopButton.Click += (_, _) => StopWizard();

        var buttonRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        buttonRow.Controls.Add(_wizardStartButton, 0, 0);
        buttonRow.Controls.Add(_wizardHeardButton, 1, 0);
        buttonRow.Controls.Add(_wizardStopButton, 2, 0);

        layout.Controls.Add(CreateMutedLabel("Aktuell"), 0, 0);
        layout.Controls.Add(_wizardFrequencyLabel, 1, 0);
        layout.Controls.Add(CreateMutedLabel("Pegel"), 0, 1);
        layout.Controls.Add(_wizardLevelLabel, 1, 1);
        layout.Controls.Add(CreateMutedLabel("Fortschritt"), 0, 2);
        layout.Controls.Add(_wizardProgress, 1, 2);
        layout.Controls.Add(_wizardHintLabel, 1, 3);
        layout.Controls.Add(buttonRow, 1, 4);

        UpdateWizardLabels();

        return WrapWithTitle("Hoerkurven-Wizard", panel);
    }

    private Control CreateSupportPanel()
    {
        var panel = CreatePanel();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        panel.Controls.Add(layout);

        _sourceModeCombo.Items.Add("Mikrofon");
        _sourceModeCombo.Items.Add("Systemton (Film/Browser)");
        _sourceModeCombo.SelectedIndex = SystemAudioMode;
        _sourceModeCombo.SelectedIndexChanged += (_, _) => PopulateSourceDevices();

        _sourceDeviceCombo.SelectedIndexChanged += (_, _) => UpdateDeviceTooltips();
        _outputDeviceCombo.SelectedIndexChanged += (_, _) => UpdateDeviceTooltips();

        _masterGainTrack.Minimum = -12;
        _masterGainTrack.Maximum = 12;
        _masterGainTrack.Value = 0;
        _masterGainTrack.TickFrequency = 3;
        _masterGainTrack.ValueChanged += (_, _) => UpdateLabels();

        _gateTrack.Minimum = -80;
        _gateTrack.Maximum = -25;
        _gateTrack.Value = -62;
        _gateTrack.TickFrequency = 5;
        _gateTrack.ValueChanged += (_, _) => UpdateLabels();

        _toggleSupportButton.Text = "Starten";
        StyleActionButton(_toggleSupportButton, Color.FromArgb(34, 111, 84));
        _toggleSupportButton.Click += (_, _) =>
        {
            if (_supportRunning)
            {
                StopSupport();
            }
            else
            {
                StartSupport();
            }
        };

        _testOutputButton.Text = "Ausgabe testen";
        StyleSecondaryButton(_testOutputButton);
        _testOutputButton.Click += (_, _) => PlayOutputTestTone();

        _refreshAudioDevicesButton.Text = "Geraete aktualisieren";
        StyleSecondaryButton(_refreshAudioDevicesButton);
        _refreshAudioDevicesButton.Click += (_, _) => RefreshAudioDevices();

        _levelLabel.Dock = DockStyle.Fill;
        _levelLabel.TextAlign = ContentAlignment.MiddleLeft;
        _levelLabel.ForeColor = Color.FromArgb(65, 74, 86);
        _levelLabel.Text = "Pegel: noch kein Signal gemessen";

        var actionRow = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 3 };
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        actionRow.Controls.Add(_toggleSupportButton, 0, 0);
        actionRow.Controls.Add(_testOutputButton, 1, 0);
        actionRow.Controls.Add(_refreshAudioDevicesButton, 2, 0);

        layout.Controls.Add(CreateMutedLabel("Modus"), 0, 0);
        layout.Controls.Add(_sourceModeCombo, 1, 0);
        layout.Controls.Add(CreateMutedLabel("Quelle"), 0, 1);
        layout.Controls.Add(_sourceDeviceCombo, 1, 1);
        layout.Controls.Add(CreateMutedLabel("Ausgabe"), 0, 2);
        layout.Controls.Add(_outputDeviceCombo, 1, 2);
        layout.Controls.Add(actionRow, 1, 3);
        layout.Controls.Add(CreateMutedLabel("Signal"), 0, 4);
        layout.Controls.Add(_levelLabel, 1, 4);
        layout.Controls.Add(CreateMutedLabel("Gesamt-Gain"), 0, 5);
        layout.Controls.Add(CreateSliderValueRow(_masterGainTrack, _masterGainLabel), 1, 5);
        layout.Controls.Add(CreateMutedLabel("Rauschschwelle"), 0, 6);
        layout.Controls.Add(CreateSliderValueRow(_gateTrack, _gateLabel), 1, 6);

        return WrapWithTitle("Live-Unterstuetzung", panel);
    }

    private static Control CreateSliderValueRow(TrackBar trackBar, Label valueLabel)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
        trackBar.Dock = DockStyle.Fill;
        trackBar.Margin = new Padding(0, 0, 8, 0);
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.TextAlign = ContentAlignment.MiddleLeft;
        row.Controls.Add(trackBar, 0, 0);
        row.Controls.Add(valueLabel, 1, 0);
        return row;
    }

    private Control CreateSafetyPanel()
    {
        var panel = CreatePanel();
        var text = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(70, 77, 89),
            Text = "Sicherheitsregeln: leise starten, Kopfhoerer verwenden, bei Schmerz oder Pfeifen sofort stoppen."
        };
        panel.Controls.Add(text);
        return WrapWithTitle("Sicherheit", panel);
    }

    private Control CreateStatusPanel()
    {
        var panel = CreatePanel();
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.ForeColor = Color.FromArgb(48, 58, 69);
        _statusLabel.Text = "Bereit.";
        panel.Controls.Add(_statusLabel);
        return panel;
    }

    private static Panel CreatePanel() => new()
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(10),
        Margin = new Padding(5),
        BackColor = Color.White
    };

    private Control WrapWithTitle(string title, Control content)
    {
        var panel = CreatePanel();
        var label = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font(Font, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 33, 46)
        };
        content.Dock = DockStyle.Fill;
        panel.Controls.Add(content);
        panel.Controls.Add(label);
        return panel;
    }

    private void ApplyTheme()
    {
        var appBack = _darkMode ? Color.FromArgb(24, 28, 34) : Color.FromArgb(245, 247, 250);
        var panelBack = _darkMode ? Color.FromArgb(35, 40, 48) : Color.White;
        var text = _darkMode ? Color.FromArgb(232, 236, 241) : Color.FromArgb(22, 33, 46);
        var muted = _darkMode ? Color.FromArgb(184, 192, 203) : Color.FromArgb(65, 74, 86);
        var inputBack = _darkMode ? Color.FromArgb(47, 54, 64) : Color.White;

        BackColor = appBack;
        ApplyThemeToControl(this, panelBack, text, muted, inputBack);
        _curvePanel.SetDarkMode(_darkMode);
        _curvePanel.Invalidate();
    }

    private void ApplyThemeToControl(Control control, Color panelBack, Color text, Color muted, Color inputBack)
    {
        foreach (Control child in control.Controls)
        {
            switch (child)
            {
                case Button button when button == _toggleSupportButton:
                    button.ForeColor = Color.White;
                    break;
                case Button button when button == _wizardStartButton || button == _wizardHeardButton || button == _wizardStopButton:
                    button.ForeColor = Color.White;
                    break;
                case Button button:
                    button.BackColor = _darkMode ? Color.FromArgb(58, 66, 78) : Color.FromArgb(235, 239, 244);
                    button.ForeColor = text;
                    break;
                case ComboBox combo:
                    combo.BackColor = inputBack;
                    combo.ForeColor = text;
                    break;
                case DataGridView grid:
                    grid.BackgroundColor = panelBack;
                    grid.DefaultCellStyle.BackColor = panelBack;
                    grid.DefaultCellStyle.ForeColor = text;
                    grid.DefaultCellStyle.SelectionBackColor = _darkMode ? Color.FromArgb(49, 104, 156) : Color.FromArgb(0, 120, 215);
                    grid.DefaultCellStyle.SelectionForeColor = Color.White;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = _darkMode ? Color.FromArgb(47, 54, 64) : Color.FromArgb(230, 235, 241);
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = text;
                    grid.EnableHeadersVisualStyles = false;
                    break;
                case TrackBar:
                    child.BackColor = panelBack;
                    break;
                case Label label:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = label.Font.Bold ? text : muted;
                    break;
                case Panel or TableLayoutPanel:
                    child.BackColor = panelBack;
                    break;
                default:
                    child.BackColor = panelBack;
                    child.ForeColor = text;
                    break;
            }

            ApplyThemeToControl(child, panelBack, text, muted, inputBack);
        }
    }

    private static Label CreateMutedLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(86, 96, 108)
    };

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Height = 36,
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(235, 239, 244),
        FlatStyle = FlatStyle.Flat,
        Margin = new Padding(3, 6, 3, 3)
    };

    private static void StyleActionButton(Button button, Color backColor)
    {
        button.Dock = DockStyle.Fill;
        button.Height = 38;
        button.Margin = new Padding(2);
        button.BackColor = backColor;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.UseVisualStyleBackColor = false;
    }

    private static void StyleSecondaryButton(Button button)
    {
        button.Dock = DockStyle.Fill;
        button.Height = 38;
        button.Margin = new Padding(2);
        button.BackColor = Color.FromArgb(235, 239, 244);
        button.ForeColor = Color.FromArgb(22, 33, 46);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.UseVisualStyleBackColor = false;
    }

    private static ComboBox CreateComboBox() => new()
    {
        Dock = DockStyle.Fill,
        DropDownStyle = ComboBoxStyle.DropDownList,
        IntegralHeight = false,
        MaxDropDownItems = 14,
        MinimumSize = new Size(360, 0)
    };

    private void PopulateAudioDevices()
    {
        PopulateSourceDevices();
        PopulateOutputDevices();
    }

    private void RefreshAudioDevices()
    {
        try
        {
            PopulateAudioDevices();
            SetStatus($"Audiogeraete aktualisiert: {_sourceDeviceCombo.Items.Count} Quellen, {_outputDeviceCombo.Items.Count} Ausgaben gefunden.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Audiogeraete konnten nicht aktualisiert werden:\r\n{ex.Message}", "Hoerhilfe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PopulateSourceDevices()
    {
        var oldId = (_sourceDeviceCombo.SelectedItem as AudioDeviceItem)?.Device.ID;
        _sourceDeviceCombo.Items.Clear();

        var dataFlow = _sourceModeCombo.SelectedIndex == MicrophoneMode ? DataFlow.Capture : DataFlow.Render;
        foreach (var device in GetActiveDevices(dataFlow))
        {
            _sourceDeviceCombo.Items.Add(new AudioDeviceItem(device));
        }

        SelectDeviceById(_sourceDeviceCombo, oldId);
        SetComboDropDownWidth(_sourceDeviceCombo);
        UpdateDeviceTooltips();
    }

    private void PopulateOutputDevices()
    {
        var oldId = (_outputDeviceCombo.SelectedItem as AudioDeviceItem)?.Device.ID;
        _outputDeviceCombo.Items.Clear();

        foreach (var device in GetActiveDevices(DataFlow.Render))
        {
            _outputDeviceCombo.Items.Add(new AudioDeviceItem(device));
        }

        SelectDeviceById(_outputDeviceCombo, oldId);
        SetComboDropDownWidth(_outputDeviceCombo);
        UpdateDeviceTooltips();
    }

    private static IEnumerable<MMDevice> GetActiveDevices(DataFlow dataFlow)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active).ToArray();
    }

    private static void SelectDeviceById(ComboBox combo, string? deviceId)
    {
        if (combo.Items.Count == 0)
        {
            return;
        }

        for (var i = 0; i < combo.Items.Count; i++)
        {
            if ((combo.Items[i] as AudioDeviceItem)?.Device.ID == deviceId)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        combo.SelectedIndex = 0;
    }

    private static void SetComboDropDownWidth(ComboBox combo)
    {
        var width = combo.Width;
        foreach (var item in combo.Items)
        {
            width = Math.Max(width, TextRenderer.MeasureText(item.ToString(), combo.Font).Width + 48);
        }

        combo.DropDownWidth = Math.Min(Math.Max(width, 520), 1100);
    }

    private void UpdateDeviceTooltips()
    {
        _toolTip.SetToolTip(_sourceDeviceCombo, _sourceDeviceCombo.SelectedItem?.ToString() ?? "");
        _toolTip.SetToolTip(_outputDeviceCombo, _outputDeviceCombo.SelectedItem?.ToString() ?? "");
        UpdateUiState();
    }

    private void StartWizard()
    {
        if (_outputDeviceCombo.SelectedItem is not AudioDeviceItem output)
        {
            MessageBox.Show("Keine Audioausgabe gefunden.", "Hoerhilfe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            StopSupport();
            StopWizard();

            _wizardRunning = true;
            _wizardFrequencyIndex = 0;
            _wizardEarIndex = 0;
            _wizardProgress.Value = 0;
            StartWizardTone(output);
            SetWizardButtons(true);
            SetStatus("Wizard laeuft: linkes Ohr. Sobald du den Ton hoerst: Space druecken.");
        }
        catch (Exception ex)
        {
            StopWizard();
            MessageBox.Show($"Wizard konnte nicht gestartet werden:\r\n{ex.Message}", "Hoerhilfe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void StartWizardTone(AudioDeviceItem output)
    {
        StopTone();
        _wizardLevelDb = WizardStartDb;
        _wizardToneProvider = new WizardToneSampleProvider(
            TestFrequencies[_wizardFrequencyIndex],
            DbToGain(_wizardLevelDb),
            _wizardEarIndex);
        _toneOut = new WasapiOut(output.Device, AudioClientShareMode.Shared, false, 90);
        _toneOut.Init(_wizardToneProvider.ToWaveProvider());
        _toneOut.Play();
        _wizardTimer.Start();
        UpdateWizardLabels();
    }

    private void StopWizard()
    {
        _wizardTimer.Stop();
        _wizardRunning = false;
        _wizardToneProvider = null;
        StopTone();
        SetWizardButtons(false);
        UpdateWizardLabels();
    }

    private void WizardTimerTick(object? sender, EventArgs e)
    {
        if (!_wizardRunning || _wizardToneProvider is null)
        {
            return;
        }

        _wizardLevelDb = Math.Min(WizardMaxDb, _wizardLevelDb + WizardStepDb);
        _wizardToneProvider.SetLevel(DbToGain(_wizardLevelDb));
        UpdateWizardLabels();

        if (_wizardLevelDb >= WizardMaxDb)
        {
            MarkWizardFrequencyHeard(autoAtMaximum: true);
        }
    }

    private void MarkWizardFrequencyHeard(bool autoAtMaximum = false)
    {
        if (!_wizardRunning)
        {
            return;
        }

        var frequency = TestFrequencies[_wizardFrequencyIndex];
        var band = _bands.First(b => b.FrequencyHz == frequency);
        if (_wizardEarIndex == 0)
        {
            band.LeftThresholdDb = Math.Round(_wizardLevelDb, 1);
        }
        else
        {
            band.RightThresholdDb = Math.Round(_wizardLevelDb, 1);
        }
        UpdateComputedGains();
        SaveProfile();
        _curveGrid.ClearSelection();
        _curveGrid.Rows[_wizardFrequencyIndex].Selected = true;
        _curveGrid.FirstDisplayedScrollingRowIndex = _wizardFrequencyIndex;
        _curvePanel.Invalidate();

        var ear = WizardEarName();
        var status = autoAtMaximum
            ? $"{ear} {frequency} Hz bei Maximum gespeichert. Naechste Messung startet."
            : $"{ear} {frequency} Hz bei {_wizardLevelDb:0.#} dB gespeichert. Naechste Messung startet.";
        SetStatus(status);

        AdvanceWizardStep();
        _wizardProgress.Value = Math.Min(_wizardEarIndex * TestFrequencies.Length + _wizardFrequencyIndex, TestFrequencies.Length * 2);

        if (_wizardEarIndex >= 2)
        {
            StopWizard();
            _wizardProgress.Value = TestFrequencies.Length * 2;
            _wizardHintLabel.Text = "Fertig. Beide Ohren sind gespeichert und werden getrennt verstaerkt.";
            SetStatus("Hoerkurve fuer links und rechts fertig. Jetzt Live-Unterstuetzung mit Systemton starten.");
            return;
        }

        if (_outputDeviceCombo.SelectedItem is AudioDeviceItem output)
        {
            StartWizardTone(output);
        }
    }

    private void FormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Space || !_wizardRunning)
        {
            return;
        }

        e.SuppressKeyPress = true;
        MarkWizardFrequencyHeard();
    }

    private void UpdateWizardLabels()
    {
        var frequency = TestFrequencies[Math.Min(_wizardFrequencyIndex, TestFrequencies.Length - 1)];
        _wizardFrequencyLabel.Text = _wizardRunning
            ? $"{WizardEarName()} - {frequency} Hz"
            : "Bereit";
        _wizardLevelLabel.Text = _wizardRunning
            ? $"{_wizardLevelDb:0.#} dBFS"
            : $"Start bei {WizardStartDb:0.#} dBFS, Maximum {WizardMaxDb:0.#} dBFS";

        if (_wizardRunning)
        {
            _wizardHintLabel.Text = $"Ton nur {WizardEarName().ToLowerInvariant()}. Wenn du ihn hoerst: Space druecken.";
        }
    }

    private void AdvanceWizardStep()
    {
        _wizardFrequencyIndex++;
        if (_wizardFrequencyIndex < TestFrequencies.Length)
        {
            return;
        }

        _wizardFrequencyIndex = 0;
        _wizardEarIndex++;
    }

    private string WizardEarName() => _wizardEarIndex == 0 ? "Links" : "Rechts";

    private void SetWizardButtons(bool running)
    {
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        var hasOutput = _outputDeviceCombo.SelectedItem is AudioDeviceItem;
        var hasSource = _sourceDeviceCombo.SelectedItem is AudioDeviceItem;
        var busy = _wizardRunning || _supportRunning;

        _wizardStartButton.Enabled = !_wizardRunning && !_supportRunning && hasOutput;
        _wizardHeardButton.Enabled = _wizardRunning;
        _wizardStopButton.Enabled = _wizardRunning;

        _toggleSupportButton.Enabled = !_wizardRunning && hasSource && hasOutput;
        _testOutputButton.Enabled = !_wizardRunning && !_supportRunning && hasOutput;
        _refreshAudioDevicesButton.Enabled = !busy;

        _profileCombo.Enabled = !busy;
        _saveProfileButton.Enabled = !busy && _profileCombo.Items.Count > 0;
        _saveAsProfileButton.Enabled = !busy;
        _deleteProfileButton.Enabled = !busy && _profileCombo.Items.Count > 0;

        _sourceModeCombo.Enabled = !busy;
        _sourceDeviceCombo.Enabled = !busy;
        _outputDeviceCombo.Enabled = !busy;
        _curveGrid.Enabled = !busy;

        _masterGainTrack.Enabled = !_wizardRunning;
        _gateTrack.Enabled = !_wizardRunning;
    }

    private void ConfigureToolTips()
    {
        _toolTip.SetToolTip(_profileCombo, "Gespeichertes Hoerprofil auswaehlen.");
        _toolTip.SetToolTip(_saveProfileButton, "Speichert die aktuelle Hoerkurve im ausgewaehlten Profil.");
        _toolTip.SetToolTip(_saveAsProfileButton, "Legt ein neues Profil mit den aktuellen Werten an.");
        _toolTip.SetToolTip(_deleteProfileButton, "Loescht das ausgewaehlte Profil nach Rueckfrage.");
        _toolTip.SetToolTip(_wizardStartButton, "Startet die Messung: erst linkes Ohr, dann rechtes Ohr.");
        _toolTip.SetToolTip(_wizardHeardButton, "Druecken, sobald der Ton im aktiven Ohr gerade hoerbar ist. Space funktioniert auch.");
        _toolTip.SetToolTip(_wizardStopButton, "Bricht die laufende Hoerkurvenmessung ab.");
        _toolTip.SetToolTip(_sourceModeCombo, "Systemton fuer Filme/Browser, Mikrofon fuer Umgebungsgeraeusche.");
        _toolTip.SetToolTip(_sourceDeviceCombo, "Audioquelle, die aufgenommen und verstaerkt wird.");
        _toolTip.SetToolTip(_outputDeviceCombo, "Zielgeraet, auf dem der verstaerkte Ton ausgegeben wird.");
        _toolTip.SetToolTip(_toggleSupportButton, "Startet oder stoppt die Live-Verstaerkung.");
        _toolTip.SetToolTip(_testOutputButton, "Spielt einen kurzen Testton auf dem ausgewaehlten Ausgabegeraet.");
        _toolTip.SetToolTip(_refreshAudioDevicesButton, "Liest Mikrofone, Systemton-Quellen und Ausgabegeraete neu ein.");
        _toolTip.SetToolTip(_masterGainTrack, "Gesamtlautstaerke nach der Hoerkurven-Korrektur. Positiv ist lauter, negativ leiser.");
        _toolTip.SetToolTip(_masterGainLabel, "Aktuelle Gesamtverstaerkung in Dezibel.");
        _toolTip.SetToolTip(_gateTrack, "Rauschschwelle: sehr leise Eingangssignale unter diesem Wert werden abgesenkt.");
        _toolTip.SetToolTip(_gateLabel, "Aktuelle Rauschschwelle in Dezibel.");
        _toolTip.SetToolTip(_levelLabel, "Zeigt, ob an der Quelle Signal ankommt und wie stark die Ausgabe ist.");
    }

    private void StopTone()
    {
        if (_toneOut is null)
        {
            return;
        }

        var tone = _toneOut;
        _toneOut = null;
        tone.Stop();
        tone.Dispose();
    }

    private void PlayOutputTestTone()
    {
        StopTone();
        if (_outputDeviceCombo.SelectedItem is not AudioDeviceItem output)
        {
            MessageBox.Show("Keine Audioausgabe gefunden.", "Hoerhilfe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _wizardToneProvider = new WizardToneSampleProvider(700, DbToGain(-24), -1);
            _toneOut = new WasapiOut(output.Device, AudioClientShareMode.Shared, false, 90);
            _toneOut.Init(_wizardToneProvider.ToWaveProvider());
            _toneOut.Play();
            _testOutputButton.Enabled = false;
            SetStatus($"Ausgabe-Testton laeuft auf: {output.Device.FriendlyName}");
            var stopTimer = new System.Windows.Forms.Timer { Interval = 900 };
            stopTimer.Tick += (_, _) =>
            {
                stopTimer.Stop();
                stopTimer.Dispose();
                StopTone();
                UpdateUiState();
            };
            stopTimer.Start();
        }
        catch (Exception ex)
        {
            StopTone();
            MessageBox.Show($"Ausgabe-Testton konnte nicht gestartet werden:\r\n{ex.Message}", "Hoerhilfe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void UpdateComputedGains()
    {
        foreach (var band in _bands)
        {
            var leftLossRelativeToComfortable = band.LeftThresholdDb + 45.0;
            var rightLossRelativeToComfortable = band.RightThresholdDb + 45.0;
            band.LeftGainDb = Math.Round(Clamp(leftLossRelativeToComfortable * 0.65, 0, 18), 1);
            band.RightGainDb = Math.Round(Clamp(rightLossRelativeToComfortable * 0.65, 0, 18), 1);
        }

        _curveGrid.Refresh();
        _curvePanel.Invalidate();
        _processor?.UpdateBands(_bands, _masterGainTrack.Value, _gateTrack.Value);
    }

    private void StartSupport()
    {
        if (_sourceDeviceCombo.SelectedItem is not AudioDeviceItem source ||
            _outputDeviceCombo.SelectedItem is not AudioDeviceItem output)
        {
            MessageBox.Show("Bitte Quelle und Ausgabe auswaehlen.", "Hoerhilfe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_sourceModeCombo.SelectedIndex == SystemAudioMode && source.Device.ID == output.Device.ID)
        {
            var result = MessageBox.Show(
                "Quelle und Ausgabe sind identisch. Das kann den bereits verstaerkten Ton erneut aufnehmen.\r\n\r\n" +
                "Besser: Film/Browser auf ein anderes Ausgabegeraet oder ein virtuelles Kabel legen und als Ausgabe den Kopfhoerer waehlen.\r\n\r\n" +
                "Trotzdem starten?",
                "Hoerhilfe",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        try
        {
            StopTone();
            StopSupport();

            _capture = _sourceModeCombo.SelectedIndex == SystemAudioMode
                ? new WasapiLoopbackCapture(source.Device)
                : new WasapiCapture(source.Device);

            _captureFormat = _capture.WaveFormat;
            _processor = new EqualizerProcessor(
                _captureFormat.SampleRate,
                Math.Max(1, _captureFormat.Channels),
                _bands,
                _masterGainTrack.Value,
                _gateTrack.Value);
            _buffer = new BufferedWaveProvider(_captureFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(700),
                DiscardOnBufferOverflow = true
            };

            _supportOut = new WasapiOut(output.Device, AudioClientShareMode.Shared, false, 90);
            _supportOut.Init(_buffer);

            _capture.DataAvailable += CaptureDataAvailable;
            _capture.RecordingStopped += (_, args) =>
            {
                if (args.Exception is not null)
                {
                    BeginInvoke(() => SetStatus($"Audio gestoppt: {args.Exception.Message}"));
                }
            };

            _supportOut.Play();
            _capture.StartRecording();
            _supportRunning = true;
            _lastSignalAt = DateTime.Now;
            _levelLabel.Text = "Pegel: warte auf Signal...";
            _toggleSupportButton.Text = "Stoppen";
            _toggleSupportButton.BackColor = Color.FromArgb(158, 55, 55);
            UpdateUiState();
            SetStatus(_sourceModeCombo.SelectedIndex == SystemAudioMode
                ? "Systemton-Unterstuetzung aktiv. Film/Browser muss auf die Quelle ausgeben."
                : "Mikrofon-Unterstuetzung aktiv. Bei Rueckkopplung sofort stoppen.");
        }
        catch (Exception ex)
        {
            StopSupport();
            MessageBox.Show($"Hoerunterstuetzung konnte nicht gestartet werden:\r\n{ex.Message}", "Hoerhilfe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void StopSupport()
    {
        _supportRunning = false;

        if (_capture is not null)
        {
            _capture.DataAvailable -= CaptureDataAvailable;
            _capture.StopRecording();
            _capture.Dispose();
            _capture = null;
        }

        _supportOut?.Stop();
        _supportOut?.Dispose();
        _supportOut = null;
        _buffer = null;
        _captureFormat = null;
        _processor = null;

        _toggleSupportButton.Text = "Starten";
        _toggleSupportButton.BackColor = Color.FromArgb(34, 111, 84);
        _levelLabel.Text = "Pegel: gestoppt";
        UpdateUiState();
        SetStatus("Bereit.");
    }

    private void CaptureDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_buffer is null || _processor is null || _captureFormat is null)
        {
            return;
        }

        var processed = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, processed, 0, e.BytesRecorded);

        float inputPeak = 0;
        float outputPeak = 0;

        if (_captureFormat.Encoding == WaveFormatEncoding.IeeeFloat && _captureFormat.BitsPerSample == 32)
        {
            ProcessFloat32(processed, e.BytesRecorded, _captureFormat.Channels, _processor, out inputPeak, out outputPeak);
        }
        else if (_captureFormat.Encoding == WaveFormatEncoding.Pcm && _captureFormat.BitsPerSample == 16)
        {
            ProcessPcm16(processed, e.BytesRecorded, _captureFormat.Channels, _processor, out inputPeak, out outputPeak);
        }

        UpdateSignalStatus(inputPeak, outputPeak);
        _buffer.AddSamples(processed, 0, processed.Length);
    }

    private static void ProcessFloat32(
        byte[] buffer,
        int byteCount,
        int channels,
        EqualizerProcessor processor,
        out float inputPeak,
        out float outputPeak)
    {
        inputPeak = 0;
        outputPeak = 0;
        var sampleIndex = 0;
        for (var offset = 0; offset <= byteCount - 4; offset += 4)
        {
            var channel = sampleIndex % channels;
            var sample = BitConverter.ToSingle(buffer, offset);
            inputPeak = Math.Max(inputPeak, Math.Abs(sample));
            var filtered = processor.Process(sample, channel);
            outputPeak = Math.Max(outputPeak, Math.Abs(filtered));
            var bytes = BitConverter.GetBytes(Clamp(filtered, -0.95f, 0.95f));
            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
            sampleIndex++;
        }
    }

    private static void ProcessPcm16(
        byte[] buffer,
        int byteCount,
        int channels,
        EqualizerProcessor processor,
        out float inputPeak,
        out float outputPeak)
    {
        inputPeak = 0;
        outputPeak = 0;
        var sampleIndex = 0;
        for (var offset = 0; offset <= byteCount - 2; offset += 2)
        {
            var channel = sampleIndex % channels;
            var sample16 = (short)(buffer[offset] | (buffer[offset + 1] << 8));
            var sample = sample16 / 32768f;
            inputPeak = Math.Max(inputPeak, Math.Abs(sample));
            var filtered = processor.Process(sample, channel);
            outputPeak = Math.Max(outputPeak, Math.Abs(filtered));
            var output = (short)Math.Round(Clamp(filtered, -0.95f, 0.95f) * short.MaxValue);
            buffer[offset] = (byte)(output & 0xff);
            buffer[offset + 1] = (byte)((output >> 8) & 0xff);
            sampleIndex++;
        }
    }

    private void UpdateSignalStatus(float inputPeak, float outputPeak)
    {
        if (!_supportRunning)
        {
            return;
        }

        var now = DateTime.Now;
        if (inputPeak > 0.0008f)
        {
            _lastSignalAt = now;
        }

        if ((now - _lastLevelUiUpdate).TotalMilliseconds < 250)
        {
            return;
        }

        _lastLevelUiUpdate = now;
        var inputDb = LinearToDb(inputPeak);
        var outputDb = LinearToDb(outputPeak);
        BeginInvoke(() =>
        {
            _levelLabel.Text = $"Quelle {inputDb:0} dBFS -> Ausgabe {outputDb:0} dBFS";
            if ((DateTime.Now - _lastSignalAt).TotalSeconds > 2.0)
            {
                SetStatus("Kein Eingangssignal an der Quelle. Chrome-Ausgabe, Windows-Lautstaerke und Quellgeraet pruefen.");
            }
        });
    }

    private void UpdateLabels()
    {
        _masterGainLabel.Text = $"{_masterGainTrack.Value:+0;-0;0} dB";
        _gateLabel.Text = $"{_gateTrack.Value} dB";
        _processor?.UpdateBands(_bands, _masterGainTrack.Value, _gateTrack.Value);
    }

    private void SaveProfile()
    {
        try
        {
            Directory.CreateDirectory(ProfilesDirectory);
            var profile = new HearingProfile
            {
                Name = _currentProfileName,
                Bands = _bands.ToList(),
                MasterGainDb = _masterGainTrack.Value,
                NoiseGateDb = _gateTrack.Value,
                SourceMode = _sourceModeCombo.SelectedIndex,
                SourceDeviceId = (_sourceDeviceCombo.SelectedItem as AudioDeviceItem)?.Device.ID,
                OutputDeviceId = (_outputDeviceCombo.SelectedItem as AudioDeviceItem)?.Device.ID,
                Theme = _darkMode ? "dark" : "light"
            };
            File.WriteAllText(GetProfilePath(_currentProfileName), JsonSerializer.Serialize(profile, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }));
        }
        catch
        {
            // Profile persistence must not interrupt audio use.
        }
    }

    private void LoadProfiles(string? selectProfileName = null)
    {
        try
        {
            Directory.CreateDirectory(ProfilesDirectory);
            MigrateLegacyProfileIfNeeded();

            var profileNames = Directory.GetFiles(ProfilesDirectory, "*.json")
                .Select(ReadProfileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            if (profileNames.Count == 0)
            {
                _currentProfileName = DefaultProfileName;
                SaveProfile();
                profileNames.Add(DefaultProfileName);
            }

            var selectedName = selectProfileName ?? ReadLastProfileName();
            if (string.IsNullOrWhiteSpace(selectedName) || !profileNames.Contains(selectedName, StringComparer.OrdinalIgnoreCase))
            {
                selectedName = profileNames.Contains(DefaultProfileName, StringComparer.OrdinalIgnoreCase)
                    ? DefaultProfileName
                    : profileNames[0];
            }

            _loadingProfiles = true;
            _profileCombo.Items.Clear();
            foreach (var profileName in profileNames)
            {
                _profileCombo.Items.Add(profileName);
            }
            _profileCombo.SelectedItem = profileNames.First(name => string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase));
            _loadingProfiles = false;

            LoadProfile(selectedName);
            SetProfileButtonsEnabled();
        }
        catch
        {
            _loadingProfiles = false;
            _currentProfileName = DefaultProfileName;
            SetStatus("Profile konnten nicht geladen werden; Standardwerte aktiv.");
        }
    }

    private void MigrateLegacyProfileIfNeeded()
    {
        if (!File.Exists(ProfilePath) || File.Exists(GetProfilePath(DefaultProfileName)))
        {
            return;
        }

        try
        {
            var legacy = JsonSerializer.Deserialize<HearingProfile>(File.ReadAllText(ProfilePath));
            if (legacy is null)
            {
                return;
            }

            legacy.Name = DefaultProfileName;
            File.WriteAllText(GetProfilePath(DefaultProfileName), JsonSerializer.Serialize(legacy, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }));
        }
        catch
        {
            // Migration is best-effort; defaults are still usable.
        }
    }

    private static string ReadProfileName(string path)
    {
        try
        {
            var profile = JsonSerializer.Deserialize<HearingProfile>(File.ReadAllText(path));
            return string.IsNullOrWhiteSpace(profile?.Name)
                ? Path.GetFileNameWithoutExtension(path)
                : profile.Name.Trim();
        }
        catch
        {
            return Path.GetFileNameWithoutExtension(path);
        }
    }

    private void ProfileComboSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_loadingProfiles || _profileCombo.SelectedItem is not string selectedProfile)
        {
            return;
        }

        SaveProfile();
        StopWizard();
        LoadProfile(selectedProfile);
    }

    private void LoadProfile(string profileName)
    {
        var path = GetProfilePath(profileName);
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var profile = JsonSerializer.Deserialize<HearingProfile>(File.ReadAllText(path));
            if (profile is null)
            {
                return;
            }

            _loadingProfiles = true;
            _currentProfileName = string.IsNullOrWhiteSpace(profile.Name) ? profileName : profile.Name.Trim();
            _darkMode = string.Equals(profile.Theme, "dark", StringComparison.OrdinalIgnoreCase);
            _themeCombo.SelectedIndex = _darkMode ? 1 : 0;
            foreach (var saved in profile.Bands)
            {
                var band = _bands.FirstOrDefault(b => b.FrequencyHz == saved.FrequencyHz);
                if (band is not null)
                {
                    if (saved.ThresholdDb.HasValue)
                    {
                        saved.LeftThresholdDb = saved.ThresholdDb.Value;
                        saved.RightThresholdDb = saved.ThresholdDb.Value;
                    }

                    band.LeftThresholdDb = saved.LeftThresholdDb;
                    band.RightThresholdDb = saved.RightThresholdDb;
                    band.LeftGainDb = saved.LeftGainDb;
                    band.RightGainDb = saved.RightGainDb;
                }
            }

            _masterGainTrack.Value = (int)Clamp(profile.MasterGainDb, _masterGainTrack.Minimum, _masterGainTrack.Maximum);
            _gateTrack.Value = (int)Clamp(profile.NoiseGateDb, _gateTrack.Minimum, _gateTrack.Maximum);
            ApplyProfileAudioDevices(profile);
            _loadingProfiles = false;
            UpdateComputedGains();
            ApplyTheme();
            _curveGrid.Refresh();
            _curvePanel.Invalidate();
            SaveLastProfileName(_currentProfileName);
            SetStatus($"Profil geladen: {_currentProfileName}");
        }
        catch
        {
            _loadingProfiles = false;
            SetStatus("Profil konnte nicht geladen werden; Standardwerte aktiv.");
        }
    }

    private void ApplyProfileAudioDevices(HearingProfile profile)
    {
        var sourceMode = profile.SourceMode is MicrophoneMode or SystemAudioMode
            ? profile.SourceMode
            : SystemAudioMode;

        if (_sourceModeCombo.SelectedIndex != sourceMode)
        {
            _sourceModeCombo.SelectedIndex = sourceMode;
        }
        else
        {
            PopulateSourceDevices();
        }

        SelectDeviceById(_sourceDeviceCombo, profile.SourceDeviceId);
        SelectDeviceById(_outputDeviceCombo, profile.OutputDeviceId);
        SetComboDropDownWidth(_sourceDeviceCombo);
        SetComboDropDownWidth(_outputDeviceCombo);
        UpdateDeviceTooltips();
    }

    private void SaveCurrentProfileWithStatus()
    {
        SaveProfile();
        LoadProfiles(_currentProfileName);
        SetStatus($"Profil gespeichert: {_currentProfileName}");
    }

    private void SaveProfileAs()
    {
        var profileName = PromptForProfileName("Profil speichern als", _currentProfileName);
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        profileName = profileName.Trim();
        var path = GetProfilePath(profileName);
        if (File.Exists(path))
        {
            var overwrite = MessageBox.Show(
                $"Profil '{profileName}' existiert bereits. Ueberschreiben?",
                "Hoerhilfe",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (overwrite != DialogResult.Yes)
            {
                return;
            }
        }

        _currentProfileName = profileName;
        SaveProfile();
        LoadProfiles(profileName);
        SetStatus($"Profil gespeichert: {profileName}");
    }

    private void DeleteCurrentProfile()
    {
        if (_profileCombo.SelectedItem is not string profileName)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Profil '{profileName}' wirklich loeschen?",
            "Hoerhilfe",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var path = GetProfilePath(profileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            ResetBandsToDefaults();
            _currentProfileName = DefaultProfileName;
            LoadProfiles();
            SetStatus($"Profil geloescht: {profileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Profil konnte nicht geloescht werden:\r\n{ex.Message}", "Hoerhilfe", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ResetBandsToDefaults()
    {
        foreach (var band in _bands)
        {
            band.LeftThresholdDb = -45;
            band.RightThresholdDb = -45;
            band.LeftGainDb = 0;
            band.RightGainDb = 0;
        }
        UpdateComputedGains();
    }

    private void SetProfileButtonsEnabled()
    {
        UpdateUiState();
    }

    private static string GetProfilePath(string profileName)
    {
        return Path.Combine(ProfilesDirectory, $"{SanitizeProfileFileName(profileName)}.json");
    }

    private static string? ReadLastProfileName()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return null;
            }

            var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(StatePath));
            return string.IsNullOrWhiteSpace(state?.LastProfileName)
                ? null
                : state.LastProfileName.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static void SaveLastProfileName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(ProfileDirectory);
            var state = new AppState { LastProfileName = profileName.Trim() };
            File.WriteAllText(StatePath, JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }));
        }
        catch
        {
            // Last-profile state is convenience only; profile loading still falls back safely.
        }
    }

    private static string SanitizeProfileFileName(string profileName)
    {
        var safeName = Regex.Replace(profileName.Trim(), @"[^\w\-. ]+", "_");
        safeName = Regex.Replace(safeName, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(safeName) ? DefaultProfileName : safeName;
    }

    private string? PromptForProfileName(string title, string defaultValue)
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(420, 130),
            Font = Font
        };
        var label = new Label
        {
            Text = "Profilname",
            Left = 14,
            Top = 16,
            Width = 390
        };
        var textBox = new TextBox
        {
            Text = defaultValue,
            Left = 14,
            Top = 42,
            Width = 390
        };
        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Left = 244,
            Top = 84,
            Width = 76
        };
        var cancel = new Button
        {
            Text = "Abbrechen",
            DialogResult = DialogResult.Cancel,
            Left = 328,
            Top = 84,
            Width = 76
        };
        form.Controls.Add(label);
        form.Controls.Add(textBox);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog(this) == DialogResult.OK ? textBox.Text : null;
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(message));
            return;
        }

        _statusLabel.Text = message;
    }

    private static double Clamp(double value, double min, double max) => Math.Min(max, Math.Max(min, value));

    private static float Clamp(float value, float min, float max) => Math.Min(max, Math.Max(min, value));

    private static double DbToGain(double db) => Math.Pow(10, db / 20.0);

    private static double LinearToDb(float value)
    {
        return value <= 0.000001f ? -120 : 20.0 * Math.Log10(value);
    }

    private sealed record AudioDeviceItem(MMDevice Device)
    {
        public override string ToString() => Device.FriendlyName;
    }

    private sealed class HearingProfile
    {
        public string Name { get; set; } = DefaultProfileName;
        public List<HearingBand> Bands { get; set; } = [];
        public int MasterGainDb { get; set; }
        public int NoiseGateDb { get; set; } = -62;
        public int SourceMode { get; set; } = SystemAudioMode;
        public string? SourceDeviceId { get; set; }
        public string? OutputDeviceId { get; set; }
        public string Theme { get; set; } = "light";
    }

    private sealed class AppState
    {
        public string? LastProfileName { get; set; }
    }

    private sealed class HearingBand
    {
        public int FrequencyHz { get; set; }
        public double LeftThresholdDb { get; set; } = -45;
        public double RightThresholdDb { get; set; } = -45;
        public double LeftGainDb { get; set; }
        public double RightGainDb { get; set; }
        public double? ThresholdDb { get; set; }
        public double? GainDb { get; set; }
    }

    private sealed class EqualizerProcessor
    {
        private readonly int _sampleRate;
        private readonly int _channels;
        private BiQuadFilter[][] _filtersByChannel = [];
        private float _masterGain = 1f;
        private float _gateLinear;

        public EqualizerProcessor(int sampleRate, int channels, IEnumerable<HearingBand> bands, int masterGainDb, int gateDb)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            UpdateBands(bands, masterGainDb, gateDb);
        }

        public void UpdateBands(IEnumerable<HearingBand> bands, int masterGainDb, int gateDb)
        {
            var bandList = bands.ToArray();
            _filtersByChannel = Enumerable.Range(0, _channels)
                .Select(channel => bandList
                    .Select(b =>
                    {
                        var gain = channel % 2 == 0 ? b.LeftGainDb : b.RightGainDb;
                        return BiQuadFilter.PeakingEQ(_sampleRate, b.FrequencyHz, 1.1f, (float)gain);
                    })
                    .ToArray())
                .ToArray();
            _masterGain = (float)DbToGain(masterGainDb);
            _gateLinear = (float)DbToGain(gateDb);
        }

        public float Process(float sample, int channel)
        {
            if (Math.Abs(sample) < _gateLinear)
            {
                sample *= 0.25f;
            }

            foreach (var filter in _filtersByChannel[channel % _filtersByChannel.Length])
            {
                sample = filter.Transform(sample);
            }

            sample *= _masterGain;

            return (float)Math.Tanh(sample * 1.4f) / 1.4f;
        }
    }

    private sealed class WizardToneSampleProvider : ISampleProvider
    {
        private readonly WaveFormat _waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        private readonly double _frequency;
        private readonly int _targetChannel;
        private readonly object _lock = new();
        private double _gain;
        private int _framePosition;

        public WizardToneSampleProvider(double frequency, double gain, int targetChannel)
        {
            _frequency = frequency;
            _gain = gain;
            _targetChannel = targetChannel;
        }

        public WaveFormat WaveFormat => _waveFormat;

        public void SetLevel(double gain)
        {
            lock (_lock)
            {
                _gain = gain;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var framesRequested = count / _waveFormat.Channels;
            var written = 0;
            for (var frame = 0; frame < framesRequested; frame++)
            {
                double gain;
                lock (_lock)
                {
                    gain = _gain;
                }

                var t = (double)_framePosition / _waveFormat.SampleRate;
                var envelope = Envelope(_framePosition, _waveFormat.SampleRate);
                var sample = (float)(Math.Sin(2 * Math.PI * _frequency * t) * gain * envelope);
                for (var channel = 0; channel < _waveFormat.Channels; channel++)
                {
                    buffer[offset + written++] = _targetChannel < 0 || channel == _targetChannel ? sample : 0f;
                }

                _framePosition++;
            }

            return written;
        }

        private static double Envelope(int position, int sampleRate)
        {
            var ramp = Math.Max(1, sampleRate / 50);
            return position < ramp ? position / (double)ramp : 1.0;
        }
    }

    private sealed class CurvePanel : Panel
    {
        private readonly BindingList<HearingBand> _bands;
        private bool _darkMode;

        public CurvePanel(BindingList<HearingBand> bands)
        {
            _bands = bands;
            Dock = DockStyle.Fill;
            Margin = new Padding(8);
            Padding = new Padding(12);
            BackColor = Color.White;
            DoubleBuffered = true;
        }

        public void SetDarkMode(bool darkMode)
        {
            _darkMode = darkMode;
            BackColor = _darkMode ? Color.FromArgb(35, 40, 48) : Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var plot = new Rectangle(55, 24, Width - 85, Height - 82);
            if (plot.Width < 120 || plot.Height < 80)
            {
                return;
            }

            using var gridPen = new Pen(_darkMode ? Color.FromArgb(70, 78, 90) : Color.FromArgb(225, 230, 238));
            using var axisPen = new Pen(_darkMode ? Color.FromArgb(150, 158, 170) : Color.FromArgb(110, 120, 135));
            using var leftCurvePen = new Pen(Color.FromArgb(27, 100, 156), 3f);
            using var rightCurvePen = new Pen(Color.FromArgb(145, 82, 35), 3f);
            using var leftGainPen = new Pen(Color.FromArgb(34, 111, 84), 2f);
            using var rightGainPen = new Pen(Color.FromArgb(112, 76, 160), 2f);
            using var textBrush = new SolidBrush(_darkMode ? Color.FromArgb(210, 216, 224) : Color.FromArgb(75, 84, 96));
            using var leftCurveBrush = new SolidBrush(Color.FromArgb(27, 100, 156));
            using var rightCurveBrush = new SolidBrush(Color.FromArgb(145, 82, 35));
            using var leftGainBrush = new SolidBrush(Color.FromArgb(34, 111, 84));
            using var rightGainBrush = new SolidBrush(Color.FromArgb(112, 76, 160));

            g.DrawRectangle(axisPen, plot);

            for (var db = -70; db <= -5; db += 10)
            {
                var y = YForThreshold(db, plot);
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                g.DrawString($"{db}", Font, textBrush, 8, y - 8);
            }

            var leftThresholdPoints = new List<PointF>();
            var rightThresholdPoints = new List<PointF>();
            var leftGainPoints = new List<PointF>();
            var rightGainPoints = new List<PointF>();
            for (var i = 0; i < _bands.Count; i++)
            {
                var x = XForIndex(i, plot);
                var band = _bands[i];
                leftThresholdPoints.Add(new PointF(x, YForThreshold(band.LeftThresholdDb, plot)));
                rightThresholdPoints.Add(new PointF(x, YForThreshold(band.RightThresholdDb, plot)));
                leftGainPoints.Add(new PointF(x, YForGain(band.LeftGainDb, plot)));
                rightGainPoints.Add(new PointF(x, YForGain(band.RightGainDb, plot)));
                g.DrawString($"{band.FrequencyHz}", Font, textBrush, x - 18, plot.Bottom + 10);
            }

            if (leftThresholdPoints.Count > 1)
            {
                g.DrawLines(leftCurvePen, leftThresholdPoints.ToArray());
                g.DrawLines(rightCurvePen, rightThresholdPoints.ToArray());
                g.DrawLines(leftGainPen, leftGainPoints.ToArray());
                g.DrawLines(rightGainPen, rightGainPoints.ToArray());
            }

            foreach (var point in leftThresholdPoints)
            {
                g.FillEllipse(leftCurveBrush, point.X - 4, point.Y - 4, 8, 8);
            }

            foreach (var point in rightThresholdPoints)
            {
                g.FillEllipse(rightCurveBrush, point.X - 4, point.Y - 4, 8, 8);
            }

            foreach (var point in leftGainPoints)
            {
                g.FillRectangle(leftGainBrush, point.X - 4, point.Y - 4, 8, 8);
            }

            foreach (var point in rightGainPoints)
            {
                g.FillRectangle(rightGainBrush, point.X - 4, point.Y - 4, 8, 8);
            }

            g.DrawString("L Schwelle", Font, leftCurveBrush, plot.Left, 4);
            g.DrawString("R Schwelle", Font, rightCurveBrush, plot.Left + 95, 4);
            g.DrawString("L Gain", Font, leftGainBrush, plot.Left + 198, 4);
            g.DrawString("R Gain", Font, rightGainBrush, plot.Left + 265, 4);
            g.DrawString("Hz", Font, textBrush, plot.Right - 20, plot.Bottom + 32);
        }

        private static float XForIndex(int index, Rectangle plot)
        {
            return plot.Left + (plot.Width * index / 7f);
        }

        private static float YForThreshold(double db, Rectangle plot)
        {
            var normalized = (db + 70) / 65.0;
            return (float)(plot.Bottom - normalized * plot.Height);
        }

        private static float YForGain(double gain, Rectangle plot)
        {
            var normalized = gain / 18.0;
            return (float)(plot.Bottom - normalized * plot.Height);
        }
    }
}
