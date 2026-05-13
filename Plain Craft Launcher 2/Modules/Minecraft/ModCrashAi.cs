using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Network;

namespace PCL;

public static class CrashAiAnalyzer
{
    private const int MaxLogLength = 90000;
    private const string SystemPrompt = """
        你是 Minecraft 崩溃日志分析助手。请用简体中文回答，语气清晰克制。
        先给出最可能结论，再列出证据、建议操作和仍需补充的信息。
        不要编造日志中没有的事实；如果信息不足，请明确说明。
        优先识别 Mod 冲突、缺失依赖、Java/显卡/内存问题、启动参数问题和版本不兼容。
        """;

    public static bool IsEnabled => Config.Tool.AI.Enabled;

    public static void Start(string logText)
    {
        if (!Config.Tool.AI.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(Config.Tool.AI.ApiKey))
        {
            ModMain.MyMsgBox("请先在 设置 → 工具 → AI 中填写 API Key。", "AI 分析未配置");
            return;
        }

        if (string.IsNullOrWhiteSpace(Config.Tool.AI.BaseUrl) || string.IsNullOrWhiteSpace(Config.Tool.AI.ModelId))
        {
            ModMain.MyMsgBox("请先在 设置 → 工具 → AI 中填写 Base URL 和 Model ID。", "AI 分析未配置");
            return;
        }

        ModMain.ShowCrashAiAnalysis(TrimLog(logText));
    }

    public static string Analyze(string logText)
    {
        return Config.Tool.AI.ApiType switch
        {
            1 => AnalyzeWithChatCompletions(logText),
            _ => AnalyzeWithResponses(logText)
        };
    }

    public static string ApiTypeName => Config.Tool.AI.ApiType == 1 ? "Chat Completions API" : "Responses API";

    private static string AnalyzeWithResponses(string logText)
    {
        var payload = new JObject
        {
            ["model"] = Config.Tool.AI.ModelId.Trim(),
            ["instructions"] = SystemPrompt,
            ["input"] = logText,
            ["max_output_tokens"] = 1800
        };

        var json = RequestJson(GetEndpoint(Config.Tool.AI.BaseUrl, "responses"), payload);
        var text = json["output_text"]?.ToString();
        if (!string.IsNullOrWhiteSpace(text))
            return text;

        var parts = json["output"]?
            .SelectMany(item => item["content"] ?? new JArray())
            .Where(content => content["type"]?.ToString() == "output_text")
            .Select(content => content["text"]?.ToString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (parts is { Count: > 0 })
            return string.Join("\n\n", parts);

        throw new Exception("OpenAI 返回中没有可显示的文本内容。");
    }

    private static string AnalyzeWithChatCompletions(string logText)
    {
        var payload = new JObject
        {
            ["model"] = Config.Tool.AI.ModelId.Trim(),
            ["messages"] = new JArray
            {
                new JObject
                {
                    ["role"] = "system",
                    ["content"] = SystemPrompt
                },
                new JObject
                {
                    ["role"] = "user",
                    ["content"] = logText
                }
            },
            ["max_tokens"] = 1800
        };

        var json = RequestJson(GetEndpoint(Config.Tool.AI.BaseUrl, "chat/completions"), payload);
        var content = json["choices"]?.FirstOrDefault()?["message"]?["content"];
        if (content?.Type == JTokenType.String && !string.IsNullOrWhiteSpace(content.ToString()))
            return content.ToString();

        if (content is JArray parts)
        {
            var text = parts
                .Select(part => part["text"]?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            if (text.Count > 0)
                return string.Join("\n\n", text);
        }

        throw new Exception("OpenAI 兼容接口返回中没有可显示的文本内容。");
    }

    private static JObject RequestJson(string endpoint, JObject payload)
    {
        try
        {
            var response = Requester.Fetch(endpoint, new FetchParam
            {
                Method = "POST",
                Content = payload.ToString(Formatting.None),
                ContentType = "application/json",
                Accept = "application/json",
                Encoding = Encoding.UTF8,
                Timeout = 120000,
                Headers = new Dictionary<string, string>
                {
                    ["Authorization"] = "Bearer " + Config.Tool.AI.ApiKey.Trim()
                }
            });

            return JObject.Parse(response);
        }
        catch (Exception ex)
        {
            throw new Exception($"请求 {ApiTypeName} 失败（{endpoint}）", ex);
        }
    }

    private static string GetEndpoint(string baseUrl, string relativeEndpoint)
    {
        var url = baseUrl.Trim().TrimEnd('/');
        url = RemoveEndpointSuffix(url, "/responses");
        url = RemoveEndpointSuffix(url, "/chat/completions");

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')))
            url += "/v1";

        return $"{url}/{relativeEndpoint}";
    }

    private static string RemoveEndpointSuffix(string url, string suffix)
    {
        return url.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? url[..^suffix.Length] : url;
    }

    private static string TrimLog(string logText)
    {
        if (logText.Length <= MaxLogLength)
            return logText;
        return logText[..(MaxLogLength / 2)] +
               "\n\n...... 日志过长，中间部分已省略 ......\n\n" +
               logText[^((MaxLogLength / 2) - 100)..];
    }
}
