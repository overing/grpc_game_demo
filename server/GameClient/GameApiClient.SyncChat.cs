using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GameCore.Models;
using GameCore.Protos;
using Grpc.Core;

namespace GameClient;

sealed partial class GameApiClient
{
    public async IAsyncEnumerable<ChatData> SyncChatAsync(Guid userId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new SyncChatRequest { ID = userId.ToString("N") };
        var interval = TimeSpan.FromSeconds(10);
        using var duplex = _client.SyncChat(cancellationToken: cancellationToken);
        _ = ResendAsync(cancellationToken);
        await foreach (var response in duplex.ResponseStream.ReadAllAsync(cancellationToken))
        {
            var data = new ChatData(
                Sender: response.Sender,
                Message: response.Message);
            yield return data;
        }
        async ValueTask ResendAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await duplex.RequestStream.WriteAsync(request);
                await Task.Delay(interval, cancellationToken);
            }
        }
    }
}
