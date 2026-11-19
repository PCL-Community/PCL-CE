using System.Linq;
using FluentValidation.Results;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Validate;

namespace PCL.Core.Test.Validate;

[TestClass]
public sealed class ValidationMessageTest
{
    [TestMethod]
    public void InvalidInputsReturnNonEmptyMessages()
    {
        AssertNonEmpty(new BlacklistValidator(["test"]).Validate("test"));
        AssertNonEmpty(new FileNameValidator().Validate("?foo."));
        AssertNonEmpty(new FolderNameValidator().Validate("LPT4"));
        AssertNonEmpty(new FolderPathValidator().Validate(@"C:\CON\AUX"));
    }

    private static void AssertNonEmpty(ValidationResult result)
    {
        Assert.IsFalse(result.IsValid);
        Assert.IsNotEmpty(result.Errors);
        Assert.IsTrue(
            result.Errors.All(error => !string.IsNullOrWhiteSpace(error.ErrorMessage)),
            "校验失败时必须提供非空错误消息");
    }
}