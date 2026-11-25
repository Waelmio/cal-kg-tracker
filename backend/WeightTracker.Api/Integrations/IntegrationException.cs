namespace WeightTracker.Api.Integrations;

/// <summary>An error to show to the user. The message never contains a token or a secret.</summary>
public class IntegrationException(string message, int status = StatusCodes.Status502BadGateway, Exception? inner = null) : Exception(message, inner)
{
    public int Status { get; } = status;
}

/// <summary>The provider no longer accepts the stored token (or it can't be read): the user has to connect again.</summary>
public class NeedsReconnectException(string message) : IntegrationException(message, StatusCodes.Status409Conflict);
