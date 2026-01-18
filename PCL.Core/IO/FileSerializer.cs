using System.IO;

namespace PCL.Core.IO;

public interface IFileSerializer<T>
{
    T Deserialize(Stream source);
    void Serialize(T input, Stream destination);
}