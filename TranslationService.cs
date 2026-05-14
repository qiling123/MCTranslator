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

public class DeepSeekService : ITranslationService, IDisposable
{
    private readonly string apiKey, model, endpoint;
    private readonly string style;
    private readonly HttpClient client;

    private const string SYSTEM_PROMPT = @"
你是一个专业的 Minecraft 模组汉化专家，严格遵循 VM 汉化组与 CFPA 社区翻译规范进行工作。

## 身份与基本原则
- 你翻译的文本将服务于广大中文Minecraft玩家。
- 以原文为准。不要漏译，也不要过度发挥。
- 注意不同时态、词性之间的细微差异，以免误读错译。
- 译文应符合中文语言习惯，符合中文语法和表达方式。
- 保持术语、句式的一致性。
- 禁止套用不适宜的烂梗，尤其是带有负面影响的。

## 格式铁律 (必须100%遵守)
1. 保留所有格式占位符，如 %s, %d, %1$s, %2$s 等，顺序和数量不能改变。
2. 保留所有颜色代码和样式代码，如 §0, §r, &a, &l 等。
3. 原文中的换行符 \n 必须在译文的合理位置保留。

## 术语表 (必须严格遵守)
### 游戏基础术语
- Block → 方块
- Item → 物品
- Entity → 实体
- Mob → 生物
- Biome → 生物群系
- Nether → 下界
- Overworld → 主世界
- The End → 末地
### 游戏机制
- Redstone → 红石
- Enchanting → 附魔
- Potion → 药水
- Crafting → 合成
- Smelting → 烧炼
### 模组与社区
- Mod → 模组
- Resource Pack → 资源包
- Shader → 光影
- Server → 服务器
### 界面与交互
- Inventory → 物品栏
- Hotbar → 快捷栏
- GUI → 图形用户界面
- Keybind → 按键绑定
### 生物
- Wither → 凋灵
- Ender Dragon → 末影龙
- Villager → 村民
- Pillager → 掠夺者

## 排版规范
- 英文或半角数字与中文之间应当添加空格。
- 中文语句应使用全角标点符号。

## 翻译策略
- 遇到自造词、俚语或文化梗时，结合上下文进行意译或创译。
- 对于科技模组，使用专业、准确的术语，避免口语化。
- 对于魔法或奇幻模组，可以适当使用古风或富有诗意的表达。
- 如果原文有误，按正确含义翻译，并在译文中保持通顺。
";

    public DeepSeekService(string apiKey, string model = "deepseek-chat", string endpoint = "https://api.deepseek.com/v1/chat/completions", string style = "标准")
    {
        this.apiKey = apiKey;
        this.model = model;
        this.endpoint = endpoint;
        this.style = string.IsNullOrWhiteSpace(style) ? "标准" : style;
        client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<List<string>> TranslateAsync(List<string> texts)
    {
        if (texts == null) throw new ArgumentNullException(nameof(texts));

        var prompt = new StringBuilder();
        prompt.AppendLine("将以下 Minecraft 模组英文文本翻译为简体中文。");
        prompt.AppendLine("严格遵循 System Prompt 中的所有规范和术语表。");
        prompt.AppendLine("只返回一个 JSON 字符串数组，格式：[\"译文1\", \"译文2\"]");
        prompt.AppendLine("不要包含任何其他解释、说明或 Markdown 代码块标记。");
        prompt.AppendLine("输入：");
        for (int i = 0; i < texts.Count; i++)
            prompt.AppendLine($"{i}: {texts[i] ?? string.Empty}");
        prompt.Append("输出 JSON 数组：");

        var body = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = SYSTEM_PROMPT },
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
            if (result == null) throw new Exception("API未返回结果");

            if (result["error"] != null)
            {
                var errorObj = result["error"];
                var message = errorObj?["message"]?.ToString() ?? "未知错误";
                throw new Exception($"API错误: {message}");
            }

            // Safely extract the model reply from choices
            string reply;
            var choicesToken = result["choices"];
            if (choicesToken is JArray choicesArr && choicesArr.Count > 0)
            {
                var firstChoice = choicesArr[0];
                reply = firstChoice?[
                    "message"]?["content"]?.ToString() ?? string.Empty;
            }
            else
            {
                reply = result["choices"]?.ToString() ?? string.Empty;
            }

            // Ensure trimmed and remove common fencing
            reply = reply?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reply)) throw new Exception("AI未返回有效翻译数组");
            if (reply.StartsWith("```"))
            {
                int idx = reply.IndexOf("\n");
                if (idx >= 0) reply = reply.Substring(idx + 1).Trim();
                else if (reply.Length >= 3) reply = reply.Substring(3).Trim();
            }
            if (reply.EndsWith("```")) reply = reply.Substring(0, reply.LastIndexOf("```")).Trim();

            // Some models may wrap response with single quotes or other wrappers; try to locate the JSON array inside
            int firstBracket = reply.IndexOf('[');
            int lastBracket = reply.LastIndexOf(']');
            if (firstBracket >= 0 && lastBracket > firstBracket)
            {
                reply = reply.Substring(firstBracket, lastBracket - firstBracket + 1);
            }

            JArray arr = JArray.Parse(reply);
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

    public void Dispose()
    {
        try { client?.Dispose(); } catch { }
    }
}