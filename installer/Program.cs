using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Hoerfix.Setup;

internal static class Program
{
    private const string AppName = "Hoerfix";
    private const string ExeName = "Hoerfix.exe";
    private const string UninstallScriptName = "Uninstall-Hoerfix.ps1";
    private const string Publisher = "immer-gut";
    private const string UninstallRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Hoerfix";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (HasArg(args, "--uninstall"))
        {
            RunUninstall(HasArg(args, "--quiet"));
            return;
        }

        if (HasArg(args, "--install"))
        {
            InstallHoerfix(
                desktopShortcut: !HasArg(args, "--no-desktop"),
                launchAfterInstall: !HasArg(args, "--no-launch"),
                status: null);
            return;
        }

        using var form = new SetupForm();
        Application.Run(form);
    }

    private static bool HasArg(string[] args, string name) =>
        args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

    private static string InstallDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        AppName);

    private static string StartMenuShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        "Programs",
        "Hoerfix.lnk");

    private static string DesktopShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "Hoerfix.lnk");

    private static string InstalledExePath => Path.Combine(InstallDir, ExeName);

    private static string InstalledUninstallScriptPath => Path.Combine(InstallDir, UninstallScriptName);

    private static string AppVersion => Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion
        .Split('+')[0] ?? "0.0.0";

    private static void RunUninstall(bool quiet)
    {
        if (!quiet)
        {
            var result = MessageBox.Show(
                "Hoerfix wirklich von diesem PC entfernen?",
                "Hoerfix deinstallieren",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes)
            {
                return;
            }
        }

        try
        {
            StopInstalledApp();
            DeleteShortcut(StartMenuShortcutPath);
            DeleteShortcut(DesktopShortcutPath);
            Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryKey, throwOnMissingSubKey: false);
            ScheduleInstallDirectoryRemoval();
            if (!quiet)
            {
                MessageBox.Show("Hoerfix wurde deinstalliert.", "Hoerfix deinstallieren", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            if (quiet)
            {
                throw;
            }

            MessageBox.Show($"Hoerfix konnte nicht vollstaendig deinstalliert werden:\r\n{ex.Message}", "Hoerfix deinstallieren", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private sealed class SetupForm : Form
    {
        private readonly CheckBox _desktopShortcut = new()
        {
            Text = "Desktop-Verknuepfung erstellen",
            Checked = true,
            AutoSize = true,
            Dock = DockStyle.Left,
            Margin = new Padding(0, 0, 0, 6)
        };

        private readonly CheckBox _launchAfterInstall = new()
        {
            Text = "Hoerfix nach der Installation starten",
            Checked = true,
            AutoSize = true,
            Dock = DockStyle.Left,
            Margin = new Padding(0, 0, 0, 10)
        };

        private readonly Label _status = new()
        {
            Text = "Hoerfix wird auf diesem PC fuer den aktuellen Benutzer installiert. Sie brauchen keine Administratorrechte.",
            Dock = DockStyle.Fill,
            AutoSize = false,
            TextAlign = ContentAlignment.TopLeft,
            Margin = new Padding(0, 8, 0, 0)
        };

        private readonly Button _install = new()
        {
            Text = "Installieren",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Width = 128,
            Height = 34
        };

        public SetupForm()
        {
            Text = "Hoerfix Setup";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 260);
            Font = new Font("Segoe UI", 9F);
            Padding = new Padding(18, 16, 18, 14);

            var title = new Label
            {
                Text = "Hoerfix installieren oder aktualisieren",
                Dock = DockStyle.Top,
                Height = 40,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(Font.FontFamily, 14F, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8)
            };

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0)
            };
            var cancel = new Button
            {
                Text = "Abbrechen",
                Width = 128,
                Height = 34,
                Margin = new Padding(6, 0, 0, 0)
            };
            cancel.Click += (_, _) => Close();
            _install.Click += (_, _) => Install();
            buttons.Controls.Add(_install);
            buttons.Controls.Add(cancel);

            var body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0, 12, 0, 8)
            };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.Controls.Add(_desktopShortcut, 0, 0);
            body.Controls.Add(_launchAfterInstall, 0, 1);
            body.Controls.Add(_status, 0, 2);

            Controls.Add(body);
            Controls.Add(buttons);
            Controls.Add(title);
            AcceptButton = _install;
            CancelButton = cancel;
        }

        private void Install()
        {
            try
            {
                _install.Enabled = false;
                InstallHoerfix(_desktopShortcut.Checked, _launchAfterInstall.Checked, text => _status.Text = text);

                MessageBox.Show(this, "Hoerfix wurde installiert.", "Hoerfix Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                _install.Enabled = true;
                _status.Text = "Installation fehlgeschlagen.";
                MessageBox.Show(this, ex.Message, "Hoerfix Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

    }

    private static void ExtractEmbeddedExe(string targetPath)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ExeName)
            ?? throw new InvalidOperationException("Installer-Payload wurde nicht gefunden.");
        using var file = File.Create(targetPath);
        stream.CopyTo(file);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell ist nicht verfuegbar.");
        var shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("WScript.Shell konnte nicht erstellt werden.");

        try
        {
            dynamic shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath])!;
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcut.Description = "Hoerfix starten";
            shortcut.Save();
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    private static void WriteUninstallScript()
    {
        File.WriteAllText(
            InstalledUninstallScriptPath,
            """
            $ErrorActionPreference = "SilentlyContinue"

            $installDir = Join-Path $env:LOCALAPPDATA "Programs\Hoerfix"
            $exePath = Join-Path $installDir "Hoerfix.exe"
            $startMenuShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Hoerfix.lnk"
            $desktopShortcut = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "Hoerfix.lnk"

            Get-Process -Name "Hoerfix" | Where-Object {
                $_.Path -and ([string]::Equals($_.Path, $exePath, [StringComparison]::OrdinalIgnoreCase))
            } | ForEach-Object {
                $_.CloseMainWindow() | Out-Null
                if (-not $_.WaitForExit(2500)) {
                    $_.Kill()
                    $_.WaitForExit(2500)
                }
            }

            Remove-Item -LiteralPath $startMenuShortcut -Force
            Remove-Item -LiteralPath $desktopShortcut -Force
            Remove-Item -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Hoerfix" -Recurse -Force

            Start-Process -FilePath "$env:ComSpec" -ArgumentList "/c timeout /t 1 /nobreak > nul & rmdir /s /q `"$installDir`"" -WindowStyle Hidden
            """);
    }

    private static void RegisterUninstallEntry()
    {
        using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryKey)
            ?? throw new InvalidOperationException("Der Windows-Deinstallations-Eintrag konnte nicht erstellt werden.");

        key.SetValue("DisplayName", AppName);
        key.SetValue("DisplayVersion", AppVersion);
        key.SetValue("Publisher", Publisher);
        key.SetValue("InstallLocation", InstallDir);
        key.SetValue("DisplayIcon", InstalledExePath);
        key.SetValue("UninstallString", $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{InstalledUninstallScriptPath}\"");
        key.SetValue("QuietUninstallString", $"powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{InstalledUninstallScriptPath}\"");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", GetEstimatedInstallSizeKb(), RegistryValueKind.DWord);
    }

    private static int GetEstimatedInstallSizeKb()
    {
        try
        {
            return Directory.EnumerateFiles(InstallDir, "*", SearchOption.AllDirectories)
                .Sum(path => (int)Math.Max(1, new FileInfo(path).Length / 1024));
        }
        catch
        {
            return 0;
        }
    }

    private static void InstallHoerfix(bool desktopShortcut, bool launchAfterInstall, Action<string>? status)
    {
        status?.Invoke("Installiere Hoerfix...");

        Directory.CreateDirectory(InstallDir);
        StopInstalledApp();

        ExtractEmbeddedExe(InstalledExePath);
        WriteUninstallScript();
        CreateShortcut(StartMenuShortcutPath, InstalledExePath);

        if (desktopShortcut)
        {
            CreateShortcut(DesktopShortcutPath, InstalledExePath);
        }
        else
        {
            DeleteShortcut(DesktopShortcutPath);
        }

        RegisterUninstallEntry();
        status?.Invoke("Installation abgeschlossen.");

        if (launchAfterInstall)
        {
            Process.Start(new ProcessStartInfo(InstalledExePath) { UseShellExecute = true });
        }
    }

    private static void StopInstalledApp()
    {
        foreach (var process in Process.GetProcessesByName("Hoerfix"))
        {
            try
            {
                if (!string.Equals(process.MainModule?.FileName, InstalledExePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                process.CloseMainWindow();
                if (!process.WaitForExit(2500))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2500);
                }
            }
            catch
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void DeleteShortcut(string shortcutPath)
    {
        if (File.Exists(shortcutPath))
        {
            File.Delete(shortcutPath);
        }
    }

    private static void ScheduleInstallDirectoryRemoval()
    {
        if (!Directory.Exists(InstallDir))
        {
            return;
        }

        var command = $"/c timeout /t 1 /nobreak > nul & rmdir /s /q \"{InstallDir}\"";
        Process.Start(new ProcessStartInfo("cmd.exe", command)
        {
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false
        });
    }
}
