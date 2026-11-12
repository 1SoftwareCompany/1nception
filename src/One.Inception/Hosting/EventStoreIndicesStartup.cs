using One.Inception.EventStore.Index;
using One.Inception.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace One.Inception;

[InceptionStartup(Bootstraps.EventStoreIndices, ClusterWide = true)]
public class EventStoreIndicesStartup : IInceptionStartup
{
    private TenantsOptions tenants;
    private InceptionHostOptions hostOptions;
    private readonly IPublisher<ICommand> publisher;
    private readonly TypeContainer<IEventStoreIndex> indexTypeContainer;
    private readonly ILogger<EventStoreIndicesStartup> logger;

    public EventStoreIndicesStartup(TypeContainer<IEventStoreIndex> indexTypeContainer, IOptionsMonitor<InceptionHostOptions> hostOptions, IOptionsMonitor<TenantsOptions> tenantsOptions, IPublisher<ICommand> publisher, ILogger<EventStoreIndicesStartup> logger)
    {
        this.tenants = tenantsOptions.CurrentValue;
        this.hostOptions = hostOptions.CurrentValue;
        this.publisher = publisher;
        this.logger = logger;
        this.indexTypeContainer = indexTypeContainer;

        hostOptions.OnChange(hostOptionsChanged);
        tenantsOptions.OnChange(TenantOptionsChanges);
    }

    public async Task BootstrapAsync()
    {
        await BootstrapInternalAsync(tenants.Tenants).ConfigureAwait(false);
    }

    public async Task BootstrapAsync(IEnumerable<string> tenants)
    {
        await BootstrapInternalAsync(tenants).ConfigureAwait(false);
    }

    public async Task BootstrapInternalAsync(IEnumerable<string> tenants)
    {
        if (hostOptions.ApplicationServicesEnabled == false)
            return;

        List<Task> tasks = new List<Task>();
        foreach (var index in indexTypeContainer.Items)
        {
            foreach (var tenant in tenants)
            {
                tasks.Add(InitializeIndicesForTenantAsync(index, tenant));
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task InitializeIndicesForTenantAsync(Type index, string tenant)
    {
        if (hostOptions.ApplicationServicesEnabled)
        {
            var id = new EventStoreIndexManagerId(index.GetContractId(), tenant);
            var command = new RegisterIndex(id);

            if (await publisher.PublishAsync(command).ConfigureAwait(false) == false)
                logger.LogError("Failed to publish RegisterIndex for index {index} of tenant {tenant}. The index is not registered", index.GetContractId(), tenant);
        }
    }

    private void TenantOptionsChanges(TenantsOptions newOptions)
    {
        if (tenants.Tenants.SequenceEqual(newOptions.Tenants) == false) // Check for difference between tenants and newOptions
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("tenant options re-loaded with {@options}", newOptions);

            tenants = newOptions;
        }
    }

    private void hostOptionsChanged(InceptionHostOptions newOptions)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("host options re-loaded with {@options}", newOptions);

        hostOptions = newOptions;
    }
}
