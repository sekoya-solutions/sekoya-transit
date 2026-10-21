#nullable enable
using System.Threading.Tasks;

namespace TransportTycoon.Save
{
    /// <summary>
    /// Synchronizes Unity's virtual filesystem with its durable backing store.
    /// </summary>
    public interface IFileSystemSynchronizer
    {
        Task SynchronizeAsync();
    }
}
