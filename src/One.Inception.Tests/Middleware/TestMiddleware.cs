using One.Inception.Workflow;
using System.Threading.Tasks;

namespace One.Inception.Tests.Middleware;

public class TestMiddleware : Workflow<string>
{
    ExecutionToken token;

    public TestMiddleware(ExecutionToken token)
    {
        this.token = token;
    }

    protected override Task RunAsync(Execution<string> execution)
    {
        token.Notify();
        return Task.CompletedTask;
    }
}
