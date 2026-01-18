using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCL.Core.Account.OAuth;

namespace PCL.Core.Account.OAuth;
public class YggdrasilDeviceCodeSession : LoginSession<YggdrasilDeviceCode>
{
    public override Task BeginAsync()
    {
        throw new NotImplementedException();
    }
}
