namespace PCL.Xaml;

/// <summary>AXAML 主页的校验结果。</summary>
public sealed class AxamlValidationResult
{
    public List<string> Errors { get; } = [];

    /// <summary>片段顶层元素数量。纯文本或 JSON 内容为 0。</summary>
    public int ElementCount { get; internal set; }

    public bool IsValid => Errors.Count == 0;

    public void Fail(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Errors.Add(message);
    }
}