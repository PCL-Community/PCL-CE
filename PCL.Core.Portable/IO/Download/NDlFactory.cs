// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Download;

public abstract class NDlFactory
{
    public abstract IDlConnection? CreateConnection(string resourceId);

    public abstract IDlWriter? CreateWriter(string resourceId);
}

public abstract class NDlFactory<TSourceArgument, TTargetArgument> : NDlFactory
{
    protected abstract IDlResourceMapping<TSourceArgument> SourceMapping { get; }

    protected abstract IDlResourceMapping<TTargetArgument> TargetMapping { get; }

    protected abstract IDlConnection CreateConnection(TSourceArgument source);

    protected abstract IDlWriter CreateWriter(TTargetArgument target);

    public override IDlConnection? CreateConnection(string resourceId)
    {
        var source = SourceMapping.Parse(resourceId);
        return source is null ? null : CreateConnection(source);
    }

    public override IDlWriter? CreateWriter(string resourceId)
    {
        var target = TargetMapping.Parse(resourceId);
        return target is null ? null : CreateWriter(target);
    }
}
