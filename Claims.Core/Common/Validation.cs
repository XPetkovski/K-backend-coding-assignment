namespace Claims.Core.Common;

public interface IValidator<in T>
{
    ValidationResult Validate(T instance);
}

public sealed record ValidationError(string Property, string Message);

public sealed class ValidationResult
{
    private readonly List<ValidationError> _errors = [];

    public IReadOnlyList<ValidationError> Errors => _errors;

    public bool IsValid => _errors.Count == 0;

    public void Add(string property, string message) => _errors.Add(new ValidationError(property, message));

    public void AddIf(bool invalid, string property, string message)
    {
        if (invalid)
        {
            Add(property, message);
        }
    }

    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new ValidationException(_errors);
        }
    }
}

public class ValidationException : Exception
{
    public ValidationException(IReadOnlyList<ValidationError> errors)
        : base("One or more validation rules were violated.")
    {
        Errors = errors;
    }

    public IReadOnlyList<ValidationError> Errors { get; }
}