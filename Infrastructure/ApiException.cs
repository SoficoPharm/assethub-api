namespace AssetHub.Api.Infrastructure;

/// <summary>
/// An error whose message is safe to show to the client.
/// </summary>
public class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
