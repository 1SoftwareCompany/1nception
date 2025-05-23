﻿using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace One.Inception.Hosting.Heartbeat;

[DataContract(Namespace = "Inception", Name = "c80739a6-b5dc-483e-8c11-06a85542416e")]
public sealed class HeartbeatSignal : ISignal // Consider using ISystemSignal. You need to check if system signals can be published to the public RMQ
{
    HeartbeatSignal()
    {
        Tenants = new List<string>();
    }

    public HeartbeatSignal(string boundedContext, List<string> tenants)
    {
        BoundedContext = boundedContext;
        Tenants = tenants;
        Timestamp = DateTimeOffset.Now;
        Tenant = "Inception";
        MachineName = Environment.MachineName;
        EnvironmentConfig = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    }

    [DataMember(Order = 0)]
    public string Tenant { get; private set; }

    [DataMember(Order = 1)]
    public string BoundedContext { get; private set; }

    [DataMember(Order = 2)]
    public List<string> Tenants { get; private set; }

    [DataMember(Order = 3)]
    public DateTimeOffset Timestamp { get; private set; }

    [DataMember(Order = 4)]
    public string MachineName { get; private set; }

    [DataMember(Order = 5)]
    public string EnvironmentConfig { get; private set; }
}

