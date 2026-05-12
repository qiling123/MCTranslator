using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace MCTranslator
{
    public class ModpackTextEntry
    {
        public string ModId { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string SourceText { get; set; } = string.Empty;
    }

    public class ModpackScanResult
    {
        public List<ModpackTextEntry> Entries { get; set; } = new List<ModpackTextEntry>();
        // 记录每个模组ID，保存时用
        public List<string> ModIds { get; set; } = new List<string>();
    }

    public static class ModpackAnalyzer
    {
        public static ModpackScanResult ScanFolder(string folderPath)
        {
            var result = new ModpackScanResult();
            var jarFiles = Directory.GetFiles(folderPath, "*.jar", SearchOption.AllDirectories);
            // 为了方便，也包含可能直接解压的模组文件夹
            var modFolders = Directory.GetDirectories(folderPath, "*", SearchOption.TopDirectoryOnly);

            foreach (var jarFile in jarFiles)
            {
                ExtractFromJar(jarFile, result);
            }
            // 如果用户直接把模组解压了，也扫描
            foreach (var folder in modFolders)
            {
                ExtractFromFolder(folder, result);
            }
            return result;
        }

        private static void ExtractFromJar(string jarPath, ModpackScanResult result)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(jarPath))
                {
                    var langEntries = archive.Entries.Where(e =>
                        e.FullName.StartsWith("assets/") &&
                        e.Name.Equals("en_us.json", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (langEntries.Count == 0)
                        langEntries = archive.Entries.Where(e =>
                            e.FullName.StartsWith("assets/") &&
                            e.Name.Equals("en_US.lang", StringComparison.OrdinalIgnoreCase)).ToList();

                    foreach (var entry in langEntries)
                    {
                        string path = entry.FullName;
                        // 格式：assets/<modid>/lang/
                        string[] parts = path.Split('/');
                        if (parts.Length < 4) continue;
                        string modId = parts[1]; // 第二个文件夹就是模组ID
                        if (!result.ModIds.Contains(modId))
                            result.ModIds.Add(modId);

                        using (StreamReader reader = new StreamReader(entry.Open(), Encoding.UTF8))
                        {
                            if (entry.Name.Equals("en_us.json", StringComparison.OrdinalIgnoreCase))
                            {
                                string json = reader.ReadToEnd();
                                var obj = JObject.Parse(json);
                                foreach (var prop in obj)
                                {
                                    // prop.Value 可能为 null，使用安全调用并回退为空字符串
                                    result.Entries.Add(new ModpackTextEntry
                                    {
                                        ModId = modId,
                                        Key = prop.Key,
                                        SourceText = prop.Value?.ToString() ?? string.Empty
                                    });
                                }
                            }
                            else // .lang
                            {
                                string txt = reader.ReadToEnd();
                                string[] lines = txt.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var line in lines)
                                {
                                    int idx = line.IndexOf('=');
                                    if (idx > 0)
                                    {
                                        string key = line.Substring(0, idx);
                                        string val = line.Substring(idx + 1);
                                        result.Entries.Add(new ModpackTextEntry
                                        {
                                            ModId = modId,
                                            Key = key,
                                            SourceText = val
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 处理 jar 失败，跳过
            }
        }

        private static void ExtractFromFolder(string folderPath, ModpackScanResult result)
        {
            string assetsPath = Path.Combine(folderPath, "assets");
            if (!Directory.Exists(assetsPath)) return;
            var modDirs = Directory.GetDirectories(assetsPath);
            foreach (var modDir in modDirs)
            {
                string langDir = Path.Combine(modDir, "lang");
                if (!Directory.Exists(langDir)) continue;
                string modId = new DirectoryInfo(modDir).Name;
                if (!result.ModIds.Contains(modId))
                    result.ModIds.Add(modId);
                string enJson = Path.Combine(langDir, "en_us.json");
                string enLang = Path.Combine(langDir, "en_US.lang");
                if (File.Exists(enJson))
                {
                    var json = JObject.Parse(File.ReadAllText(enJson));
                    foreach (var prop in json)
                    {
                        result.Entries.Add(new ModpackTextEntry
                        {
                            ModId = modId,
                            Key = prop.Key,
                            SourceText = prop.Value?.ToString() ?? string.Empty
                        });
                    }
                }
                else if (File.Exists(enLang))
                {
                    foreach (var line in File.ReadAllLines(enLang))
                    {
                        int idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            result.Entries.Add(new ModpackTextEntry
                            {
                                ModId = modId,
                                Key = line.Substring(0, idx),
                                SourceText = line.Substring(idx + 1)
                            });
                        }
                    }
                }
            }
        }
        public class DifferentialResult
        {
            public Dictionary<string, ModpackTextEntry> AlreadyTranslated { get; set; } = new();
            public Dictionary<string, ModpackTextEntry> NeedTranslate { get; set; } = new();
            public List<string> ModIds { get; set; } = new();
        }

        public static DifferentialResult ScanWithDifferential(string folderPath)
        {
            var result = new DifferentialResult();
            var rawScan = ScanFolder(folderPath);

            foreach (var entry in rawScan.Entries)
            {
                string fullKey = $"{entry.ModId}:{entry.Key}";
                if (!result.ModIds.Contains(entry.ModId))
                    result.ModIds.Add(entry.ModId);

                // 检查 CFPA 缓存，先确保 CachedTranslations 不为 null
                var cache = CFPAHelper.CachedTranslations;
                if (cache != null && cache.TryGetValue(fullKey, out _))
                {
                    result.AlreadyTranslated[fullKey] = entry;
                }
                else
                {
                    result.NeedTranslate[fullKey] = entry;
                }
            }
            return result;
        }
    }
}