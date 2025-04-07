using System;
using System.Threading.Tasks;

namespace One.Inception;

public interface IInceptionHost : IDisposable
{
    Task StartAsync();
    Task StopAsync();
}
