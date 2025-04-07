using System.Threading;
using System.Threading.Tasks;

namespace One.Inception.Hosting.Heartbeat;

public interface IHeartbeat
{
    Task StartBeatingAsync(CancellationToken stoppingToken);
}
