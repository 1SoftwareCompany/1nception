using System.Collections.Generic;
using System.Threading.Tasks;

namespace One.Inception.DangerZone;

public interface IDangerZone
{
    Task WipeDataAsync(string tenant);
}
