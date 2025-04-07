using System.Threading.Tasks;

namespace One.Inception;

public interface IConsumer<out T> where T : IMessageHandler
{
    Task StartAsync();

    Task StopAsync();
}
