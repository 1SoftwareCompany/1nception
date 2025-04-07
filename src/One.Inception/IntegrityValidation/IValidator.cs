using System;

namespace One.Inception.IntegrityValidation;

public interface IValidator<T> : IComparable<IValidator<T>>
{
    IValidatorResult Validate(T candidate);

    uint PriorityLevel { get; }
}
