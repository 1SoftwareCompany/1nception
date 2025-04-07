using System.Threading;

namespace One.Inception.MessageProcessing;

/// <summary>
/// Provides an implementation of <see cref="IInceptionContextAccessor" /> based on the current execution context.
/// </summary>
public class InceptionContextAccessor : IInceptionContextAccessor
{
    private static readonly AsyncLocal<ContextHolder> _contextCurrent = new AsyncLocal<ContextHolder>();

    /// <inheritdoc/>
    public InceptionContext Context
    {
        get
        {
            return _contextCurrent.Value?.Context;
        }
        set
        {
            var holder = _contextCurrent.Value;
            if (holder != null)
            {
                // Clear current HttpContext trapped in the AsyncLocals, as its done.
                holder.Context = null;
            }

            if (value != null)
            {
                // Use an object indirection to hold the HttpContext in the AsyncLocal,
                // so it can be cleared in all ExecutionContexts when its cleared.
                _contextCurrent.Value = new ContextHolder { Context = value };

            }
        }
    }

    private sealed class ContextHolder
    {
        public InceptionContext Context;
    }
}
