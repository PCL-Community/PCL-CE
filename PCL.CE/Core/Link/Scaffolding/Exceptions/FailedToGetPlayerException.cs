using System;

namespace PCL.CE.Core.Link.Scaffolding.Exceptions;

public class FailedToGetPlayerException : Exception
{
    public FailedToGetPlayerException() : base()
    {
    }

    public FailedToGetPlayerException(string msg) : base(msg)
    {
    }
}