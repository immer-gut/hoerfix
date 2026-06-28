using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Hoerfix.Setup;

internal static class Program
{
    private const string AppName = "Hoerfix";
    private const string ExeName = "Hoerfix.exe";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var form = new SetupForm();
        Application.Run(form);
    }

    private sealed class SetupForm : Form
    {
        private readonly CheckBox _desktopShortcut = new()
        {
            Text = "Desktop-Verknuepfung erstellen",
            Checked = true,
            Dock = DockStyle.Top,
            Height = 32
        };

        private readonly CheckBox _launchAfterInstall = new()
        {
            Text = "Hoerfix nach der Installation starten",
            Checked = true,
            Dock = DockStyle.Top,
            Height = 32
        };

        private readonly Label _status = new()
        {
            Text = "Bereit zur Installation.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        private readonly Button _install = new()
        {
            Text = "Installieren",
            Dock = DockStyle.Right,
            Width = 120
        };

        public SetupForm()
        {
            Text = "Hoerfix Setup";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 190);
            Font = new Font("Segoe UI", 9F);

            var title = new Label
            {
                Text = "Hoerfix installieren",
                Dock = DockStyle.Top,
                Height = 44,
                Font = new Font(Font.FontFamily, 14F, FontStyle.Bold)
            };

            var buttons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                Padding = new Padding(0, 8, 0, 0)
            };
            var cancel = new Button
            {
                Text = "Abbrechen",
                Dock = DockStyle.Right,
                Width = 120
            };
            cancel.Click += (_, _) => Close();
            _install.Click += (_, _) => Install();
            buttons.Controls.Add(_install);
            buttons.Controls.Add(cancel);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14)
            };
            body.Controls.Add(_status);
            body.Controls.Add(_launchAfterInstall);
            body.Controls.Add(_desktopShortcut);

            Controls.Add(body);
            Controls.Add(buttons);
            Controls.Add(title);
        }

        private void Install()
        {
            try
            {
                _install.Enabled = false;
                _status.Text = "Installiere Hoerfix...";

                var installDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    AppName);
                Directory.CreateDirectory(installDir);

                var exePath = Path.Combine(installDir, ExeName);
                ExtractEmbeddedExe(exePath);
                CreateShortcut(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                        "Programs",
                        "Hoerfix.lnk"),
                    exePath);

                if (_desktopShortcut.Checked)
                {
                    CreateShortcut(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Hoerfix.lnk"),
                        exePath);
                }

                _status.Text = "Installation abgeschlossen.";

                if (_launchAfterInstall.Checked)
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }

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
                    System.Reflection.BindingFlags.InvokeMethod,
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
    }
}
