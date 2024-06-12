using GameCore.Models;
using GameCore.Protos;
using Grpc.Core;
using Orleans.Utilities;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IChatRoomGrain")]
public interface IChatRoomGrain : IGrainWithIntegerKey
{
    [Alias("SubscribeChatAsync")]
    ValueTask SubscribeChatAsync(Guid userId, IMapChatObserver mapCharacter, GrainCancellationToken grainCancellationToken);

    [Alias("UnsubscribeChatAsync")]
    ValueTask UnsubscribeChatAsync(IMapChatObserver mapCharacter);

    [Alias("ChatAsync")]
    ValueTask<ChatData> ChatAsync(Guid userId, string message, GrainCancellationToken grainCancellationToken);

    [Alias("LeaveAsync")]
    ValueTask LeaveAsync(Guid userId, GrainCancellationToken grainCancellationToken);
}

sealed class ChatRoomGrain(
    ILogger<ChatRoomGrain> logger,
    IGrainFactory grainFactory)
    : Grain, IChatRoomGrain
{
    readonly Dictionary<Guid, string> _characterNames = [];

    readonly Queue<ChatData> _chats = [];

    readonly ObserverManager<IMapChatObserver> _chatObservers = new(TimeSpan.FromMinutes(3), logger);

    public ValueTask LeaveAsync(Guid userId, GrainCancellationToken grainCancellationToken)
    {
        if (!_characterNames.TryGetValue(userId, out var name))
            throw new ArgumentException($"Character#{userId:N} not found", nameof(userId));

        var data = new ChatData(Sender: "<system>", Message: $"{name} leave.");

        _chatObservers.NotifyIgnoreWarning(o => o.Receive(data));

        return ValueTask.CompletedTask;
    }

    public async ValueTask SubscribeChatAsync(Guid userId, IMapChatObserver mapChat, GrainCancellationToken grainCancellationToken)
    {
        var user = grainFactory.GetGrain<IUserGrain>(userId);
        var userData = await user.GetDataAsync(grainCancellationToken);

        var exists = _chats.ToList();

        _chatObservers.Subscribe(mapChat, mapChat);

        foreach (var exist in exists)
            _chatObservers.NotifyIgnoreWarning(o => o.Receive(exist), o => o == mapChat);
    }

    public ValueTask UnsubscribeChatAsync(IMapChatObserver mapCharacter)
    {
        _chatObservers.Unsubscribe(mapCharacter);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ChatData> ChatAsync(Guid userId, string message, GrainCancellationToken grainCancellationToken)
    {
        if (!_characterNames.TryGetValue(userId, out var name))
            throw new ArgumentException($"Character#{userId:N} not found", nameof(userId));

        var newData = new ChatData(name, message);
        _chats.Enqueue(newData);
        while (_chats.Count > 240)
            _chats.Dequeue();

        _chatObservers.NotifyIgnoreWarning(o => o.Receive(newData));

        return ValueTask.FromResult(newData);
    }
}

[Alias("GameServer.Grains.IMapChatObserver")]
public interface IMapChatObserver : IGrainObserver
{
    [Alias("Receive")]
    ValueTask Receive(ChatData data);
}

sealed class MapChatObserver(
    ILogger<MapChatObserver> logger,
    IServerStreamWriter<SyncChatResponse> responseStream,
    CancellationToken cancellationToken = default) : IMapChatObserver
{
    public async ValueTask Receive(ChatData data)
    {
        var response = new SyncChatResponse
        {
            Sender = data.Sender,
            Message = data.Message,
        };
        await responseStream.WriteAsync(response, cancellationToken);
        logger.LogInformation("Write sync chat: {data}", data);
    }
}
