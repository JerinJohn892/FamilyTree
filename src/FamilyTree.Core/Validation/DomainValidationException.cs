using FamilyTree.Core.DTOs;

namespace FamilyTree.Core.Validation;

public sealed class DomainValidationException : Exception
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public DomainValidationException(params ValidationError[] errors)
        : base(string.Join(Environment.NewLine, errors.Select(e => e.Message))) => Errors = errors;

    public static DomainValidationException For(string code, string field, string message) =>
        new(new ValidationError(code, field, message));
}
