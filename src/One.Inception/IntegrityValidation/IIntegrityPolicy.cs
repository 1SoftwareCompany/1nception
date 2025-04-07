using System.Collections.Generic;

namespace One.Inception.IntegrityValidation;

public interface IIntegrityPolicy<T>
{
    IEnumerable<IntegrityRule<T>> Rules { get; }

    IntegrityResult<T> Apply(T candidate);
}
