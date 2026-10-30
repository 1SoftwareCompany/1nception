# Handler cancellation and graceful shutdown (v14)

This is a breaking handler-contract change. Rebuild applications and upgrade
`1nception.Domain`, `1nception`, `1nception.Transport.RabbitMQ`, and, when used,
`1nception.AspNet` and `1nception.Api` together. The local development package
references use `14.0.0-preview.1`; these packages have not been published.
Release the domain package first, then core, then its adapters.

## Implementing handlers

Add a final `CancellationToken cancellationToken` parameter to every
`HandleAsync` implementation: commands, events, public events, signals,
process-manager timeouts, node broadcasts, aggregate-commit handlers, and RPC
request handlers. Update direct callers to pass a token explicitly.

```csharp
public async Task HandleAsync(RefreshCustomers signal, CancellationToken cancellationToken)
{
    foreach (var id in signal.CustomerIds)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await customerClient.RefreshAsync(id, cancellationToken).ConfigureAwait(false);
    }
}
```

Inception owns the cancellation sources. A handler receives a token, so it
cannot cancel other handlers. Consumer sources are recreated on restart and
linked to `IHostApplicationLifetime.ApplicationStopping` when available.
Explicit consumer/host stop also requests cancellation.

Create your own linked source only when an operation needs independent
cancellation or a timeout:

```csharp
using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
operation.CancelAfter(TimeSpan.FromSeconds(30));
await client.RefreshAsync(id, operation.Token).ConfigureAwait(false);
```

A local timeout does not cancel the consumer. A linked source cannot undo parent
cancellation or extend the container's grace period.

## Shutdown and message settlement

`IInceptionHost.StopAsync(CancellationToken)` and consumer/RPC stop methods accept
a **shutdown deadline**, not the handler's stopping token. Active handler tokens
are cancelled promptly; the deadline bounds how long shutdown waits.

`InceptionHostOptions.ShutdownTimeout` is the single shutdown timeout setting,
defaulting to 15 seconds. Configure it under `Inception:ShutdownTimeout`:

```json
{ "Inception": { "ShutdownTimeout": "00:00:30" } }
```

The host creates one linked deadline using this setting; an earlier caller/host
cancellation takes precedence. Handler cancellation is requested immediately.
If the deadline expires, Inception logs a warning and leaves unfinished
deliveries unacknowledged.

Consumer and RPC hosts use the supplied token unchanged. Only when no cancellable
token is supplied do they create a fallback deadline from the same options value.
Failed-start cleanup also uses that value. Low-level `AsyncConsumerBase` instances
never create their own timeout: direct callers must supply the desired deadline.
A cancellable token without a deadline can therefore wait indefinitely.

Channel cleanup is given at most one additional second after draining or deadline
expiry. Pass the hosting deadline from your worker's `StopAsync`:

```csharp
public override async Task StopAsync(CancellationToken cancellationToken)
{
    await inceptionHost.StopAsync(cancellationToken).ConfigureAwait(false);
    await base.StopAsync(cancellationToken).ConfigureAwait(false);
}
```

RabbitMQ stops intake and Inception refuses buffered deliveries after stopping.
A handler that successfully returns is acknowledged, even if cancellation was
requested meanwhile. A shutdown cancellation bypasses ordinary workflow error
wrapping, retries, and exception swallowing; its delivery is left unacknowledged
and returns to RabbitMQ when its channel closes. The ordinary-error and
deserialization-error acknowledgement policies are unchanged.

A shared delivery may have several subscribers. If one cancels, successful
subscribers may run again on redelivery. This remains at-least-once processing,
not per-subscriber checkpointing or exactly-once execution. Transient/auto-delete
queues retain their existing broker lifetime semantics.

RPC shutdown cancellation leaves the request unacknowledged and sends no
success/error response. A request that completes during shutdown can still
publish its response and be acknowledged. Cancelling a client's local wait does
not implement remote request cancellation.

## Handling cancellation correctly

- Pass the token into HTTP calls, cancellable delays, and other interruptible
  operations. Check it between batches in CPU-bound work.
- For resumable work, save progress and rethrow shutdown cancellation so the
  message is redelivered. Returning normally means the message is complete.
- If cleanup must continue after cancellation, give it a separate short-lived
  source. The original token is already cancelled; an independent source still
  cannot keep the process alive past Docker/Kubernetes termination.
- Keep token callbacks fast. Do asynchronous cleanup inside the handler's
  catch/finally, not an async-void cancellation callback.
- A handler ignoring cancellation can outlive the wait and overlap a redelivery
  on another instance. Its scope/source are not deliberately disposed underneath
  the active handler by the consumer. The host may still dispose its container
  or terminate the process, so this is not a supported way to continue work.
- The RabbitMQ client may keep waiting internally for an uncooperative callback
  after the channel-close frame has been sent. This can delay other operations
  sharing that connection until the callback returns.
- Cancellation is not rollback. No new cancellation points were introduced
  between aggregate persistence and event publication. Storage/publisher
  operations without cancellation parameters remain non-interruptible.
- Projection restoration receives the handling token through the scoped
  `InceptionContext`. ASP.NET contexts use `HttpContext.RequestAborted`.
  Background/custom callers can supply a token to
  `DefaultContextFactory.Create(target, provider, token)` or
  `ProjectionStream.RestoreFromHistoryAsync(projection, token)`.
- Existing long-running cluster jobs still have their own cancellation and
  error policies. The built-in job-starting handlers now pass their token and
  do not finalize a request after their handling token was cancelled.

Neither graceful shutdown nor cancellation makes persist-and-publish atomic.
A durable outbox and application-specific idempotency are separate concerns.
