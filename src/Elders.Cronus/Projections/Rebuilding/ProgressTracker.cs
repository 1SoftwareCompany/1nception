﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Elders.Cronus.EventStore;
using Elders.Cronus.MessageProcessing;
using Microsoft.Extensions.Logging;

namespace Elders.Cronus.Projections.Rebuilding
{
    public class ProgressTracker
    {
        private Task notifier;
        private readonly string tenant;
        private readonly IMessageCounter messageCounter;
        private readonly IPublisher<ISystemSignal> signalPublisher;
        private readonly ProjectionVersionHelper projectionVersionHelper;
        private readonly ILogger<ProgressTracker> logger;

        public string ProjectionName { get; set; }
        public Dictionary<string, StrongBox<ulong>> EventTypeProcessed { get; set; }
        public ulong TotalEvents { get; set; }

        public ProgressTracker(IMessageCounter messageCounter, ICronusContextAccessor contextAccessor, IPublisher<ISystemSignal> signalPublisher, ProjectionVersionHelper projectionVersionHelper, ILogger<ProgressTracker> logger)
        {
            EventTypeProcessed = new Dictionary<string, StrongBox<ulong>>();
            tenant = contextAccessor.CronusContext.Tenant;
            this.messageCounter = messageCounter;
            this.signalPublisher = signalPublisher;
            this.projectionVersionHelper = projectionVersionHelper;
            this.logger = logger;
        }

        /// <summary>
        /// Use Initialize for initializing progress for specified projection 
        /// </summary>
        /// <param name="version">Projection version that should be initialized</param>
        public async Task InitializeAsync(ProjectionVersion version)
        {
            EventTypeProcessed = new Dictionary<string, StrongBox<ulong>>();
            TotalEvents = 0;

            ProjectionName = version.ProjectionName;
            IEnumerable<Type> projectionHandledEventTypes = projectionVersionHelper.GetInvolvedEventTypes(ProjectionName.GetTypeByContract());
            foreach (var eventType in projectionHandledEventTypes)
            {
                TotalEvents += (ulong)await messageCounter.GetCountAsync(eventType).ConfigureAwait(false);
                EventTypeProcessed.Add(eventType.GetContractId(), new StrongBox<ulong>(0));
            }
        }

        /// <summary>
        /// Finishes the action and sending incrementing progress signal
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public void TrackAndNotify(string executionId, CancellationToken cancellationToken = default)
        {
            Track(executionId);
            Notify(cancellationToken);
        }

        public RebuildProjectionProgress GetProgressSignal()
        {
            return new RebuildProjectionProgress(tenant, ProjectionName, CountTotalProcessedEvents(), TotalEvents);
        }

        public RebuildProjectionFinished GetProgressFinishedSignal()
        {
            notifier = null;
            return new RebuildProjectionFinished(tenant, ProjectionName);
        }

        public RebuildProjectionStarted GetProgressStartedSignal()
        {
            return new RebuildProjectionStarted(tenant, ProjectionName);
        }

        public ulong GetTotalProcessedCount()
        {
            ulong totalProcessed = 0;
            foreach (var typeProcessed in EventTypeProcessed)
            {
                totalProcessed += typeProcessed.Value.Value;
            }
            return totalProcessed;
        }

        public ulong GetTotalProcessedCount(string executionId)
        {
            EventTypeProcessed.TryGetValue(executionId, out var totalProcessed);
            return totalProcessed.Value;
        }

        public ulong CountTotalProcessedEvents()
        {
            ulong totalProcessed = 0;
            foreach (var typeProcessed in EventTypeProcessed)
            {
                totalProcessed += typeProcessed.Value.Value;
            }
            return totalProcessed;
        }

        private void Track(string executionId)
        {
            try
            {
                if (EventTypeProcessed.ContainsKey(executionId))
                    Interlocked.Increment(ref EventTypeProcessed[executionId].Value);

            }
            catch (Exception ex) when (logger.ErrorException(ex, () => $"Error when saving aggregate commit for projection {ProjectionName}")) { }
        }

        object gate = new object();

        private void Notify(CancellationToken cancellationToken = default)
        {
            if (notifier is null)
            {
                lock (gate)
                {
                    if (notifier is null)
                        notifier = Task.Run(async () =>
                        {
                            try
                            {
                                while (true)
                                {
                                    RebuildProjectionProgress progressSignalche = GetProgressSignal();
                                    signalPublisher.Publish(progressSignalche);

                                    await Task.Delay(1000, cancellationToken);
                                }
                            }
                            catch (Exception)
                            {
                                notifier = null;
                            }
                        }, cancellationToken);
                }

            }
        }
    }
}
