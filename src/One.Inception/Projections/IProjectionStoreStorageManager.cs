using System.Threading.Tasks;

namespace One.Inception.Projections;

public interface IProjectionStoreStorageManager
{
    Task CreateProjectionsStorageAsync(string location);
}
