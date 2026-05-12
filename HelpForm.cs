using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace MCTranslator
{
    public class HelpForm : Form
    {
        public HelpForm()
        {
            this.Text = "使用说明";
            this.Size = new Size(650, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            this.Controls.Add(panel);

            int y = 10;

            // 标题
            var title = new Label
            {
                Text = "Minecraft 资源包/光影/整合包汉化器",
                Font = new Font("微软雅黑", 12, FontStyle.Bold),
                Location = new Point(20, y),
                AutoSize = true
            };
            panel.Controls.Add(title);
            y += 40;

            // ── 使用方法 ──
            AddSection(panel, "【使用方法】", ref y);
            AddText(panel, "1. 选择翻译引擎（百度翻译 / DeepSeek），填入对应的 API 密钥。", ref y);
            AddText(panel, "2. 选择要汉化的文件：", ref y);
            AddText(panel, "   · 资源包/光影包：点击「选择压缩包」或拖拽 .zip 文件。", ref y);
            AddText(panel, "   · 整合包：点击「选择整合包文件夹」，选择 mods 目录。", ref y);
            AddText(panel, "3. 点击「翻译」，等待进度完成。", ref y);
            AddText(panel, "4. 点击「保存汉化包」，将生成的 zip 放入对应目录：", ref y);
            AddText(panel, "   · 资源包 → .minecraft/resourcepacks", ref y);
            AddText(panel, "   · 光影包 → .minecraft/shaderpacks", ref y);
            AddText(panel, "   · 整合包 → resourcepacks（建议配合 Resource Loader 模组）", ref y);
            AddText(panel, "5. 在游戏中启用该资源包。", ref y);
            y += 10;

            // ── CFPA 社区汉化 ──
            AddSection(panel, "【关于 CFPA 社区汉化资源包】", ref y);
            AddText(panel, "本工具可自动下载 CFPA 社区维护的汉化资源包，覆盖数千个模组。", ref y);
            AddText(panel, "首次使用时会自动下载（约需几分钟），之后 7 天内使用本地缓存。", ref y);
            AddText(panel, "勾选「优先使用 CFPA 社区汉化」后，社区已有的翻译直接使用，", ref y);
            AddText(panel, "仅对缺失的条目进行 AI 翻译，大幅减少翻译时间。", ref y);
            AddLink(panel, "CFPA 项目仓库：https://github.com/CFPAOrg/Minecraft-Mod-Language-Package",
                "https://github.com/CFPAOrg/Minecraft-Mod-Language-Package", ref y);
            y += 10;

            // ── 格式转换工具 ──
            AddSection(panel, "【格式转换与补全工具】", ref y);
            AddText(panel, "如需将 .lang 转为 .json 或补全语言文件，可点击主界面的", ref y);
            AddText(panel, "「汉化小工具」按钮，在浏览器中打开在线工具。", ref y);
            AddLink(panel, "汉化小工具：https://tt.nptr.cc/", "https://tt.nptr.cc/", ref y);
            y += 10;

            // ── 密钥获取 ──
            AddSection(panel, "【百度翻译密钥获取】", ref y);
            AddLink(panel, "打开百度翻译开放平台：https://fanyi-api.baidu.com",
                "https://fanyi-api.baidu.com", ref y);
            AddText(panel, "注册并登录 → 开通「通用翻译」→ 控制台获取 APP ID 和密钥。", ref y);
            AddText(panel, "每月免费 100 万字符，个人使用完全足够。", ref y);
            y += 10;

            AddSection(panel, "【DeepSeek 密钥获取】", ref y);
            AddLink(panel, "打开 DeepSeek 开放平台：https://platform.deepseek.com",
                "https://platform.deepseek.com", ref y);
            AddText(panel, "注册并登录 → API Keys → 创建密钥。", ref y);
            AddText(panel, "需充值任意金额（最低 1 元），翻译成本极低。", ref y);
            y += 10;

            // ── 版权声明 ──
            AddSection(panel, "【版权声明】", ref y);
            AddText(panel, "本软件由 B站UP主「南_岳」原创开发。", ref y);
            AddText(panel, "未经作者允许，禁止将本软件用于任何商业用途，违者将承担法律责任。", ref y);
            AddText(panel, "本工具使用的 CFPA 汉化资源包遵循 CC BY-NC-SA 4.0 协议。", ref y);
            AddText(panel, "感谢 CFPAOrg 团队及所有社区贡献者的辛勤付出。", ref y);
            AddText(panel, "请关注 B站「南_岳」支持更多原创工具！", ref y);
        }

        private void AddSection(Panel panel, string text, ref int y)
        {
            var label = new Label
            {
                Text = text,
                Font = new Font("微软雅黑", 10, FontStyle.Bold),
                Location = new Point(20, y),
                AutoSize = true
            };
            panel.Controls.Add(label);
            y += 25;
        }

        private void AddText(Panel panel, string text, ref int y)
        {
            var label = new Label
            {
                Text = text,
                Font = new Font("微软雅黑", 9),
                Location = new Point(35, y),
                AutoSize = true
            };
            panel.Controls.Add(label);
            y += 20;
        }

        private void AddLink(Panel panel, string text, string url, ref int y)
        {
            var link = new LinkLabel
            {
                Text = text,
                Font = new Font("微软雅黑", 9),
                Location = new Point(35, y),
                AutoSize = true
            };
            link.Links.Add(0, text.Length, url);
            link.LinkClicked += (s, e) =>
            {
                if (e.Link?.LinkData is string linkUrl)
                {
                    Process.Start(new ProcessStartInfo(linkUrl) { UseShellExecute = true });
                }
            };
            panel.Controls.Add(link);
            y += 20;
        }
    }
}