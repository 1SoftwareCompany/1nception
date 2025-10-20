using One.Inception.EventStore.Index;
using One.Inception.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace One.Inception;

[InceptionStartup(Bootstraps.EventStoreIndices)]
public class EventStoreIndicesStartup : IInceptionStartup /// TODO: make this <see cref="ITenantStartup"/>
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
        tenantsOptions.OnChange(async tenantOptions => await OptionsChangedBootstrapEventStoreIndexForTenantAsync(tenantOptions));
    }

    public async Task BootstrapAsync()
    {
        if (hostOptions.ApplicationServicesEnabled == false)
            return;

        List<Task> tasks = new List<Task>();
        foreach (var index in indexTypeContainer.Items)
        {
            foreach (var tenant in tenants.Tenants)
            {
                tasks.Add(InitializeIndesForTenantAsync(index, tenant));
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private Task InitializeIndesForTenantAsync(Type index, string tenant)
    {
        if (hostOptions.ApplicationServicesEnabled)
        {
            var id = new EventStoreIndexManagerId(index.GetContractId(), tenant);
            var command = new RegisterIndex(id);

            return publisher.PublishAsync(command);
        }

        return Task.CompletedTask;
    }

    private async Task OptionsChangedBootstrapEventStoreIndexForTenantAsync(TenantsOptions newOptions)
    {
        if (tenants.Tenants.SequenceEqual(newOptions.Tenants) == false) // Check for difference between tenants and newOptions
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("tenants options re-loaded with {@options}", newOptions);

            // Find the difference between the old and new tenants
            // and bootstrap the new tenants
            List<Task> tasks = new List<Task>();
            IEnumerable<string> newTenants = newOptions.Tenants.Except(tenants.Tenants);
            foreach (var index in indexTypeContainer.Items)
            {
                foreach (var tenant in newTenants)
                {
                    tasks.Add(InitializeIndesForTenantAsync(index, tenant));
                }
            }

            tenants = newOptions;

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }

    private void hostOptionsChanged(InceptionHostOptions newOptions)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("host options re-loaded with {@options}", newOptions);

        hostOptions = newOptions;
    }
}
