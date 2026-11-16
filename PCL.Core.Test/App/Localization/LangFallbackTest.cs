using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Localization;

namespace PCL.Core.Test.App.Localization;

[TestClass]
public sealed class LangFallbackTest
{
    [TestMethod]
    public void MissingKeyReturnsNonEmptyFallback()
    {
        const string key = "Common.Test.MissingKey";

        var text = Lang.Text(key);

        Assert.IsFalse(string.IsNullOrWhiteSpace(text));
        Assert.Contains(key, text);
    }

    [TestMethod]
    public void MissingKeyWithArgumentsReturnsNonEmptyFallback()
    {
        const string key = "Common.Test.MissingKeyWithArguments";

        var text = Lang.Text(key, "value");

        Assert.IsFalse(string.IsNullOrWhiteSpace(text));
        Assert.Contains(key, text);
    }
}