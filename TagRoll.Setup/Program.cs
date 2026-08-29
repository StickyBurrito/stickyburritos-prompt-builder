using System.Diagnostics;
using System.IO.Compression;
using System.Drawing.Drawing2D;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace TagRoll.Setup;

internal sealed record ModelTier(string Label, int MinimumVram, string TextModel, string VisionModel, string Download);

internal static class Program
{
    private const string AppProductName = "Stickyburrito's Prompt Generator";
    private const string AppExecutableName = "Stickyburritos-Prompt-Generator.exe";
    private const string UninstallRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\StickyburritosPromptGenerator";
    private const string AppProductVersion = "1.1.0";

    private static readonly ModelTier[] Tiers =
    [
        new("4–5 GB · Compact", 4, "huihui_ai/qwen3-vl-abliterated:4b-instruct-q4_K_M", "huihui_ai/qwen3-vl-abliterated:4b-instruct-q4_K_M", "about 3.3 GB"),
        new("6–11 GB · Balanced", 6, "huihui_ai/qwen3-vl-abliterated:8b-instruct-q4_K_M", "huihui_ai/qwen3-vl-abliterated:8b-instruct-q4_K_M", "about 6.1 GB"),
        new("12–15 GB · Qwen 3.6 compact", 12, "richardyoung/qwen3.6-27b-abliterated:IQ3_M", "huihui_ai/qwen3-vl-abliterated:8b-instruct-q4_K_M", "about 19 GB total"),
        new("16–23 GB · Qwen 3.6 recommended", 16, "richardyoung/qwen3.6-27b-abliterated:IQ4_XS", "huihui_ai/qwen3-vl-abliterated:8b-instruct-q4_K_M", "about 21 GB total"),
        new("24–31 GB · High quality", 24, "richardyoung/qwen3.6-27b-abliterated:Q5_K_M", "huihui_ai/qwen3-vl-abliterated:30b-instruct-q4_K_M", "about 39 GB total"),
        new("32 GB+ · Stable text + maximum vision", 32, "richardyoung/qwen3.6-27b-abliterated:latest", "huihui_ai/qwen3-vl-abliterated:32b-instruct-q4_K_M", "about 36 GB total")
    ];

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm());
    }

    private sealed class SetupForm : Form
    {
        private readonly ComboBox vram = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox tier = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label recommendation = new() { AutoSize = true, ForeColor = Color.FromArgb(190, 184, 210) };
        private readonly CheckBox installOllama = new() { Text = "Install or update Ollama", Checked = true, AutoSize = true };
        private readonly CheckBox desktopShortcut = new() { Text = "Create a desktop shortcut", Checked = true, AutoSize = true };
        private readonly TextBox installLocation = new() { Dock = DockStyle.Fill };
        private readonly Button browse = new() { Text = "Browse…", AutoSize = true };
        private readonly NeonButton install = new() { Text = "INSTALL PROMPT GENERATOR  →", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
        private readonly Button uninstall = new() { Text = "UNINSTALL", Dock = DockStyle.Fill, Margin = new Padding(6, 0, 6, 0), FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(255, 126, 191), BackColor = Color.FromArgb(30, 22, 54), Visible = false };
        private readonly Button cancel = new() { Text = "CLOSE", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0), FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(220, 214, 234), BackColor = Color.FromArgb(30, 22, 54) };
        private readonly ProgressBar progress = new() { Dock = DockStyle.Bottom, Height = 10, Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous };
        private readonly Label progressStatus = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(203, 195, 220), Font = new Font("Segoe UI", 9) };
        private readonly Panel progressArea = new() { Dock = DockStyle.Bottom, Height = 42, Visible = false, Padding = new Padding(24, 2, 20, 8), BackColor = Color.FromArgb(15, 9, 34) };
        private readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 8, 25), ForeColor = Color.FromArgb(225, 221, 238), BorderStyle = BorderStyle.FixedSingle };
        private readonly Stopwatch installationTimer = new();
        private int detectedVram;
        private int currentProgress;
        private CancellationTokenSource? installationCancellation;
        private Process? activeProcess;
        private string? activeInstallDirectory;
        private bool installDirectoryExisted;
        private HashSet<string> preExistingInstallFiles = new(StringComparer.OrdinalIgnoreCase);

        internal SetupForm()
        {
            Text = "Stickyburrito's Prompt Generator Setup";
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new SizeF(96f, 96f);
            Width = 760; Height = 720; MinimumSize = new Size(700, 650); StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(7, 4, 24); ForeColor = Color.White; Font = new Font("Segoe UI", 10); DoubleBuffered = true;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            for (var gb = 4; gb <= 48; gb += gb < 16 ? 2 : 4) vram.Items.Add($"{gb} GB");
            vram.Items.Add("64 GB or more");
            foreach (var item in Tiers) tier.Items.Add(item.Label);

            detectedVram = DetectNvidiaVram();
            installLocation.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Stickyburrito's Prompt Generator");
            SelectVram(detectedVram > 0 ? detectedVram : 8);
            SelectRecommendedTier();
            vram.SelectedIndexChanged += (_, _) => SelectRecommendedTier();
            tier.SelectedIndexChanged += (_, _) => UpdateRecommendation();
            install.Click += async (_, _) => await InstallAsync();
            uninstall.Click += async (_, _) => await UninstallAsync();
            browse.Click += (_, _) => BrowseForInstallLocation();
            cancel.Click += (_, _) => CancelOrClose();

            var iconImage = LoadIconImage();
            var logo = new PictureBox { Image = iconImage, Size = new Size(76, 76), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 16, 0) };
            var title = new Label { Text = "STICKYBURRITO'S\nPROMPT GENERATOR", AutoSize = true, Font = new Font("Segoe UI", 21, FontStyle.Bold), ForeColor = Color.FromArgb(255, 65, 172), Margin = new Padding(0, 5, 0, 0) };
            var brand = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, BackColor = Color.Transparent };
            brand.Controls.Add(logo); brand.Controls.Add(title);
            var intro = new Label { Text = "Local prompting for Danbooru, Pony, Krea 2 and MiniMax H3.\nSetup chooses models that fit your graphics memory.", AutoSize = true, ForeColor = Color.FromArgb(203, 195, 220), Margin = new Padding(0, 6, 0, 10) };
            var support = new LinkLabel { Text = "Support Stickyburrito with PayPal", AutoSize = true, LinkColor = Color.FromArgb(88, 169, 255), ActiveLinkColor = Color.FromArgb(255, 65, 172), VisitedLinkColor = Color.FromArgb(88, 169, 255), Margin = new Padding(0, 10, 0, 0), Cursor = Cursors.Hand };
            support.LinkClicked += (_, _) => Process.Start(new ProcessStartInfo("https://paypal.me/StickyBurrito") { UseShellExecute = true });
            var grid = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Padding = new Padding(0, 4, 0, 8) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grid.Controls.Add(new Label { Text = "Graphics memory", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); grid.Controls.Add(vram, 1, 0);
            grid.Controls.Add(new Label { Text = "Model package", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); grid.Controls.Add(tier, 1, 1);
            var locationRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0) };
            locationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); locationRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            locationRow.Controls.Add(installLocation, 0, 0); locationRow.Controls.Add(browse, 1, 0);
            grid.Controls.Add(new Label { Text = "Install files to", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2); grid.Controls.Add(locationRow, 1, 2);
            vram.Dock = tier.Dock = DockStyle.Fill;
            var top = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(20, 18, 20, 14), BackColor = Color.FromArgb(28, 13, 54) };
            top.Controls.Add(brand); top.Controls.Add(intro); top.Controls.Add(grid); top.Controls.Add(recommendation); top.Controls.Add(installOllama); top.Controls.Add(desktopShortcut); top.Controls.Add(support);
            var shell = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 12, 24, 16), BackColor = Color.Transparent }; shell.Controls.Add(log);
            var actions = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 66, ColumnCount = 3, RowCount = 1, Padding = new Padding(24, 8, 20, 8), BackColor = Color.FromArgb(15, 9, 34) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            actions.Controls.Add(cancel, 0, 0); actions.Controls.Add(uninstall, 1, 0); actions.Controls.Add(install, 2, 0);
            progressArea.Controls.Add(progressStatus); progressArea.Controls.Add(progress);
            Controls.Add(shell); Controls.Add(top); Controls.Add(progressArea); Controls.Add(actions);
            RefreshUninstallButton();
        }

        private static int DetectNvidiaVram()
        {
            try
            {
                var psi = new ProcessStartInfo("nvidia-smi", "--query-gpu=memory.total --format=csv,noheader,nounits") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
                using var process = Process.Start(psi); var line = process?.StandardOutput.ReadLine(); process?.WaitForExit();
                return int.TryParse(line?.Trim(), out var mb) ? (int)Math.Round(mb / 1024d) : 0;
            }
            catch { return 0; }
        }

        private int SelectedVram => vram.SelectedIndex == vram.Items.Count - 1 ? 64 : int.Parse(vram.SelectedItem!.ToString()!.Split(' ')[0]);
        private void SelectVram(int gb)
        {
            var index = 0;
            for (var i = 0; i < vram.Items.Count - 1; i++) if (int.Parse(vram.Items[i]!.ToString()!.Split(' ')[0]) <= gb) index = i;
            if (gb >= 64) index = vram.Items.Count - 1;
            vram.SelectedIndex = index;
        }
        private void SelectRecommendedTier() => tier.SelectedIndex = Array.FindLastIndex(Tiers, x => x.MinimumVram <= SelectedVram) is var i && i >= 0 ? i : 0;
        private void UpdateRecommendation()
        {
            var selected = Tiers[Math.Max(0, tier.SelectedIndex)];
            recommendation.Text = $"{(detectedVram > 0 ? $"Detected approximately {detectedVram} GB NVIDIA VRAM. " : "VRAM could not be detected automatically. ")}\nText: {selected.TextModel}\nVision: {selected.VisionModel}\nDownload: {selected.Download}. Models stay on this computer.";
        }

        private async Task InstallAsync()
        {
            install.Enabled = false; browse.Enabled = false; installLocation.Enabled = false; progressArea.Visible = true; log.Clear(); cancel.Text = "CANCEL";
            installationTimer.Restart(); currentProgress = 0; progress.Value = 0; SetProgress(1, "Preparing installation");
            installationCancellation = new CancellationTokenSource();
            var cancellationToken = installationCancellation.Token;
            try
            {
                var selected = Tiers[tier.SelectedIndex];
                var installDir = ValidateInstallLocation(installLocation.Text);
                activeInstallDirectory = installDir;
                installDirectoryExisted = Directory.Exists(installDir);
                preExistingInstallFiles = installDirectoryExisted
                    ? Directory.EnumerateFiles(installDir, "*", SearchOption.AllDirectories).ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                SetProgress(3, "Preparing the installation folder");
                Write("Installing Stickyburrito's Prompt Generator…"); Directory.CreateDirectory(installDir);
                SetProgress(5, "Unpacking the application");
                await ExtractPayloadAsync(installDir, cancellationToken);
                SetProgress(10, "Writing the local model configuration");
                Write("Writing selected model configuration…"); WriteConfiguration(installDir, selected);
                if (installOllama.Checked) await EnsureOllamaAsync(cancellationToken);
                else SetProgress(22, "Checking the existing Ollama installation");
                var ollama = FindOllama() ?? throw new InvalidOperationException("Ollama was not found after installation.");
                var models = new[] { selected.TextModel, selected.VisionModel }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                for (var index = 0; index < models.Length; index++) await PullModelAsync(ollama, models[index], index, models.Length, cancellationToken);
                SetProgress(97, "Creating shortcuts");
                CreateShortcuts(installDir, desktopShortcut.Checked);
                RegisterUninstaller(installDir);
                SetProgress(100, "Installation complete");
                Write("Installation complete. Starting Stickyburrito's Prompt Generator…");
                Process.Start(new ProcessStartInfo(Path.Combine(installDir, "Stickyburritos-Prompt-Generator.exe"), "--urls http://localhost:8765 --open-browser") { WorkingDirectory = installDir, UseShellExecute = true });
                MessageBox.Show(this, "Stickyburrito's Prompt Generator is installed and ready.", "Setup complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException) { SetProgress(currentProgress, "Cancelling and cleaning up"); CleanupCancelledInstallation(); Write("Installation cancelled. Files created by this attempt were removed. Ollama's separate model cache was left untouched."); }
            catch (Exception ex) { Write("ERROR: " + ex.Message); MessageBox.Show(this, ex.Message, "Installation failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { activeProcess = null; installationCancellation?.Dispose(); installationCancellation = null; activeInstallDirectory = null; preExistingInstallFiles.Clear(); installationTimer.Reset(); progressArea.Visible = false; install.Enabled = true; browse.Enabled = true; installLocation.Enabled = true; cancel.Text = "CLOSE"; RefreshUninstallButton(); }
        }

        private async Task UninstallAsync()
        {
            var installDir = FindInstalledDirectory();
            if (installDir is null)
            {
                MessageBox.Show(this, "No installed copy could be found.", "Nothing to uninstall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshUninstallButton();
                return;
            }

            var answer = MessageBox.Show(this,
                "Remove Stickyburrito's Prompt Generator, its shortcuts, and its local settings?\n\nOllama and downloaded models will be kept because other local applications may use them.",
                "Uninstall Stickyburrito's Prompt Generator", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            install.Enabled = uninstall.Enabled = browse.Enabled = installLocation.Enabled = false;
            progressArea.Visible = true; installationTimer.Restart(); currentProgress = 0; progress.Value = 0; log.Clear();
            try
            {
                SetProgress(10, "Stopping the prompt generator");
                StopInstalledApplication(installDir);
                await Task.Delay(500);
                SetProgress(35, "Removing shortcuts and Windows registration");
                RemoveShortcuts();
                RemoveUninstallRegistration();
                SetProgress(55, "Removing installed files");
                Directory.Delete(installDir, recursive: true);
                SetProgress(100, "Uninstall complete");
                Write("Stickyburrito's Prompt Generator was removed. Ollama and its model cache were kept.");
                MessageBox.Show(this, "Stickyburrito's Prompt Generator was removed. Ollama and downloaded models were kept.", "Uninstall complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Write("ERROR: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Uninstall failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                installationTimer.Reset(); progressArea.Visible = false; install.Enabled = browse.Enabled = installLocation.Enabled = true; RefreshUninstallButton();
            }
        }

        private void CleanupCancelledInstallation()
        {
            var installDir = activeInstallDirectory;
            if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir)) return;
            try
            {
                if (!installDirectoryExisted)
                {
                    Directory.Delete(installDir, recursive: true);
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(installDir, "*", SearchOption.AllDirectories)
                    .Where(file => !preExistingInstallFiles.Contains(file)).OrderByDescending(file => file.Length))
                {
                    try { File.Delete(file); } catch { }
                }

                foreach (var directory in Directory.EnumerateDirectories(installDir, "*", SearchOption.AllDirectories)
                    .OrderByDescending(directory => directory.Length))
                {
                    try
                    {
                        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Write("Cleanup warning: " + ex.Message); }
        }

        private void Write(string value) { log.AppendText(value + Environment.NewLine); log.SelectionStart = log.TextLength; log.ScrollToCaret(); Application.DoEvents(); }
        private void SetProgress(int percent, string stage)
        {
            currentProgress = Math.Max(currentProgress, Math.Clamp(percent, 0, 100));
            progress.Value = currentProgress;
            if (currentProgress >= 100) progressStatus.Text = $"{stage}  •  100%";
            else
            {
                var eta = EstimateRemainingTime(currentProgress);
                progressStatus.Text = eta is null
                    ? $"{stage}  •  {currentProgress}%  •  estimating time remaining…"
                    : $"{stage}  •  {currentProgress}%  •  about {FormatDuration(eta.Value)} remaining";
            }
            Application.DoEvents();
        }

        private TimeSpan? EstimateRemainingTime(int percent)
        {
            if (!installationTimer.IsRunning || installationTimer.Elapsed < TimeSpan.FromSeconds(4) || percent < 4 || percent >= 100) return null;
            var seconds = installationTimer.Elapsed.TotalSeconds * (100d - percent) / percent;
            return TimeSpan.FromSeconds(Math.Clamp(seconds, 1, TimeSpan.FromHours(24).TotalSeconds));
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1) return $"{Math.Ceiling(duration.TotalHours):0} hr";
            if (duration.TotalMinutes >= 1) return $"{Math.Ceiling(duration.TotalMinutes):0} min";
            return $"{Math.Max(1, Math.Ceiling(duration.TotalSeconds)):0} sec";
        }
        private static async Task ExtractPayloadAsync(string destination, CancellationToken cancellationToken)
        {
            await using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("TagRollPayload.zip") ?? throw new InvalidOperationException("The TagRoll payload is missing.");
            var temp = Path.Combine(Path.GetTempPath(), "TagRollPayload-" + Guid.NewGuid().ToString("N") + ".zip");
            await using (var file = File.Create(temp)) await resource.CopyToAsync(file, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ZipFile.ExtractToDirectory(temp, destination, true); File.Delete(temp);
        }
        private static void WriteConfiguration(string installDir, ModelTier selected)
        {
            var path = Path.Combine(installDir, "App", "appsettings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var config = new { Ollama = new { Endpoint = "http://127.0.0.1:11434/api/chat", DefaultModel = selected.TextModel, VisionModel = selected.VisionModel, TimeoutSeconds = 300 } };
            File.WriteAllText(path, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }
        private async Task EnsureOllamaAsync(CancellationToken cancellationToken)
        {
            if (FindOllama() is not null) { SetProgress(22, "Ollama is ready"); Write("Ollama is already installed; keeping the current installation."); return; }
            Write("Downloading the current official Ollama installer…");
            SetProgress(12, "Downloading Ollama");
            var setup = Path.Combine(Path.GetTempPath(), "OllamaSetup.exe");
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TagRoll-Setup", "1.0"));
            using (var response = await client.GetAsync("https://ollama.com/download/OllamaSetup.exe", HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength;
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var target = File.Create(setup);
                var buffer = new byte[128 * 1024];
                long downloaded = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloaded += read;
                    if (totalBytes is > 0)
                    {
                        var downloadProgress = 12 + (int)Math.Round(6d * downloaded / totalBytes.Value);
                        if (downloadProgress > currentProgress) SetProgress(downloadProgress, "Downloading Ollama");
                    }
                }
            }
            Write("Installing Ollama silently…");
            SetProgress(19, "Installing Ollama");
            using var process = Process.Start(new ProcessStartInfo(setup, "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES") { UseShellExecute = true });
            activeProcess = process;
            if (process is null) throw new InvalidOperationException("Could not start the Ollama installer.");
            await process.WaitForExitAsync(cancellationToken); if (process.ExitCode != 0) throw new InvalidOperationException($"Ollama installer exited with code {process.ExitCode}.");
            activeProcess = null;
            SetProgress(24, "Ollama is ready");
        }
        private static string? FindOllama()
        {
            var candidates = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama", "ollama.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ollama", "ollama.exe") };
            return candidates.FirstOrDefault(File.Exists) ?? Environment.GetEnvironmentVariable("PATH")?.Split(';').Select(x => Path.Combine(x, "ollama.exe")).FirstOrDefault(File.Exists);
        }
        private async Task PullModelAsync(string ollama, string model, int modelIndex, int modelCount, CancellationToken cancellationToken)
        {
            var stageStart = 25 + (int)Math.Round(70d * modelIndex / modelCount);
            var stageEnd = 25 + (int)Math.Round(70d * (modelIndex + 1) / modelCount);
            SetProgress(stageStart, $"Preparing local model {modelIndex + 1} of {modelCount}");
            Write($"Downloading {model}… This can take a while.");
            var psi = new ProcessStartInfo(ollama, $"pull {model}") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start Ollama.");
            activeProcess = process;
            long trackedLayerBytes = 0;
            void QueueOutput(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                BeginInvoke(() =>
                {
                    var clean = Regex.Replace(value, "\\x1B\\[[0-?]*[ -/]*[@-~]", string.Empty);
                    Write(clean);
                    var match = Regex.Match(clean, @"(?<!\d)(\d{1,3})%");
                    var sizes = Regex.Matches(clean, @"(?<value>\d+(?:\.\d+)?)\s*(?<unit>[KMGT]?B)\b", RegexOptions.IgnoreCase);
                    if (match.Success && sizes.Count > 0 && int.TryParse(match.Groups[1].Value, out var modelProgress))
                    {
                        var size = sizes[sizes.Count - 1];
                        var amount = double.Parse(size.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture);
                        var multiplier = size.Groups["unit"].Value.ToUpperInvariant() switch
                        {
                            "TB" => 1024d * 1024 * 1024 * 1024,
                            "GB" => 1024d * 1024 * 1024,
                            "MB" => 1024d * 1024,
                            "KB" => 1024d,
                            _ => 1d
                        };
                        var layerBytes = (long)(amount * multiplier);
                        // Ollama reports several blobs independently. Follow the largest model-weight
                        // layer instead of letting a tiny metadata blob jump the whole bar to 100%.
                        if (layerBytes >= 1024L * 1024 * 1024 && layerBytes >= trackedLayerBytes)
                        {
                            trackedLayerBytes = Math.Max(trackedLayerBytes, layerBytes);
                            var overall = stageStart + (int)Math.Round((stageEnd - stageStart) * Math.Clamp(modelProgress, 0, 100) / 100d);
                            SetProgress(overall, $"Downloading local model {modelIndex + 1} of {modelCount}");
                        }
                    }
                });
            }
            process.OutputDataReceived += (_, e) => QueueOutput(e.Data);
            process.ErrorDataReceived += (_, e) => QueueOutput(e.Data);
            process.BeginOutputReadLine(); process.BeginErrorReadLine(); await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0) throw new InvalidOperationException($"Ollama could not pull {model} (exit {process.ExitCode}).");
            activeProcess = null;
            SetProgress(stageEnd, $"Local model {modelIndex + 1} of {modelCount} is ready");
        }
        private static void CreateShortcuts(string installDir, bool desktop)
        {
            var exe = Path.Combine(installDir, "Stickyburritos-Prompt-Generator.exe");
            var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Stickyburrito's Prompt Generator.lnk");
            CreateShortcut(startMenu, exe, installDir);
            if (desktop) CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Stickyburrito's Prompt Generator.lnk"), exe, installDir);
        }

        private static void RemoveShortcuts()
        {
            var shortcuts = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", AppProductName + ".lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppProductName + ".lnk")
            };
            foreach (var shortcut in shortcuts) try { if (File.Exists(shortcut)) File.Delete(shortcut); } catch { }
        }

        private static void RegisterUninstaller(string installDir)
        {
            var exe = Path.Combine(installDir, AppExecutableName);
            using var key = Registry.CurrentUser.CreateSubKey(UninstallRegistryPath, writable: true)
                ?? throw new InvalidOperationException("Windows could not register the uninstaller.");
            key.SetValue("DisplayName", AppProductName);
            key.SetValue("DisplayVersion", AppProductVersion);
            key.SetValue("Publisher", "StickyBurrito");
            key.SetValue("InstallLocation", installDir);
            key.SetValue("DisplayIcon", exe + ",0");
            key.SetValue("UninstallString", $"\"{exe}\" --uninstall");
            key.SetValue("QuietUninstallString", $"\"{exe}\" --uninstall --quiet");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            try
            {
                var sizeKb = Directory.EnumerateFiles(installDir, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length) / 1024;
                key.SetValue("EstimatedSize", Math.Min(sizeKb, int.MaxValue), RegistryValueKind.DWord);
            }
            catch { }
        }

        private static void RemoveUninstallRegistration()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallRegistryPath, throwOnMissingSubKey: false); } catch { }
        }

        private static string? FindInstalledDirectory()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath);
                if (key?.GetValue("InstallLocation") is string registered && IsInstalledDirectory(registered)) return Path.GetFullPath(registered);
            }
            catch { }
            var defaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", AppProductName);
            return IsInstalledDirectory(defaultDirectory) ? defaultDirectory : null;
        }

        private static bool IsInstalledDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try
            {
                var fullPath = Path.GetFullPath(path);
                return File.Exists(Path.Combine(fullPath, AppExecutableName)) &&
                       (Directory.Exists(Path.Combine(fullPath, "App")) || Directory.Exists(Path.Combine(fullPath, "Web")));
            }
            catch { return false; }
        }

        private static void StopInstalledApplication(string installDir)
        {
            var expectedExe = Path.GetFullPath(Path.Combine(installDir, AppExecutableName));
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(AppExecutableName)))
            {
                using (process)
                {
                    try
                    {
                        if (string.Equals(Path.GetFullPath(process.MainModule?.FileName ?? string.Empty), expectedExe, StringComparison.OrdinalIgnoreCase))
                        {
                            process.Kill(entireProcessTree: true);
                            process.WaitForExit(5000);
                        }
                    }
                    catch { }
                }
            }
        }

        private void RefreshUninstallButton()
        {
            var installed = FindInstalledDirectory() is not null;
            uninstall.Visible = installed;
            uninstall.Enabled = installed && installationCancellation is null;
        }
        private static void CreateShortcut(string shortcutPath, string target, string installDir)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
            var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows shortcut support is unavailable.");
            dynamic shell = Activator.CreateInstance(shellType)!; dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = target; shortcut.Arguments = "--urls http://localhost:8765 --open-browser"; shortcut.WorkingDirectory = installDir; shortcut.Description = "Stickyburrito's local AI prompt generator"; shortcut.IconLocation = target + ",0"; shortcut.Save();
        }

        private static Image LoadIconImage()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("StickyburritoIcon.png") ?? throw new InvalidOperationException("Installer artwork is missing.");
            return new Bitmap(stream);
        }

        private void BrowseForInstallLocation()
        {
            using var dialog = new FolderBrowserDialog { Description = "Choose where Stickyburrito's Prompt Generator should be installed", UseDescriptionForTitle = true, SelectedPath = Directory.Exists(installLocation.Text) ? installLocation.Text : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ShowNewFolderButton = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) installLocation.Text = Path.Combine(dialog.SelectedPath, "Stickyburrito's Prompt Generator");
        }

        private static string ValidateInstallLocation(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Choose an installation folder.");
            var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim()));
            if (string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The root of a drive cannot be used as the installation folder.");
            return fullPath;
        }

        private void CancelOrClose()
        {
            if (installationCancellation is null) { Close(); return; }
            cancel.Enabled = false;
            Write("Cancelling installation…");
            installationCancellation.Cancel();
            try { if (activeProcess is { HasExited: false }) activeProcess.Kill(true); } catch { }
            cancel.Enabled = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var baseGradient = new LinearGradientBrush(ClientRectangle, Color.FromArgb(4, 8, 28), Color.FromArgb(42, 3, 46), 35f);
            e.Graphics.FillRectangle(baseGradient, ClientRectangle);
            using var cyanGlow = new SolidBrush(Color.FromArgb(22, 0, 218, 255));
            using var pinkGlow = new SolidBrush(Color.FromArgb(28, 255, 0, 145));
            e.Graphics.FillEllipse(cyanGlow, -180, Height - 370, 560, 500);
            e.Graphics.FillEllipse(pinkGlow, Width - 390, -180, 560, 440);
        }
    }

    private sealed class NeonButton : Button
    {
        internal NeonButton() { FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; ForeColor = Color.FromArgb(8, 7, 22); Font = new Font("Segoe UI", 10.5f, FontStyle.Bold); Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(255, 174, 40), Color.FromArgb(244, 17, 151), 0f);
            e.Graphics.FillRectangle(brush, ClientRectangle);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
