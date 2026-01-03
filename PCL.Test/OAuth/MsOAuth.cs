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
            var oauth = new MicrosoftCodeFlowOAuthSession("", "https://graph.microsoft.com/mail.read");
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
