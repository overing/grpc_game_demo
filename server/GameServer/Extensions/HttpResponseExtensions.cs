
static class HttpResponseExtensions
{
    /// <summary>
    /// patch for grpc duplex streaming 'resend the state to client at disconnect' bug
    /// ref: https://github.com/Cysharp/GrpcWebSocketBridge/blob/main/src/GrpcWebSocketBridge.AspNetCore/Internal/BridgeHttpResponseFeature.cs#L89
    /// </summary>
    public static void CancelStartOnAborted(this HttpResponse response)
    {
        response.OnStarting(callback, response.HttpContext);

        static Task callback(object state)
        {
            var token = ((HttpContext)state).RequestAborted;
            return token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
        }
    }
}
