using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public interface ITranslationService
{
    Task<List<string>> TranslateAsync(List<string> texts);
}

public class BaiduService : ITranslationService
{
    private readonly string appId, secretKey;
    public BaiduService(string appId, string secretKey)
    {
        this.appId = appId;
        this.secretKey = secretKey;
    }

    public async Task<List<string>> TranslateAsync(List<string> texts)
    {
        if (texts == null) throw new ArgumentNullException(nameof(texts));

        string url = "https://fanyi-api.baidu.com/api/trans/vip/translate";
        string q = string.Join("\n", texts);
        string salt = new Random().Next(100000).ToString();
        string sign = ComputeMD5(appId + q + salt + secretKey);

        using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("q", q),
                new KeyValuePair<string, string>("from", "en"),
                new KeyValuePair<string, string>("to", "zh"),
                new KeyValuePair<string, string>("appid", appId),
                new KeyValuePair<string, string>("salt", salt),
                new KeyValuePair<string, string>("sign", sign),
            });
            var response = await client.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();
            var obj = JObject.Parse(json);
            if (obj["error_code"] != null)
                throw new Exception($"百度翻译错误: {obj["error_msg"]?.ToString() ?? "未知错误"}");

            var transArray = obj["trans_result"] as JArray ?? throw new Exception("百度翻译返回数据为空");

            return transArray.Select(t => t["dst"]?.ToString() ?? string.Empty).ToList();
        }
    }

    private string ComputeMD5(string input)
    {
        using (var md5 = MD5.Create())
        {
            byte[] result = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(result).Replace("-", "").ToLower();
        }
    }
}

public class DeepSeekService : ITranslationService
{
    private readonly string apiKey, model, endpoint;
    private readonly HttpClient client;

    private const string MinecraftTerminology = @"
你必须严格遵守以下 Minecraft 社区专用术语翻译（英文→简体中文）:
Block → 方块
Item → 物品
Entity → 实体
Mob → 生物
Biome → 生物群系
Dimension → 维度
Chunk → 区块
Tick → 刻
Redstone → 红石
Crafting → 合成
Smelting → 烧炼
Enchanting → 附魔
Potion → 药水
Effect → 效果
Particle → 粒子
Shader → 光影
Resource Pack → 资源包
Texture Pack → 材质包
Screen → 屏幕/界面
GUI → 图形用户界面
Inventory → 物品栏
Hotbar → 快捷栏
Armor → 盔甲
Tool → 工具
Weapon → 武器
Food → 食物
Crop → 作物
Villager → 村民
Pillager → 掠夺者
Raid → 袭击
Boss → Boss
Wither → 凋灵
Ender Dragon → 末影龙
Nether → 下界
Overworld → 主世界
The End → 末地
Portal → 传送门
Command → 命令
Gamemode → 游戏模式
Survival → 生存模式
Creative → 创造模式
Hardcore → 极限模式
Spectator → 旁观模式
Multiplayer → 多人游戏
Server → 服务器
Mod → 模组
Forge → Forge（保留英文）
Fabric → Fabric（保留英文）
Skin → 皮肤
Cape → 披风
Screenshot → 截图
Keybind → 按键绑定
Option → 选项
Setting → 设置
Video → 视频
Audio → 音频
Language → 语言
请严格按照上述对照表翻译，未列出的词也请使用 Minecraft 玩家习惯的中文说法，避免机翻味.";

    public DeepSeekService(string apiKey, string model = "deepseek-chat", string endpoint = "https://api.deepseek.com/v1/chat/completions")
    {
        this.apiKey = apiKey;
        this.model = model;
        this.endpoint = endpoint;
        client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<List<string>> TranslateAsync(List<string> texts)
    {
        if (texts == null) throw new ArgumentNullException(nameof(texts));

        var prompt = new StringBuilder();
        prompt.AppendLine("将以下英文文本翻译为简体中文，严格保持顺序，返回一个JSON数组，不要任何额外解释。");
        prompt.AppendLine("输入：");
        for (int i = 0; i < texts.Count; i++)
            prompt.AppendLine($"{i}: {texts[i] ?? string.Empty}");
        prompt.AppendLine("输出JSON数组：");

        var body = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = MinecraftTerminology },
                new { role = "user", content = prompt.ToString() }
            },
            temperature = 0.3
        };

        try
        {
            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(endpoint, content);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"HTTP {response.StatusCode}: {json}");
            }

            var result = JObject.Parse(json);
            if (result["error"] != null)
                throw new Exception($"API错误: {result["error"]["message"]?.ToString() ?? "未知错误"}");

            string reply = result["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
            reply = reply.Trim();
            if (string.IsNullOrWhiteSpace(reply)) throw new Exception("AI未返回有效翻译数组");
            if (reply.StartsWith("```")) reply = reply.Substring(reply.IndexOf("\n") + 1);
            if (reply.EndsWith("```")) reply = reply.Substring(0, reply.LastIndexOf("```"));

            JArray? arr = JArray.Parse(reply);
            if (arr == null) throw new Exception("AI未返回有效翻译数组");
            return arr.Select(t => t.ToString()).ToList();
        }
        catch (TaskCanceledException)
        {
            throw new Exception("请求超时，请检查网络或换用百度翻译。");
        }
        catch (Exception ex)
        {
            throw new Exception($"翻译出错: {ex.Message}");
        }
    }
}