using System;
using System.Threading;
using System.Threading.Tasks;

namespace One.Inception;

public interface IInceptionHost : IDisposable
{
    Task StartAsync();
    Task StopAsync(CancellationToken cancellationToken = default);
}
