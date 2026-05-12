using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace MCTranslator
{
    public class AppSettings
    {
        public string? BaiduAppId { get; set; }
        public string? GitHubToken { get; set; }
        public string? BaiduSecret { get; set; }
        public string? DeepSeekKey { get; set; }
        public string? DeepSeekModel { get; set; }
        public int LastEngineIndex { get; set; } // 0=百度, 1=DeepSeek
    }

    public static class SettingsManager
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MCTranslator",
            "settings.dat");

        public static AppSettings Load()
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            try
            {
                string encrypted = File.ReadAllText(FilePath);
                byte[] protectedBytes = Convert.FromBase64String(encrypted);
                byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plainBytes);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            string? dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
            string json = JsonConvert.SerializeObject(settings);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllText(FilePath, Convert.ToBase64String(protectedBytes));
        }
    }
}