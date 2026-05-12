using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MCTranslator
{
    public partial class Form1 : Form
    {
        // ========== 会话管理 ==========
        private TranslationSession _sessionResource = new TranslationSession();
        private TranslationSession _sessionModpack = new TranslationSession();
        private TextBox CurrentSource => CurrentSession.PackType == "modpack" ? txtSourceModpack : txtSourceResource;
        private TextBox CurrentTarget => CurrentSession.PackType == "modpack" ? txtTargetModpack : txtTargetResource;
        private Label CurrentPathLabel => CurrentSession.PackType == "modpack" ? lblPathModpack : lblPathResource;

        private TranslationSession CurrentSession =>
            (tabMain.SelectedIndex == 2) ? _sessionModpack : _sessionResource;

        // ========== 界面控件 ==========
        private TabControl tabMain = null!;
        private ComboBox cmbEngine = null!;
        private Panel pnlConfig = null!;
        private TextBox? txtBaiduAppId;
        private TextBox? txtBaiduSecret;
        private TextBox? txtDeepSeekKey;
        private TextBox? txtDeepSeekModel;
        private TextBox? txtGitHubToken;
        private bool _useCFPA = true;

        // 资源/光影页独立控件
        private TextBox txtSourceResource = null!;
        private TextBox txtTargetResource = null!;
        private Label lblPathResource = null!;

        // 整合包页独立控件
        private TextBox txtSourceModpack = null!;
        private TextBox txtTargetModpack = null!;
        private Label lblPathModpack = null!;

        // ========== 构造函数 ==========
        public Form1()
        {
            InitializeComponent();
            this.Text = "Minecraft 资源包/光影/整合包汉化器";
            this.Size = new Size(860, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.AllowDrop = true;
            this.Font = new Font("微软雅黑", 9F);

            // 主 TabControl
            tabMain = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.Normal,
                Font = new Font("微软雅黑", 10F)
            };
            Controls.Add(tabMain);

            CreateTabHome(tabMain);
            CreateTabResource(tabMain);
            CreateTabModpack(tabMain);
            CreateTabTools(tabMain);

            // 页签切换事件
            tabMain.SelectedIndexChanged += (s, e) =>
            {
                // CurrentSession 自动根据 tabMain.SelectedIndex 切换
            };

            // 拖放事件
            this.DragEnter += (s, e) =>
            {
                if (e.Data!.GetDataPresent(DataFormats.FileDrop))
                    e.Effect = DragDropEffects.Copy;
                else
                    e.Effect = DragDropEffects.None;
            };
            this.DragDrop += (s, e) =>
            {
                if (e.Data!.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    string file = files[0];
                    if (Path.GetExtension(file).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        lblPathResource.Text = file;
                        ExtractAndAnalyze(file);
                    }
                    else MessageBox.Show("仅支持拖拽 .zip 文件");
                }
            };

            LoadSettings();
            ShowFollowReminder();
        }

        // ============= 四个页签创建方法 =============
        private void CreateTabHome(TabControl tab)
        {
            TabPage page = new TabPage("主页") { BackColor = Color.White };
            tab.TabPages.Add(page);

            // 引擎选择
            Label lblEngine = new Label { Text = "翻译引擎:", Location = new Point(20, 25), AutoSize = true };
            page.Controls.Add(lblEngine);
            cmbEngine = new ComboBox
            {
                Location = new Point(100, 22),
                Width = 160,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbEngine.Items.AddRange(new[] { "百度翻译", "DeepSeek" });
            cmbEngine.SelectedIndex = 0;
            cmbEngine.SelectedIndexChanged += EngineChanged;
            page.Controls.Add(cmbEngine);

            // 动态配置面板
            pnlConfig = new Panel
            {
                Location = new Point(20, 60),
                Size = new Size(800, 110),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(245, 248, 255)
            };
            page.Controls.Add(pnlConfig);

            // GitHub Token 行
            Label lblGitHub = new Label
            {
                Text = "GitHub Token (可选):",
                Location = new Point(20, 190),
                AutoSize = true
            };
            page.Controls.Add(lblGitHub);
            txtGitHubToken = new TextBox
            {
                Location = new Point(180, 187),
                Width = 250,
                PasswordChar = '*',
                PlaceholderText = "留空则使用未认证模式"
            };
            page.Controls.Add(txtGitHubToken);

            EngineChanged(null, EventArgs.Empty);
        }

        private void CreateTabResource(TabControl tab)
        {
            TabPage page = new TabPage("资源/光影包") { BackColor = Color.White };
            tab.TabPages.Add(page);

            int y = 20;
            Button btnSelect = new Button
            {
                Text = "选择压缩包",
                Location = new Point(20, y),
                Size = new Size(110, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            page.Controls.Add(btnSelect);

            lblPathResource = new Label
            {
                Text = "未选择（支持拖拽 .zip 到窗口）",
                Location = new Point(150, y + 5),
                AutoSize = true
            };
            page.Controls.Add(lblPathResource);

            y += 40;
            Label lblSrc = new Label { Text = "英文原文:", Location = new Point(20, y), AutoSize = true, Font = new Font("微软雅黑", 9F, FontStyle.Bold) };
            page.Controls.Add(lblSrc);
            y += 22;

            txtSourceResource = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(20, y),
                Size = new Size(790, 150),
                ReadOnly = true
            };
            page.Controls.Add(txtSourceResource);

            y += 160;
            Button btnTranslate = new Button
            {
                Text = "开始翻译",
                Location = new Point(20, y),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            page.Controls.Add(btnTranslate);

            Button btnSave = new Button
            {
                Text = "保存汉化包",
                Location = new Point(130, y),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            page.Controls.Add(btnSave);

            y += 40;
            Label lblTgt = new Label { Text = "中文译文:", Location = new Point(20, y), AutoSize = true, Font = new Font("微软雅黑", 9F, FontStyle.Bold) };
            page.Controls.Add(lblTgt);
            y += 22;

            txtTargetResource = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(20, y),
                Size = new Size(790, 150),
                ReadOnly = true
            };
            page.Controls.Add(txtTargetResource);

            // 事件绑定
            btnSelect.Click += (s, e) =>
            {
                using OpenFileDialog ofd = new() { Filter = "ZIP 文件|*.zip" };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    lblPathResource.Text = ofd.FileName;
                    ExtractAndAnalyze(ofd.FileName);
                }
            };

            btnTranslate.Click += async (s, e) =>
            {
                // 翻译逻辑会通过 CurrentSession.PackType 判断使用哪个界面的控件
                await TranslateAll();
            };

            btnSave.Click += async (s, e) =>
            {
                if (CurrentSession.PackType == "modpack") { SaveModpackPack(); return; }
                if (string.IsNullOrEmpty(lblPathResource.Text) || !File.Exists(lblPathResource.Text))
                { MessageBox.Show("请先选择一个资源/光影包"); return; }
                string originalName = Path.GetFileNameWithoutExtension(lblPathResource.Text);
                string chineseName = await TranslateFileNameAsync(originalName);
                SavePack(chineseName, CurrentSession.PackType);
            };
        }

        private void CreateTabModpack(TabControl tab)
        {
            TabPage page = new TabPage("整合包") { BackColor = Color.White };
            tab.TabPages.Add(page);

            int y = 20;
            Button btnSelectModpack = new Button
            {
                Text = "选择整合包文件夹",
                Location = new Point(20, y),
                Size = new Size(150, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            page.Controls.Add(btnSelectModpack);

            lblPathModpack = new Label
            {
                Text = "未选择",
                Location = new Point(180, y + 5),
                AutoSize = true
            };
            page.Controls.Add(lblPathModpack);

            CheckBox chkUseCFPA = new CheckBox
            {
                Text = "优先使用 CFPA 社区汉化（推荐）",
                Location = new Point(20, y + 35),
                AutoSize = true,
                Checked = _useCFPA
            };
            chkUseCFPA.CheckedChanged += (s, e) => _useCFPA = chkUseCFPA.Checked;
            page.Controls.Add(chkUseCFPA);

            y += 65;
            Label lblSrc = new Label { Text = "待翻译条目:", Location = new Point(20, y), AutoSize = true, Font = new Font("微软雅黑", 9F, FontStyle.Bold) };
            page.Controls.Add(lblSrc);
            y += 22;

            txtSourceModpack = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(20, y),
                Size = new Size(790, 150),
                ReadOnly = true
            };
            page.Controls.Add(txtSourceModpack);

            y += 160;
            Button btnTranslate = new Button
            {
                Text = "开始翻译",
                Location = new Point(20, y),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            page.Controls.Add(btnTranslate);

            Button btnSave = new Button
            {
                Text = "保存汉化包",
                Location = new Point(130, y),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            page.Controls.Add(btnSave);

            y += 40;
            Label lblTgt = new Label { Text = "翻译结果:", Location = new Point(20, y), AutoSize = true, Font = new Font("微软雅黑", 9F, FontStyle.Bold) };
            page.Controls.Add(lblTgt);
            y += 22;

            txtTargetModpack = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(20, y),
                Size = new Size(790, 150),
                ReadOnly = true
            };
            page.Controls.Add(txtTargetModpack);

            // 事件绑定
            btnSelectModpack.Click += async (s, e) =>
            {
                using FolderBrowserDialog fbd = new() { Description = "选择整合包的 mods 文件夹" };
                if (fbd.ShowDialog() != DialogResult.OK) return;
                lblPathModpack.Text = fbd.SelectedPath;
                if (_useCFPA)
                {
                    await CFPAHelper.LoadCFPATranslationsAsync(
                        progress: null,                           // 或者 new Progress<string>(...)
                            gitHubToken: txtGitHubToken?.Text?.Trim()
                                                              );
                }
                ExtractAndAnalyzeModpack(fbd.SelectedPath);
            };

            btnTranslate.Click += async (s, e) =>
            {
                // 先确保整合包分析已完成，然后再翻译
                // 翻译逻辑会通过 CurrentSession.PackType 判断使用哪个界面的控件
                await TranslateAll();
            };

            btnSave.Click += (s, e) => SaveModpackPack();
        }

        private void CreateTabTools(TabControl tab)
        {
            TabPage page = new TabPage("工具/关于") { BackColor = Color.White };
            tab.TabPages.Add(page);

            int y = 20;
            Button btnFormatTools = new Button
            {
                Text = "汉化小工具 (Lang转Json/补全)",
                Location = new Point(20, y),
                Size = new Size(220, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            btnFormatTools.Click += (s, e) =>
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://tt.nptr.cc/") { UseShellExecute = true });
            };
            page.Controls.Add(btnFormatTools);

            Button btnClearCache = new Button
            {
                Text = "清除 CFPA 缓存",
                Location = new Point(250, y),
                Size = new Size(130, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            btnClearCache.Click += (s, e) =>
            {
                CFPAHelper.ClearCache();
                CFPAHelper.IsLoaded = false;
                MessageBox.Show("CFPA 缓存已清除。", "提示");
            };
            page.Controls.Add(btnClearCache);

            y += 50;
            Button btnHelp = new Button
            {
                Text = "使用说明",
                Location = new Point(20, y),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            btnHelp.Click += (s, e) =>
            {
                new HelpForm().ShowDialog(this);
            };
            page.Controls.Add(btnHelp);

            Button btnAbout = new Button
            {
                Text = "关于 / 版权",
                Location = new Point(130, y),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 120, 212),
                ForeColor = Color.White,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            btnAbout.Click += (s, e) => ShowAbout();
            page.Controls.Add(btnAbout);
        }

        // EngineChanged 保持原有逻辑，但面板位置不变（因为现在 pnlConfig 仍然存在于主页面中）
        private void EngineChanged(object? sender, EventArgs e)
        {
            pnlConfig.Controls.Clear();
            int y = 15;
            if (cmbEngine.SelectedItem?.ToString() == "百度翻译")
            {
                pnlConfig.Controls.Add(new Label { Text = "APP ID", Location = new Point(20, y), AutoSize = true });
                txtBaiduAppId = new TextBox { Location = new Point(80, y - 3), Width = 150 };
                pnlConfig.Controls.Add(txtBaiduAppId);
                pnlConfig.Controls.Add(new Label { Text = "密钥", Location = new Point(250, y), AutoSize = true });
                txtBaiduSecret = new TextBox { Location = new Point(290, y - 3), Width = 200, PasswordChar = '*' };
                pnlConfig.Controls.Add(txtBaiduSecret);
                pnlConfig.Controls.Add(new Label { Text = "每月免费100万字符 🆓", Location = new Point(510, y), AutoSize = true, ForeColor = Color.Green });
            }
            else
            {
                pnlConfig.Controls.Add(new Label { Text = "API Key", Location = new Point(20, y), AutoSize = true });
                txtDeepSeekKey = new TextBox { Location = new Point(80, y - 3), Width = 250, PasswordChar = '*' };
                pnlConfig.Controls.Add(txtDeepSeekKey);
                pnlConfig.Controls.Add(new Label { Text = "模型", Location = new Point(340, y), AutoSize = true });
                txtDeepSeekModel = new TextBox { Location = new Point(380, y - 3), Width = 140, Text = "deepseek-chat" };
                pnlConfig.Controls.Add(txtDeepSeekModel);
                pnlConfig.Controls.Add(new Label { Text = "已内置MC术语约束", Location = new Point(540, y), AutoSize = true, ForeColor = Color.Blue });
            }
        }




        private void ShowHelp()
        {
            HelpForm help = new HelpForm();
            help.ShowDialog(this);
        }

        private void ShowAbout()
        {
            string about = @"
═══════════════════════════════════════
      Minecraft 资源包/光影/整合包汉化器
═══════════════════════════════════════

软件作者：B站UP主「南_岳」
原创作品，未经允许禁止商用，违者必究！

本工具集成以下开源资源：
· CFPA 社区汉化资源包（CC BY-NC-SA 4.0）
  感谢 CFPAOrg 团队及所有贡献者
· 汉化小工具 (tt.nptr.cc)

═══════════════════════════════════════
请关注 B站「南_岳」支持更多原创工具！
═══════════════════════════════════════
";
            MessageBox.Show(about, "关于 / 版权", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Load();
            if (settings.LastEngineIndex >= 0 && settings.LastEngineIndex < cmbEngine.Items.Count)
                cmbEngine.SelectedIndex = settings.LastEngineIndex;

            this.Shown += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(settings.BaiduAppId)) txtBaiduAppId!.Text = settings.BaiduAppId;
                if (!string.IsNullOrWhiteSpace(settings.BaiduSecret)) txtBaiduSecret!.Text = settings.BaiduSecret;
                if (!string.IsNullOrWhiteSpace(settings.DeepSeekKey)) txtDeepSeekKey!.Text = settings.DeepSeekKey;
                if (!string.IsNullOrWhiteSpace(settings.DeepSeekModel)) txtDeepSeekModel!.Text = settings.DeepSeekModel;
            };
            // 请在 lambda 内部已有的赋值语句后面追加
            if (!string.IsNullOrWhiteSpace(settings.GitHubToken))
            {
                var txtToken = pnlConfig.Controls.Find("txtGitHubToken", true).FirstOrDefault() as TextBox;
                if (txtToken != null)
                    txtToken.Text = settings.GitHubToken;
            }
        }

        private void SaveSettings()
        {
            var settings = new AppSettings
            {
                LastEngineIndex = cmbEngine.SelectedIndex,
                BaiduAppId = txtBaiduAppId?.Text?.Trim(),
                BaiduSecret = txtBaiduSecret?.Text?.Trim(),
                DeepSeekKey = txtDeepSeekKey?.Text?.Trim(),
                DeepSeekModel = txtDeepSeekModel?.Text?.Trim(),
                  GitHubToken = (pnlConfig.Controls.Find("txtGitHubToken", true).FirstOrDefault() as TextBox)?.Text?.Trim()
            };
            SettingsManager.Save(settings);
        }

        private void ShowFollowReminder()
        {
            string flagFile = Path.Combine(Path.GetTempPath(), "MCT_reminder_" + DateTime.Today.ToShortDateString().Replace('/', '-'));
            if (File.Exists(flagFile)) return;
            MessageBox.Show("请关注 B站 UP主「南_岳」支持原创工具！\n\n您的关注是我持续更新的动力。", "关注提醒");
            File.WriteAllText(flagFile, "");
        }

        private async Task<string> TranslateFileNameAsync(string englishName)
        {
            if (englishName.Any(c => c > 0x4E00 && c < 0x9FFF)) return englishName;
            try
            {
                string? engine = cmbEngine.SelectedItem?.ToString();
                ITranslationService service = engine == "百度翻译"
                    ? new BaiduService(txtBaiduAppId!.Text.Trim(), txtBaiduSecret!.Text.Trim())
                    : new DeepSeekService(txtDeepSeekKey!.Text.Trim(), txtDeepSeekModel?.Text.Trim() ?? "deepseek-chat");
                var translated = await service.TranslateAsync(new List<string> { englishName });
                if (translated.Count > 0 && !string.IsNullOrWhiteSpace(translated[0])) return translated[0];
            }
            catch { }
            return englishName;
        }
        private async Task DownloadCFPA()
        {
            // 如果已经加载过，直接返回，不重复下载
            if (CFPAHelper.IsLoaded) return;

            CurrentSource.Text = "正在下载 CFPA 汉化资源包（首次使用请耐心等待）...";
            Application.DoEvents();

            var progress = new Progress<string>(msg =>
            {
                CurrentSource.Text = msg;
                Application.DoEvents();
            });

            await CFPAHelper.LoadCFPATranslationsAsync(progress: null, gitHubToken: txtGitHubToken?.Text?.Trim());

            CurrentSource.Text = $"CFPA 社区汉化包已就绪。请选择整合包文件夹。";
        }

        // ============= 资源包/光影分析 =============
        private string? ExtractAndAnalyze(string filePath)
        {
            CurrentSession.SourceDict.Clear();
            // 【断点续翻关键】去掉了 CurrentSession.TargetDict.Clear();
            // 这样切换文件后，已翻译的内容不会丢失
            string? localPackType = null;
            CurrentSession.TempDir = Path.Combine(Path.GetTempPath(), "MCLocalize_" + Guid.NewGuid());
            ZipFile.ExtractToDirectory(filePath, CurrentSession.TempDir);

            string langDir = Path.Combine(CurrentSession.TempDir, "assets", "minecraft", "lang");
            if (Directory.Exists(langDir))
            {
                localPackType = "resource";
                string jsonFile = Path.Combine(langDir, "en_us.json");
                string langFile = Path.Combine(langDir, "en_US.lang");
                if (File.Exists(jsonFile))
                {
                    var json = JObject.Parse(File.ReadAllText(jsonFile, Encoding.UTF8));
                    foreach (var prop in json) CurrentSession.SourceDict[prop.Key] = prop.Value!.ToString();
                }
                else if (File.Exists(langFile))
                {
                    foreach (var line in File.ReadAllLines(langFile, Encoding.UTF8))
                    {
                        int idx = line.IndexOf('=');
                        if (idx > 0) CurrentSession.SourceDict[line[..idx]] = line[(idx + 1)..];
                    }
                }
            }
            else
            {
                string shaderLangDir = Path.Combine(CurrentSession.TempDir, "shaders", "lang");
                if (Directory.Exists(shaderLangDir))
                {
                    localPackType = "shader";
                    var enFile = Directory.GetFiles(shaderLangDir).FirstOrDefault(f => Path.GetFileName(f).Contains("en_", StringComparison.OrdinalIgnoreCase));
                    if (enFile != null)
                    {
                        foreach (var line in File.ReadAllLines(enFile, Encoding.UTF8))
                        {
                            if (line.StartsWith("#")) continue;
                            int idx = line.IndexOf('=');
                            if (idx > 0) CurrentSession.SourceDict[line[..idx]] = line[(idx + 1)..];
                        }
                    }
                }
            }

            if (CurrentSession.SourceDict.Count == 0) { MessageBox.Show("未找到英文文本。"); return null; }
            var sb = new StringBuilder();
            foreach (var kv in CurrentSession.SourceDict) sb.AppendLine($"{kv.Key} = {Truncate(kv.Value, 70)}");
            CurrentSource.Text = sb.ToString();
            CurrentTarget.Text = "";
            return localPackType;
        }
        // ============= 整合包分析 =============
        private void ExtractAndAnalyzeModpack(string folderPath)
        {
            CurrentSession.SourceDict.Clear();
            CurrentSession.ModpackModIds.Clear();

            var diff = ModpackAnalyzer.ScanWithDifferential(folderPath);

            // 1. 把社区已翻译的直接存入 CurrentSession.TargetDict（不再进入待翻译队列）
            foreach (var kv in diff.AlreadyTranslated)
            {
                CurrentSession.TargetDict[kv.Key] = CFPAHelper.CachedTranslations.ContainsKey(kv.Key)
                    ? CFPAHelper.CachedTranslations[kv.Key]
                    : kv.Value.SourceText; // 兜底
            }

            // 2. 把需要AI翻译的条目放入 CurrentSession.SourceDict
            foreach (var kv in diff.NeedTranslate)
            {
                if (!CurrentSession.SourceDict.ContainsKey(kv.Key))
                    CurrentSession.SourceDict[kv.Key] = kv.Value.SourceText;
            }

            CurrentSession.ModpackModIds = diff.ModIds;

            var sb = new StringBuilder();
            sb.AppendLine($"✅ 社区已汉化：{diff.AlreadyTranslated.Count} 条");
            sb.AppendLine($"🤖 待AI翻译：{diff.NeedTranslate.Count} 条");
            sb.AppendLine(new string('-', 40));
            foreach (var kv in CurrentSession.SourceDict)
                sb.AppendLine($"{kv.Key} = {Truncate(kv.Value, 70)}");
            CurrentSource.Text = sb.ToString();
            CurrentTarget.Text = $"社区翻译条目已直接应用，无需再次翻译。";
        }
        // ============= 保存资源包/光影包 =============
        private void SavePack(string suggestedName, string? packTypeToSave)
        {
            if (CurrentSession.TargetDict.Count == 0) { MessageBox.Show("没有翻译内容"); return; }
            if (packTypeToSave == null) { MessageBox.Show("包类型未知"); return; }
            using SaveFileDialog sfd = new() { FileName = suggestedName + ".zip", Filter = "ZIP 文件|*.zip" };
            if (sfd.ShowDialog() != DialogResult.OK || CurrentSession.TempDir == null) return;

            if (packTypeToSave == "resource")
            {
                string langDir = Path.Combine(CurrentSession.TempDir, "assets", "minecraft", "lang");
                Directory.CreateDirectory(langDir);
                File.WriteAllText(Path.Combine(langDir, "zh_cn.json"), JsonConvert.SerializeObject(CurrentSession.TargetDict, Formatting.Indented), Encoding.UTF8);
                File.WriteAllLines(Path.Combine(langDir, "zh_cn.lang"), CurrentSession.TargetDict.Select(kv => $"{kv.Key}={kv.Value}"), Encoding.UTF8);
            }
            else if (packTypeToSave == "shader")
            {
                string shaderDir = Path.Combine(CurrentSession.TempDir, "shaders", "lang");
                Directory.CreateDirectory(shaderDir);
                File.WriteAllLines(Path.Combine(shaderDir, "zh_CN.properties"), CurrentSession.TargetDict.Select(kv => $"{kv.Key}={kv.Value}"), Encoding.UTF8);
            }

            if (File.Exists(sfd.FileName)) File.Delete(sfd.FileName);
            ZipFile.CreateFromDirectory(CurrentSession.TempDir, sfd.FileName);
            MessageBox.Show("汉化包已保存！");
        }

        // ============= 保存整合包资源包 =============
        private void SaveModpackPack()
        {
            if (CurrentSession.TargetDict.Count == 0) { MessageBox.Show("没有翻译内容"); return; }
            using SaveFileDialog sfd = new()
            {
                FileName = "整合包汉化资源包.zip",
                Filter = "ZIP 文件|*.zip"
            };
            if (sfd.ShowDialog() != DialogResult.OK) return;

            string? tempOutDir = null;
            try
            {
                tempOutDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                foreach (var modId in CurrentSession.ModpackModIds)
                {
                    string langDir = Path.Combine(tempOutDir, "assets", modId, "lang");
                    Directory.CreateDirectory(langDir);
                    var modEntries = CurrentSession.TargetDict.Where(kv => kv.Key.StartsWith($"{modId}:"));
                    var langObj = new JObject();
                    foreach (var kv in modEntries)
                    {
                        string realKey = kv.Key.Substring(modId.Length + 1);
                        langObj[realKey] = kv.Value;
                    }
                    File.WriteAllText(Path.Combine(langDir, "zh_cn.json"), langObj.ToString(Formatting.Indented), Encoding.UTF8);
                }

                string mcmeta = @"
{
  ""pack"": {
    ""pack_format"": 15,
    ""supported_formats"": [15, 61],
    ""description"": ""整合包汉化资源包 - 由南_岳的工具生成""
  }
}";
                File.WriteAllText(Path.Combine(tempOutDir, "pack.mcmeta"), mcmeta, Encoding.UTF8);

                if (File.Exists(sfd.FileName)) File.Delete(sfd.FileName);
                ZipFile.CreateFromDirectory(tempOutDir, sfd.FileName);
                MessageBox.Show("整合包汉化资源包已保存！\n可配合 Resource Loader 或 i18n 自动汉化更新模组加载。");
            }
            finally
            {
                if (tempOutDir != null && Directory.Exists(tempOutDir)) Directory.Delete(tempOutDir, true);
            }
        }

        private string Truncate(string str, int max) => str.Length <= max ? str : str[..max] + "...";
        private async Task TranslateAll()
        {
            if (CurrentSession.SourceDict.Count == 0) return;

            var untranslatedKeys = CurrentSession.SourceDict.Keys.Where(k => !CurrentSession.TargetDict.ContainsKey(k)).ToList();
            if (untranslatedKeys.Count == 0)
            {
                CurrentSource.Text = "所有条目已翻译完成。";
                return;
            }
            if (untranslatedKeys.Count != CurrentSession.SourceDict.Count)
            {
                CurrentSource.Text = $"检测到已有部分翻译完成，仅翻译剩余 {untranslatedKeys.Count}/{CurrentSession.SourceDict.Count} 条...";
                Application.DoEvents();
            }

            CurrentSession.Cts?.Cancel();
            CurrentSession.Cts = new CancellationTokenSource();
            var ct = CurrentSession.Cts.Token;

            ITranslationService service;
            string? engine = cmbEngine.SelectedItem?.ToString(); // moved outside try so it's in scope below
            try
            {
                if (engine == "百度翻译")
                {
                    if (string.IsNullOrWhiteSpace(txtBaiduAppId?.Text) || string.IsNullOrWhiteSpace(txtBaiduSecret?.Text))
                    { MessageBox.Show("请填写百度密钥"); return; }
                    service = new BaiduService(txtBaiduAppId.Text.Trim(), txtBaiduSecret.Text.Trim());
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(txtDeepSeekKey?.Text))
                    { MessageBox.Show("请填写 DeepSeek Key"); return; }
                    service = new DeepSeekService(txtDeepSeekKey.Text.Trim(), txtDeepSeekModel?.Text.Trim() ?? "deepseek-chat");
                }
            }
            catch (Exception ex) { MessageBox.Show($"创建翻译服务失败：{ex.Message}"); return; }

            CurrentSession.ErrorCount = 0;
            int batchSize = engine == "百度翻译" ? 30 : 20; // use engine variable declared above
            var pendingPairs = untranslatedKeys.Select(k => new KeyValuePair<string, string>(k, CurrentSession.SourceDict[k])).ToList();
            int totalBatches = (int)Math.Ceiling((double)pendingPairs.Count / batchSize);

            try
            {
                for (int i = 0; i < pendingPairs.Count; i += batchSize)
                {
                    ct.ThrowIfCancellationRequested();  // 每次循环检查

                    var batchPairs = pendingPairs.Skip(i).Take(batchSize).ToList();
                    var batchTexts = batchPairs.Select(p => p.Value).ToList();

                    CurrentSource.Text = $"正在翻译 {i + 1}/{pendingPairs.Count} 条目（第 {i / batchSize + 1}/{totalBatches} 批），已出错 {CurrentSession.ErrorCount} 条...";
                    Application.DoEvents();

                    List<string> batchResult = null!;
                    bool success = false;
                    int retries = 2;
                    while (retries >= 0 && !success)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            batchResult = await service.TranslateAsync(batchTexts);
                            success = true;
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException))
                        {
                            retries--;
                            if (retries < 0)
                            {
                                batchResult = new List<string>(batchTexts);
                                CurrentSession.ErrorCount += batchTexts.Count;
                                this.Text = $"MC 汉化器（最近出错：{ex.Message.Substring(0, Math.Min(ex.Message.Length, 40))}...）";
                                await Task.Delay(500, ct);
                                this.Text = "Minecraft 资源包/光影/整合包汉化器（百度 & DeepSeek）";
                            }
                            else
                            {
                                await Task.Delay(2000, ct);
                            }
                        }
                    }

                    for (int j = 0; j < batchPairs.Count && j < batchResult.Count; j++)
                    {
                        CurrentSession.TargetDict[batchPairs[j].Key] = batchResult[j];
                    }
                }

                var sb = new StringBuilder();
                foreach (var kv in CurrentSession.TargetDict.OrderBy(x => x.Key))
                    sb.AppendLine($"{kv.Key} = {Truncate(kv.Value, 70)}");
                CurrentTarget.Text = sb.ToString();
                CurrentSource.Text = CurrentSession.ErrorCount > 0 ? $"翻译完成，但有 {CurrentSession.ErrorCount} 条因错误使用了原文。" : "翻译完成！可保存汉化包。";
            }
            catch (OperationCanceledException)
            {
                CurrentSource.Text = "翻译已暂停，可再次点击「开始翻译」继续。";
            }
            finally
            {
                CurrentSession.Cts.Dispose();
                CurrentSession.Cts = null;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            if (CurrentSession.TempDir != null && Directory.Exists(CurrentSession.TempDir)) Directory.Delete(CurrentSession.TempDir, true);
            base.OnFormClosing(e);
        }
    }
}