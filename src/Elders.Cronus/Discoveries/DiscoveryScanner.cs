﻿using System;
using System.Linq;
using Elders.Cronus.Logging;

namespace Elders.Cronus.Discoveries
{
    public sealed class DiscoveryScanner : DiscoveryBase<DiscoveryScanner>
    {
        private readonly static ILog log = LogProvider.GetLogger(typeof(DiscoveryScanner));

        private readonly CronusServicesProvider cronusServicesProvider;

        public DiscoveryScanner(CronusServicesProvider cronusServicesProvider)
        {
            this.cronusServicesProvider = cronusServicesProvider;
        }

        protected override DiscoveryResult<DiscoveryScanner> DiscoverFromAssemblies(DiscoveryContext context)
        {
            var discoveries = context.Assemblies
                .SelectMany(asm => asm
                    .GetLoadableTypes()
                    .Where(type => type.IsAbstract == false && type.IsClass && typeof(IDiscovery<object>).IsAssignableFrom(type) && type != typeof(DiscoveryScanner)))
                .Select(dt => (IDiscovery<object>)FastActivator.CreateInstance(dt));

            foreach (var discovery in discoveries)
            {
                log.Info($"Discovered {discovery.Name}");

                var discoveryResult = discovery.Discover(context);

            }

            return new DiscoveryResult<DiscoveryScanner>();
        }
    }
}
