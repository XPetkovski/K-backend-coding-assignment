namespace Claims.Core.Common;

// Property, when set, names the offending field so the API can report business-rule failures in the
// same field-keyed shape as validation failures instead of prose a client has to string-match.
public class DomainException : Exception
{
    public DomainException(string message, string? property = null) : base(message)
    {
        Property = property;
    }

    public string? Property { get; }
}