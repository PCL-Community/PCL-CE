using System.Threading.Tasks;

namespace PCL.CE.Core.Utils.Diff;

public interface IBinaryDiff
{
    public Task<byte[]> MakeAsync(byte[] originData, byte[] newData);
    public Task<byte[]> ApplyAsync(byte[] originData, byte[] diffData);
}