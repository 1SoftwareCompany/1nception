﻿using One.Inception.Projections.Versioning;
using One.Inception.Tests.Projections;
using Machine.Specifications;

namespace One.Inception.Projections;

[Subject("ProjectionVersions")]
public class When_adding_a_building_version_to_non_versionable_projection
{
    Establish context = () =>
    {
        MessageInfo.GetContractId(typeof(NonVersionableProjection));

        version = new ProjectionVersion("NonVersionableProjection", ProjectionStatus.Live, 1, "hash");
        versions = new ProjectionVersions(version);

        var rebuildVersion = versions.GetNext(new MarkupInterfaceProjectionVersioningPolicy(), "hash");
        versions.Add(rebuildVersion);

        nextVersion = new ProjectionVersion("NonVersionableProjection", ProjectionStatus.Live, 1, "hash");
    };

    Because of = () => versions.Add(nextVersion);

    It should_not_have_live_version = () => versions.GetLive().ShouldEqual(version);

    It should_not_be__canceled__ = () => versions.IsCanceled(version).ShouldBeFalse();
    It should_not_be__outdated__ = () => versions.IsOutdated(version).ShouldBeFalse();

    static ProjectionVersion version;
    static ProjectionVersion nextVersion;
    static ProjectionVersions versions;
}
