using System.Collections.Generic;

namespace One.Inception.IntegrityValidation;

public interface IValidatorResult
{
    bool IsValid { get; }
    string ErrorType { get; }
    IEnumerable<string> Errors { get; }
}