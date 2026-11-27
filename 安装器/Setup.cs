using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VolumeFullSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    internal sealed class SetupForm : Form
    {
        private const string AppVersion = "1.2.1";

        private readonly TextBox _pathBox;
        private readonly Button _installButton;
        private readonly ProgressBar _progress;
        private readonly Label _status;

        public SetupForm()
        {
            Text = "VolumeFull 安装程序";
            Width = 580;
            Height = 270;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            // 用系统 UI 字体（不显式指定微软雅黑，避免字体授权问题；中文由系统 fallback 处理）
            Font = SystemFonts.MessageBoxFont;

            Label title = new Label();
            title.Text = "安装 VolumeFull " + AppVersion;
            title.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 14F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(20, 18);
            Controls.Add(title);

            Label dirLabel = new Label();
            dirLabel.Text = "安装目录";
            dirLabel.AutoSize = true;
            dirLabel.Location = new Point(20, 64);
            Controls.Add(dirLabel);

            _pathBox = new TextBox();
            _pathBox.Text = DefaultInstallDir();
            _pathBox.Location = new Point(90, 60);
            _pathBox.Width = 350;
            Controls.Add(_pathBox);

            Button browse = new Button();
            browse.Text = "浏览...";
            browse.Location = new Point(452, 58);
            browse.Width = 90;
            browse.Click += OnBrowse;
            Controls.Add(browse);

            _progress = new ProgressBar();
            _progress.Location = new Point(20, 110);
            _progress.Width = 522;
            _progress.Height = 20;
            Controls.Add(_progress);

            _status = new Label();
            _status.Text = "点击「安装」开始；安装后可在开始菜单和桌面找到快捷方式。";
            _status.AutoSize = true;
            _status.Location = new Point(20, 142);
            _status.ForeColor = Color.Gray;
            Controls.Add(_status);

            _installButton = new Button();
            _installButton.Text = "安装";
            _installButton.Location = new Point(452, 180);
            _installButton.Width = 90;
            _installButton.Height = 32;
            _installButton.Click += OnInstall;
            Controls.Add(_installButton);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.Location = new Point(352, 180);
            cancel.Width = 90;
            cancel.Height = 32;
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);
        }

        private static string DefaultInstallDir()
        {
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    if (d.DriveType == DriveType.Fixed && d.IsReady &&
                        !d.Name.StartsWith("C", StringComparison.OrdinalIgnoreCase))
                    {
                        return Path.Combine(d.RootDirectory.FullName, "Programs", "VolumeFull");
                    }
                }
            }
            catch { }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "VolumeFull");
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择安装目录";
                dialog.SelectedPath = _pathBox.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) _pathBox.Text = dialog.SelectedPath;
            }
        }

        private void OnInstall(object sender, EventArgs e)
        {
            string target = _pathBox.Text == null ? "" : _pathBox.Text.Trim();
            if (target.Length == 0)
            {
                MessageBox.Show(this, "请填写安装目录。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _installButton.Enabled = false;
            try
            {
                InstallTo(target);
                MessageBox.Show(this, "VolumeFull 安装完成。\n\n安装位置：" + target, "完成",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                try { Process.Start(Path.Combine(target, "VolumeFull.exe")); } catch { }
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "安装失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _installButton.Enabled = true;
                _status.Text = "安装失败";
            }
        }

        private void InstallTo(string target)
        {
            Directory.CreateDirectory(target);
            _status.Text = "正在解压文件...";
            Application.DoEvents();

            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.zip"))
            {
                if (stream == null) throw new InvalidOperationException("安装包缺少内嵌资源 app.zip");
                using (ZipArchive zip = new ZipArchive(stream))
                {
                    int total = zip.Entries.Count;
                    int done = 0;
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string dest = Path.Combine(target, entry.FullName);
                        string dir = Path.GetDirectoryName(dest);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        if (!string.IsNullOrEmpty(entry.Name)) entry.ExtractToFile(dest, true);
                        done++;
                        if (done % 25 == 0)
                        {
                            _progress.Value = (int)(done * 100.0 / total);
                            _status.Text = "正在解压 " + done + "/" + total;
                            Application.DoEvents();
                        }
                    }
                }
            }

            _progress.Value = 100;
            _status.Text = "正在创建快捷方式...";
            Application.DoEvents();

            string exe = Path.Combine(target, "VolumeFull.exe");
            CreateShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), exe, "VolumeFull");
            string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "VolumeFull");
            Directory.CreateDirectory(startMenuDir);
            CreateShortcut(startMenuDir, exe, "VolumeFull");

            WriteUninstaller(target);
            RegisterUninstall(target);
            _status.Text = "安装完成";
        }

        private static void CreateShortcut(string dir, string targetExe, string name)
        {
            string link = Path.Combine(dir, name + ".lnk");
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
            Type shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
            shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(targetExe) });
            shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe + ",0" });
            shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        private static void WriteUninstaller(string target)
        {
            string escaped = target.Replace("'", "''");
            string ps1 =
                "Stop-Process -Name VolumeFull -Force -ErrorAction SilentlyContinue\r\n" +
                "Start-Sleep -Milliseconds 600\r\n" +
                "Remove-Item -LiteralPath '" + escaped + "' -Recurse -Force -ErrorAction SilentlyContinue\r\n" +
                "Remove-Item -LiteralPath (Join-Path ([Environment]::GetFolderPath('Programs')) 'VolumeFull') -Recurse -Force -ErrorAction SilentlyContinue\r\n" +
                "Remove-Item -LiteralPath (Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'VolumeFull.lnk') -Force -ErrorAction SilentlyContinue\r\n" +
                "Remove-Item -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\VolumeFull' -Recurse -Force -ErrorAction SilentlyContinue\r\n";
            File.WriteAllText(Path.Combine(target, "卸载.ps1"), ps1, new System.Text.UTF8Encoding(true));
            string bat = "@echo off\r\npowershell -ExecutionPolicy Bypass -File \"%~dp0卸载.ps1\"\r\n";
            File.WriteAllText(Path.Combine(target, "卸载.bat"), bat, System.Text.Encoding.Default);
        }

        private static void RegisterUninstall(string target)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\VolumeFull"))
            {
                key.SetValue("DisplayName", "VolumeFull");
                key.SetValue("DisplayVersion", AppVersion);
                key.SetValue("Publisher", "VolumeFull");
                key.SetValue("InstallLocation", target);
                key.SetValue("DisplayIcon", Path.Combine(target, "VolumeFull.exe"));
                key.SetValue("UninstallString", "\"" + Path.Combine(target, "卸载.bat") + "\"");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
    }
}
