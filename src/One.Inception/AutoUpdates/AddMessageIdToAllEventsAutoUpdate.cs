using Microsoft.Extensions.Logging;
using One.Inception.EventStore;
using One.Inception.MessageProcessing;
using System.Collections.Generic;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace One.Inception.AutoUpdates
{
    internal class AddMessageIdToAllEventsAutoUpdate : IAutoUpdate
    {
        private readonly IEventStorePlayer player;
        private readonly IEventStore store;
        private readonly IInceptionContextAccessor inceptionContextAccessor;
        private readonly ISerializer serializer;
        private readonly ILogger<AddMessageIdToAllEventsAutoUpdate> logger;
        static readonly byte[] MessageIdMarker = Encoding.UTF8.GetBytes("\"messageId\":");


        public AddMessageIdToAllEventsAutoUpdate(IEventStorePlayer player, IEventStore store, IInceptionContextAccessor inceptionContextAccessor, ISerializer serializer, ILogger<AddMessageIdToAllEventsAutoUpdate> logger)
        {
            this.player = player;
            this.store = store;
            this.inceptionContextAccessor = inceptionContextAccessor;
            this.serializer = serializer;
            this.logger = logger;
        }

        public uint ExecutionSequence => 1;

        public string Name => "AddMessageIdToAllEventsAutoUpdate";

        public async Task<bool> ApplyAsync()
        {
            try
            {
                PlayerOperator @operator = new PlayerOperator()
                {
                    OnAggregateStreamLoadedAsync = async stream =>
                    {
                        foreach (var commit in stream.Commits)
                        {
                            List<Task> tasks = new List<Task>();

                            foreach (var current in commit.Events)
                            {
                                IMessage deserialized = serializer.DeserializeFromBytes<IMessage>(current.Data); // ? search for "messageId": dyrectly? in the bytes of the message

                                string theId = MessageIds.Get(deserialized);
                                if (string.IsNullOrEmpty(theId) == false)
                                {
                                    logger.LogInformation($"Event with id {Encoding.UTF8.GetString(current.AggregateRootId.Span)} is already migrated?!. MessageId: {theId}");
                                    continue;
                                }
                                else
                                {
                                    var id = deserialized.GetOrCreateMessageId();
                                    logger.LogInformation($"Create messageId {id} for Event with id {Encoding.UTF8.GetString(current.AggregateRootId.Span)}.");

                                    byte[] updated = serializer.SerializeToBytes(deserialized);

                                    if (updated.IndexOf(MessageIdMarker) < 0)
                                    {
                                        throw new Exception($"Something is wrong.. MessageId is missing in data - {id}, when it should be here supposedly... Serialized event data: {Encoding.UTF8.GetString(updated)}.");
                                    }

                                    var newEvent = new AggregateEventRaw(current.AggregateRootId, updated, current.Revision, current.Position, current.Timestamp);
                                    var append = store.AppendAsync(newEvent);
                                    tasks.Add(append);
                                }
                            }
                            await Task.WhenAll(tasks);
                        }
                    }
                };

                PlayerOptions options = options = new PlayerOptions();

                await player.EnumerateEventStore(@operator, options).ConfigureAwait(false);

                return true;

            }
            catch (System.Exception ex) when (True(() => logger.LogError(ex, "Error while executing AddMessageIdToAllEventsAutoUpdate for tenant {tenant}.", inceptionContextAccessor.Context.Tenant))) { return false; }
        }
    }
}
