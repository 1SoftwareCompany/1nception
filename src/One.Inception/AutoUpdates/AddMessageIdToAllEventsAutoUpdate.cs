using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using One.Inception;
using One.Inception.EventStore;
using One.Inception.MessageProcessing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace One.Inception.AutoUpdates
{
    internal class AddMessageIdToAllEventsAutoUpdate : IAutoUpdate
    {
        private readonly IEventStorePlayer player;
        private readonly IEventStore store;
        private readonly IInceptionContextAccessor inceptionContextAccessor;
        private readonly ISerializer serializer;
        private readonly AddMessageIdPlayerPerTenantOptions options;
        private readonly ILogger<AddMessageIdToAllEventsAutoUpdate> logger;

        public AddMessageIdToAllEventsAutoUpdate(IEventStorePlayer player, IEventStore store, IInceptionContextAccessor inceptionContextAccessor, ISerializer serializer, IOptionsMonitor<AddMessageIdPlayerPerTenantOptions> monitor, ILogger<AddMessageIdToAllEventsAutoUpdate> logger)
        {
            this.player = player;
            this.store = store;
            this.inceptionContextAccessor = inceptionContextAccessor;
            this.serializer = serializer;
            this.options = monitor.CurrentValue;
            this.logger = logger;
        }

        public uint ExecutionSequence => 1;

        public string Name => "AddMessageIdToAllEventsAutoUpdate";

        public async Task<bool> ApplyAsync()
        {
            try
            {
                var optionForTenant = this.options.GetForTenant(inceptionContextAccessor.Context.Tenant);
                if (optionForTenant is null || optionForTenant.After.HasValue == false)
                {
                    throw new Exception($"Naughty... You did not configure AddMessageIdPlayerOptions for tenant {inceptionContextAccessor.Context.Tenant}. `After` date is required! Please do so, and then replay the event AutoUpdateTriggered in AutoUpdateProcessManager. (failed statuses are not handled still...");
                }

                PlayerOperator @operator = new PlayerOperator()
                {
                    OnAggregateStreamLoadedAsync = async stream =>
                    {
                        foreach (var commit in stream.Commits)
                        {
                            List<Task> tasks = new List<Task>();

                            foreach (var current in commit.Events)
                            {
                                IMessage deserialized = serializer.DeserializeFromBytes<IMessage>(current.Data);
                                if (MessageIds.Get(deserialized) is not null)
                                {
                                    return;
                                }
                                else
                                {
                                    deserialized.GetOrCreateMessageId();
                                    byte[] updated = serializer.SerializeToBytes(deserialized);

                                    var newEvent = new AggregateEventRaw(current.AggregateRootId, updated, current.Revision, current.Position, current.Timestamp);
                                    var append = store.AppendAsync(newEvent);
                                    tasks.Add(append);
                                }
                            }
                            await Task.WhenAll(tasks);
                        }
                    }
                };

                PlayerOptions options = options = new PlayerOptions()
                {
                    After = optionForTenant.After,
                    Before = optionForTenant.Before
                };

                await player.EnumerateEventStore(@operator, options).ConfigureAwait(false);

                return true;

            }
            catch (System.Exception ex) when (True(() => logger.LogError(ex, "Error while executing AddMessageIdToAllEventsAutoUpdate for tenant {tenant}.", inceptionContextAccessor.Context.Tenant))) { return false; }
        }
    }
}

public sealed class AddMessageIdPlayerPerTenantOptions
{
    public List<AddMessageIdPlayerOptions> AddMessageIdPlayerOptionsPerTenant { get; set; } = new List<AddMessageIdPlayerOptions>();

    public AddMessageIdPlayerOptions GetForTenant(string tenant) => AddMessageIdPlayerOptionsPerTenant.Where(x => x.Tenant.Equals(tenant, StringComparison.OrdinalIgnoreCase)).SingleOrDefault();
}

public sealed class AddMessageIdPlayerOptions
{
    public string Tenant { get; set; }
    public DateTimeOffset? After { get; set; }
    public DateTimeOffset? Before { get; set; }

}

public class AddMessageIdPlayerOptionsProvider : InceptionOptionsProviderBase<AddMessageIdPlayerPerTenantOptions>
{
    public const string SettingKey = "Inception:AddMessageIdPlayerOptions";

    public AddMessageIdPlayerOptionsProvider(IConfiguration configuration) : base(configuration) { }

    public override void Configure(AddMessageIdPlayerPerTenantOptions options)
    {
        configuration.GetSection(SettingKey).Bind(options.AddMessageIdPlayerOptionsPerTenant);
    }
}
