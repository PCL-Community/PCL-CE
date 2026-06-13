using System.Text;

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.Utils.Codecs;

public static class Encodings {
    static Encodings() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static readonly Encoding GB18030 = Encoding.GetEncoding("GB18030");
}
