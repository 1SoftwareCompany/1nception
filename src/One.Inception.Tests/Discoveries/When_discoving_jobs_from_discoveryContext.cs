﻿using One.Inception.Cluster.Job;
using One.Inception.Cluster.Job.InMemory;
using Machine.Specifications;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace One.Inception.Discoveries;

[Subject("Discoveries")]
public class When_discoving_jobs_from_discoveryContext
{
    Establish context = () =>
    {
        IConfiguration configuration = new ConfigurationMock();

        discoveryContext = new DiscoveryContext(new List<Assembly> { typeof(When_discoving_jobs_from_discoveryContext).Assembly }, configuration);
    };

    Because of = () => result = new JobDiscovery().Discover(discoveryContext);

    It should_have_jobs_discovered = () => result.Models.Count().ShouldBeGreaterThan(0);

    It should_have_correct_job_discovered = () => result.Models.ShouldContain(x => x.ServiceType == typeof(TestJob) && x.ImplementationType == typeof(TestJob) && x.Lifetime.Equals(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Transient));

    It should_have_job_type_container = () => result.Models.ShouldContain(x => x.ServiceType == typeof(TypeContainer<IInceptionJob<object>>) && x.ImplementationInstance.GetType().Equals(typeof(TypeContainer<IInceptionJob<object>>)) && x.Lifetime.Equals(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton));

    static IDiscoveryResult<IInceptionJob<object>> result;
    static DiscoveryContext discoveryContext;
}

public class TestJobData : IJobData
{
    public bool IsCompleted { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public DateTimeOffset Timestamp { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
}

public class TestJob : InceptionJob<TestJobData>
{
    public override string Name { get; set; } = "Test";

    public TestJob() : base(null)
    {

    }

    protected override Task<JobExecutionStatus> RunJobAsync(IClusterOperations cluster, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
