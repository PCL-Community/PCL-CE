// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using Avalonia.Metadata;
using FluentValidation;

[assembly: XmlnsDefinition("https://ce.pclc.cc/core/utils/validate", "PCL.Desktop.Controls.Legacy.Validation")]

namespace PCL.Desktop.Controls.Legacy.Validation;

public sealed class BlacklistValidator : AbstractValidator<string>
{
    public BlacklistValidator()
    {
        RuleFor(value => value).Must(value =>
        {
            string text = value ?? string.Empty;
            return Blacklist.All(item => !text.Contains(item, StringComparison.Ordinal));
        }).WithMessage("输入内容包含不允许使用的字符");
    }

    public Collection<string> Blacklist { get; } = [];
}

public sealed class IntValidator : AbstractValidator<string>
{
    public IntValidator()
    {
        RuleFor(value => value).Must(value =>
        {
            if (!int.TryParse(value, out int number))
                return false;

            return number >= Min && number <= Max;
        }).WithMessage(_ => $"请输入 {Min} 到 {Max} 之间的整数");
    }

    public int Min { get; set; } = int.MinValue;

    public int Max { get; set; } = int.MaxValue;
}

public sealed class HttpValidator : AbstractValidator<string>
{
    public HttpValidator()
    {
        RuleFor(value => value).Must(value =>
        {
            if (string.IsNullOrWhiteSpace(value))
                return AllowsNullOrEmpty;

            return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }).WithMessage("请输入有效的网址");
    }

    public bool AllowsNullOrEmpty { get; set; }
}
