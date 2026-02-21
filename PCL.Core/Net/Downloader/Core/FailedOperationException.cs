using System;

namespace PCL.Core.Net.Downloader.Core;

public class FailedOperationException(string msg, Exception? innerException = null) : Exception(msg, innerException);