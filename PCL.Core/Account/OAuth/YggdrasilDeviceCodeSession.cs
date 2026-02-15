using System;
using System.Threading.Tasks;

namespace PCL.Core.Account.OAuth;
public class YggdrasilDeviceCodeSession : LoginSession<YggdrasilDeviceCode>
{
    public override Task BeginAsync()
    {
        throw new NotImplementedException();
    }
}
