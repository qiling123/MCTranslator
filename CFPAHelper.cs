using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MCTranslator
{
    public static class CFPAHelper
    {
        // GitHub API 地址，指向 autobuild 标签
        private const string API_URL = "https://api.github.com/repos/CFPAOrg/Minecraft-Mod-Language-Package/releases/tags/autobuild";
        // 备用直接下载地址
        private const string CFPA_DOWNLOAD_URL = "https://cfpa.site/download/";
        // GitHub Token（可选，不填也可以，仅提升速率限制）
        private const string GITHUB_TOKEN = ""; // 如 "ghp_xxxxxxxxxxxxxxxxxxxx"

        public static Dictionary<string, string> CachedTranslations = new Dictionary<string, string>();
        public static bool IsLoaded = false;

        private static readonly string CacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MCTranslator", "CFPA");

        private static readonly TimeSpan CacheExpiry = TimeSpan.FromDays(7);

        public static async Task<Dictionary<string, string>> LoadCFPATranslationsAsync(
    IProgress<string>? progress = null,
    string? gitHubToken = null)   // ← 新增这个参数
        {
            if (IsLoaded) return CachedTranslations;

            string zipFile = Path.Combine(CacheDir, "Minecraft-Mod-Language-Package.zip");
            string lastUpdateFile = Path.Combine(CacheDir, "last_update.txt");

            bool needDownload = true;

            // 检查缓存
            if (File.Exists(zipFile) && File.Exists(lastUpdateFile))
            {
                try
                {
                    string dateStr = File.ReadAllText(lastUpdateFile).Trim();
                    if (DateTime.TryParse(dateStr, out DateTime lastUpdate))
                    {
                        if (DateTime.Now - lastUpdate < CacheExpiry)
                        {
                            needDownload = false;
                            progress?.Report("使用本地缓存的 CFPA 资源包...");
                        }
                    }
                }
                catch { }
            }

            if (needDownload)
            {
                progress?.Report("正在获取 CFPA 资源包信息...");
                bool downloadSuccess = false;
                Directory.CreateDirectory(CacheDir);

                // 1. 尝试通过 GitHub API 下载
                try
                {
                    using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "MCTranslator");
                        // 如果有 Token 就加上，没有则跳过
                        if (!string.IsNullOrEmpty(GITHUB_TOKEN))
                        {
                            client.DefaultRequestHeaders.Authorization =
                                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GITHUB_TOKEN);
                        }

                        var response = await client.GetStringAsync(API_URL);
                        var release = JObject.Parse(response);
                        var assets = release["assets"] as JArray;

                        string? downloadUrl = null;
                        if (assets != null)
                        {
                            foreach (var asset in assets)
                            {
                                var name = asset["name"]?.ToString();
                                if (name?.EndsWith(".zip") == true && (name.Contains("Package") || name.Contains("Language")))
                                {
                                    downloadUrl = asset["browser_download_url"]?.ToString();
                                    break;
                                }
                            }
                        }

                        downloadUrl ??= release["zipball_url"]?.ToString();

                        if (downloadUrl != null)
                        {
                            progress?.Report("正在下载 CFPA 汉化资源包...");
                            var fileResponse = await client.GetAsync(downloadUrl);
                            fileResponse.EnsureSuccessStatusCode();
                            using (var fs = File.Create(zipFile))
                                await fileResponse.Content.CopyToAsync(fs);

                            File.WriteAllText(lastUpdateFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            downloadSuccess = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"GitHub API 下载失败：{ex.Message}");
                }

                // 2. 如果 API 方式失败，尝试备用地址
                if (!downloadSuccess)
                {
                    try
                    {
                        using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
                        {
                            client.DefaultRequestHeaders.Add("User-Agent", "MCTranslator");
                            progress?.Report("尝试备用下载地址...");
                            var response = await client.GetAsync(CFPA_DOWNLOAD_URL);
                            response.EnsureSuccessStatusCode();
                            using (var fs = File.Create(zipFile))
                                await response.Content.CopyToAsync(fs);
                            File.WriteAllText(lastUpdateFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            downloadSuccess = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"备用下载地址失败：{ex.Message}");
                    }
                }

                if (!downloadSuccess)
                {
                    progress?.Report("所有下载地址均失败，将仅使用 AI 翻译。");
                    if (File.Exists(zipFile))
                        progress?.Report("回退到旧的本地缓存。");
                }
            }

            // 解析本地 zip
            if (File.Exists(zipFile))
            {
                progress?.Report("正在解析 CFPA 翻译数据...");
                ParseCFPAZip(zipFile);
            }

            IsLoaded = true;
            return CachedTranslations;
        }

        private static void ParseCFPAZip(string zipFile)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                ZipFile.ExtractToDirectory(zipFile, tempDir);
                string assetsPath = Path.Combine(tempDir, "assets");
                if (!Directory.Exists(assetsPath)) return;

                foreach (string modDir in Directory.GetDirectories(assetsPath))
                {
                    string modId = Path.GetFileName(modDir);
                    string zhJson = Path.Combine(modDir, "lang", "zh_cn.json");
                    if (!File.Exists(zhJson)) continue;

                    try
                    {
                        var json = JObject.Parse(File.ReadAllText(zhJson, Encoding.UTF8));
                        foreach (var prop in json)
                        {
                            string key = $"{modId}:{prop.Key}";
                            if (!CachedTranslations.ContainsKey(key))
                                CachedTranslations[key] = prop.Value!.ToString();
                        }
                    }
                    catch { }
                }
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        public static void ClearCache()
        {
            try
            {
                if (Directory.Exists(CacheDir))
                    Directory.Delete(CacheDir, true);
            }
            catch { }
        }
    }
}