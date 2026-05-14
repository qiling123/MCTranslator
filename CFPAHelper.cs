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
                    using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "MCTranslator");
                        // 如果有 Token 就加上，没有则跳过
                        var tokenToUse = string.IsNullOrWhiteSpace(gitHubToken) ? GITHUB_TOKEN : gitHubToken;
                        if (!string.IsNullOrWhiteSpace(tokenToUse))
                        {
                            client.DefaultRequestHeaders.Authorization =
                                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenToUse);
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
                            using (var fileResponse = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                            {
                                fileResponse.EnsureSuccessStatusCode();
                                long totalBytes = fileResponse.Content.Headers.ContentLength ?? -1;
                                using (var stream = await fileResponse.Content.ReadAsStreamAsync())
                                using (var fs = File.Create(zipFile))
                                {
                                    byte[] buffer = new byte[81920];
                                    long totalRead = 0;
                                    int bytesRead;
                                    while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                                    {
                                        await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
                                        totalRead += bytesRead;
                                        if (totalBytes > 0)
                                        {
                                            int percent = (int)(totalRead * 100 / totalBytes);
                                            progress?.Report($"下载 CFPA 资源包: {percent}% ({totalRead}/{totalBytes} 字节)");
                                        }
                                        else
                                        {
                                            progress?.Report($"下载 CFPA 资源包: {totalRead} 字节 已下载...");
                                        }
                                    }
                                }
                            }

                            File.WriteAllText(lastUpdateFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            downloadSuccess = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"GitHub API 下载失败：{ex.Message}");
                    MCTranslator.ErrorLogger.Log($"CFPA GitHub 下载失败: {ex}");
                }

                // 2. 如果 API 方式失败，尝试备用地址
                if (!downloadSuccess)
                {
                    try
                    {
                        using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                        {
                            client.DefaultRequestHeaders.Add("User-Agent", "MCTranslator");
                            progress?.Report("尝试备用下载地址...");
                            using (var response = await client.GetAsync(CFPA_DOWNLOAD_URL, HttpCompletionOption.ResponseHeadersRead))
                            {
                                response.EnsureSuccessStatusCode();
                                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                                using (var stream = await response.Content.ReadAsStreamAsync())
                                using (var fs = File.Create(zipFile))
                                {
                                    byte[] buffer = new byte[81920];
                                    long totalRead = 0;
                                    int bytesRead;
                                    while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
                                    {
                                        await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
                                        totalRead += bytesRead;
                                        if (totalBytes > 0)
                                        {
                                            int percent = (int)(totalRead * 100 / totalBytes);
                                            progress?.Report($"下载 CFPA 资源包(备用): {percent}% ({totalRead}/{totalBytes} 字节)");
                                        }
                                        else
                                        {
                                            progress?.Report($"下载 CFPA 资源包(备用): {totalRead} 字节 已下载...");
                                        }
                                    }
                                }
                            }

                            File.WriteAllText(lastUpdateFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            downloadSuccess = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"备用下载地址失败：{ex.Message}");
                        MCTranslator.ErrorLogger.Log($"CFPA 备用下载失败: {ex}");
                    }
                }

                if (!downloadSuccess)
                {
                    progress?.Report("所有下载地址均失败，将仅使用 AI 翻译。");
                    MCTranslator.ErrorLogger.Log($"CFPA 下载失败，所有地址均不可用");
                    if (File.Exists(zipFile))
                        progress?.Report("回退到旧的本地缓存。");
                }
            }

            // 解析本地 zip
            if (File.Exists(zipFile))
            {
                progress?.Report("正在解析 CFPA 翻译数据...");
                int before = CachedTranslations.Count;
                ParseCFPAZip(zipFile, progress);
                int after = CachedTranslations.Count;
                progress?.Report($"解析完成，共 {after} 条，新增 {after - before} 条。");
            }

            IsLoaded = true;
            return CachedTranslations;
        }

        private static void ParseCFPAZip(string zipFile, IProgress<string>? progress = null)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            try
            {
                ZipFile.ExtractToDirectory(zipFile, tempDir);
                string assetsPath = Path.Combine(tempDir, "assets");
                if (!Directory.Exists(assetsPath)) return;
                var modDirs = Directory.GetDirectories(assetsPath);
                int totalMods = modDirs.Length;
                int processed = 0;
                foreach (string modDir in modDirs)
                {
                    processed++;
                    string modId = Path.GetFileName(modDir);
                    string zhJson = Path.Combine(modDir, "lang", "zh_cn.json");
                    if (!File.Exists(zhJson))
                    {
                        progress?.Report($"解析模块 {modId} ({processed}/{totalMods})：无 zh_cn.json，跳过...");
                        continue;
                    }

                    try
                    {
                        var json = JObject.Parse(File.ReadAllText(zhJson, Encoding.UTF8));
                        foreach (var prop in json)
                        {
                            string key = $"{modId}:{prop.Key}";
                            if (!CachedTranslations.ContainsKey(key))
                                CachedTranslations[key] = prop.Value!.ToString();
                        }
                        progress?.Report($"解析模块 {modId} ({processed}/{totalMods})：已加载 {json.Count} 条");
                    }
                    catch (Exception ex)
                    {
                        progress?.Report($"解析模块 {modId} 失败: {ex.Message}");
                    }
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