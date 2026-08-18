namespace Claims.Core.Common;

public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}