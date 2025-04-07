using System;
using Microsoft.Extensions.DependencyInjection;

namespace One.Inception.MessageProcessing;

public interface IHandlerFactory
{
    IHandlerInstance Create(Type handlerType);
}

public class DefaultHandlerFactory : IHandlerFactory
{
    private readonly IInceptionContextAccessor contextAccessor;

    public DefaultHandlerFactory(IInceptionContextAccessor contextAccessor)
    {
        this.contextAccessor = contextAccessor;
    }

    public IHandlerInstance Create(Type handlerType)
    {
        object handlerInstance = contextAccessor.Context.ServiceProvider.GetRequiredService(handlerType);

        return new DefaultHandlerInstance(handlerInstance);
    }
}

public interface IHandlerInstance : IDisposable
{
    object Current { get; }
}

public class DefaultHandlerInstance : IHandlerInstance
{
    public DefaultHandlerInstance(object instance)
    {
        Current = instance;
    }

    public object Current { get; set; }

    public void Dispose()
    {
        var disposeMe = Current as IDisposable;
        if (disposeMe is null == false)
            disposeMe.Dispose();
    }
}
