using System.IO;

namespace BigFilesArchiver
{
    /// <summary>
    /// 
    /// Interface for an Output buffer, if you want unification
    /// (there is no real need, since a simple stream is enough)
    /// 
    /// </summary>
    interface IOutBuffer
    {
        MemoryStream Out { get; set; }
        long BytesToWrite { get => Out.Length; }
    }
}
