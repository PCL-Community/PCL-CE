using System;

namespace PCL.Core.IO.Download.Core;

public class FailedOperationException(string msg, Exception? innerException = null) : Exception(msg, innerException);