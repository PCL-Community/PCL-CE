using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Account.OAuth;

namespace PCL.Test.OAuth
{
    [TestClass]
    public class MsOAuth
    {

        [TestMethod]
        public async Task TestOAuthLogin()
        {
            return; // Set return when auto test
            var oauth = new MicrosoftCodeFlowOAuthSession("d783841c-a30a-4351-8d4c-bd9c03dc7978", "https://graph.microsoft.com/mail.read");
            oauth.StateChanged += (sender, state) =>
            {
                switch (state)
                {
                    case AuthStep.PendingUser:
                        if (oauth.AuthUrl == null) return;
                        break;
                }
            };
            await oauth.BeginAsync();
            await oauth.WaitForResultAsync(CancellationToken.None);
        }
    }
}
