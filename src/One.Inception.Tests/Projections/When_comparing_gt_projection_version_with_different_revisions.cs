﻿using One.Inception.Projections;
using Machine.Specifications;

namespace One.Inception.Tests.Projections;

[Subject("Projections")]
public class When_comparing_gt_projection_version_with_different_revisions
{
    Establish context = () =>
    {
        lower = new ProjectionVersion("compare_lt", ProjectionStatus.Live, 1, "compare_gt_hash");
        higher = new ProjectionVersion("compare_lt", ProjectionStatus.Live, 2, "compare_gt_hash");
    };

    Because of = () => result = lower < higher;

    It should_be_able_to_compare_with_gt = () => result.ShouldBeTrue();

    static bool result;
    static ProjectionVersion lower;
    static ProjectionVersion higher;
}
