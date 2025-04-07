using One.Inception.EventStore.Index;
using One.Inception.Multitenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;

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
        tenantsOptions.OnChange(OptionsChangedBootstrapEventStoreIndexForTenant);
    }

    public void Bootstrap()
    {
        if (hostOptions.ApplicationServicesEnabled == false)
            return;

        foreach (var index in indexTypeContainer.Items)
        {
            foreach (var tenant in tenants.Tenants)
            {
                InitializeIndesForTenant(index, tenant);
            }
        }
    }

    private void InitializeIndesForTenant(Type index, string tenant)
    {
        if (hostOptions.ApplicationServicesEnabled == false)
            return;

        var id = new EventStoreIndexManagerId(index.GetContractId(), tenant);
        var command = new RegisterIndex(id);
        publisher.Publish(command);
    }

    private void OptionsChangedBootstrapEventStoreIndexForTenant(TenantsOptions newOptions)
    {
        if (tenants.Tenants.SequenceEqual(newOptions.Tenants) == false) // Check for difference between tenants and newOptions
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("tenants options re-loaded with {@options}", newOptions);

            // Find the difference between the old and new tenants
            // and bootstrap the new tenants
            var newTenants = newOptions.Tenants.Except(tenants.Tenants);
            foreach (var index in indexTypeContainer.Items)
            {
                foreach (var tenant in newTenants)
                {
                    InitializeIndesForTenant(index, tenant);
                }
            }

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
