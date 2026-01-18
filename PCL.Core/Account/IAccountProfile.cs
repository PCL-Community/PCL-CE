using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace PCL.Core.Account;
public interface IAccountProfile
{
    public Task<JsonObject> ToJson();
    public Task<IAccountProfile> FromJson(JsonObject obj);
}
