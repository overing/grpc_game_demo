using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using Grpc.Core;

public static class IGameApiClientDuplexStreamExtensions
{
    public static async UniTask ContinueAsync<TRequest, TResponse>(
        this AsyncDuplexStreamingCall<TRequest, TResponse> duplex,
        Func<CancellationToken, IAsyncEnumerable<TRequest>> requestGenerator,
        Action<TResponse> responseHandler,
        CancellationToken cancellationToken = default)
    {
        _ = ResendRequestAsync(cancellationToken);
        await foreach (var response in duplex.ResponseStream.ReadAllAsync(cancellationToken))
            responseHandler(response);

        async UniTask ResendRequestAsync(CancellationToken cancellationToken)
        {
            await foreach (var request in requestGenerator(cancellationToken))
                await duplex.RequestStream.WriteAsync(request);
        }
    }

    public static UniTask ContinueAsync<TRequest, TResponse>(
        this AsyncDuplexStreamingCall<TRequest, TResponse> duplex,
        TRequest requestForResend,
        TimeSpan interval,
        Action<TResponse> responseHandler,
        CancellationToken cancellationToken = default)
    {
        return ContinueAsync(duplex, GenerateRequest, responseHandler, cancellationToken);
        async IAsyncEnumerable<TRequest> GenerateRequest([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                yield return requestForResend;
                await UniTask.Delay(interval, ignoreTimeScale: true, delayTiming: PlayerLoopTiming.FixedUpdate, cancellationToken: cancellationToken);
            }
        }
    }
}
