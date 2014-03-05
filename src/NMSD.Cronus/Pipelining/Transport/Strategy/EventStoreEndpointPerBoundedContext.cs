﻿using System;
using System.Collections.Generic;
using System.Linq;
using NMSD.Cronus.DomainModelling;
using NMSD.Cronus.Transports;
using NMSD.Cronus.Transports.Conventions;

namespace NMSD.Cronus.Pipelining.Transport.Strategy
{
    public class EventStoreEndpointPerBoundedContext : IEndpointNameConvention
    {
        IPipelineNameConvention pipelineNameConvention;

        public EventStoreEndpointPerBoundedContext(IPipelineNameConvention pipelineNameConvention)
        {
            this.pipelineNameConvention = pipelineNameConvention;
        }

        public IEnumerable<EndpointDefinition> GetEndpointDefinitions(params Type[] eventTypes)
        {
            var eventType = eventTypes.First();
            var boundedContext = eventType.GetBoundedContext();
            var handlerQueueName = String.Format("{0}.EventStore", boundedContext.BoundedContextNamespace);
            var endpoint = new EndpointDefinition(handlerQueueName, new Dictionary<string, object> { { boundedContext.BoundedContextName, String.Empty } }, pipelineNameConvention.GetPipelineName(eventType));
            yield return endpoint;
        }

    }
}