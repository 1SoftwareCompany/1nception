using System;
using System.Threading;
using System.Threading.Tasks;

namespace One.Inception.Hosting;

public interface IRpcHost : IDisposable
{
    Task StartAsync();
    Task StopAsync(CancellationToken cancellationToken = default);
}
