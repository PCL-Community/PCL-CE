using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PCL.Core.App.Localization;

namespace PCL.Core.Logging;

/// <summary>
///     将异常信息格式化为面向用户的消息，同时不改变原始的失败原因。
/// </summary>
public static partial class ExceptionDetails
{
    private const int MaxSingleMessageLength = 4096;
    private const int MaxCombinedMessageLength = 8192;

    /// <summary>
    ///     返回异常链中去重后的消息，从最外层异常依次到内部异常。
    /// </summary>
    public static string GetUserReason(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var messages = new List<string>();
        var seenMessages = new HashSet<string>(StringComparer.Ordinal);
        var current = exception;

        // 异常链通常很短。这里的深度限制也能避免格式异常的链破坏 UI 展示。
        for (var depth = 0; current is not null && depth < 32; depth++, current = current.InnerException)
        {
            var message = current.Message?.Trim();
            if (string.IsNullOrWhiteSpace(message) || string.Equals(message, "$$", StringComparison.Ordinal))
                continue;
            message = _TruncateForUser(_RedactSensitiveText(message));
            if (seenMessages.Add(message))
                messages.Add(message);
        }

        var result = string.Join(Environment.NewLine, messages);
        return _TruncateForUser(result, MaxCombinedMessageLength);
    }

    private static string _TruncateForUser(string text, int? maxLength = null)
    {
        var limit = maxLength ?? MaxSingleMessageLength;
        return text.Length <= limit
            ? text
            : text[..limit] + Environment.NewLine + "…";
    }

    [GeneratedRegex(
        """(\b(?:access[_-]?token|refresh[_-]?token|client[_-]?secret|password|authorization)\b\s*[:=]\s*)(?:"[^"]*"|\S+)""",
        RegexOptions.IgnoreCase)]
    private static partial Regex _KeyValueCredentialPattern();

    [GeneratedRegex(
        @"([?&](?:access_token|refresh_token|token|password|client_secret)=)[^&\s]+",
        RegexOptions.IgnoreCase)]
    private static partial Regex _QueryStringCredentialPattern();

    [GeneratedRegex(
        @"\bBearer\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.IgnoreCase)]
    private static partial Regex _BearerTokenPattern();

    private static string _RedactSensitiveText(string text)
    {
        // 在保留失败原因可见的前提下，避免在普通用户界面中直接暴露明显的凭据信息。
        text = _KeyValueCredentialPattern().Replace(text, "$1[redacted]");
        text = _QueryStringCredentialPattern().Replace(text, "$1[redacted]");
        text = _BearerTokenPattern().Replace(text, "Bearer [redacted]");
        return text;
    }

    /// <summary>
    ///     返回完整的异常文本，用于调试和日志记录。
    /// </summary>
    public static string GetDebugDetails(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception.ToString();
    }

    /// <summary>
    ///     将完整的原始异常文本附加到一个稳定的本地化摘要后面。
    /// </summary>
    public static string Compose(string summary, Exception? exception = null)
    {
        return Compose(summary, exception is null ? null : GetDebugDetails(exception));
    }

    /// <summary>
    ///     将调用方提供的详细文本附加到稳定的本地化摘要后面，而不修改该详细文本。
    /// </summary>
    public static string Compose(string summary, string? details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        return string.IsNullOrWhiteSpace(details)
            ? summary
            : Lang.Text("SystemDialog.Error.Detail.WithException", summary.TrimEnd(), details);
    }
}