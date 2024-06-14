using GameCore.Models;
using GameCore.Protos;
using Grpc.Core;
using Orleans.Utilities;

namespace GameServer.Grains;

[Alias("GameServer.Grains.IChatRoomGrain")]
public interface IChatRoomGrain : IGrainWithIntegerKey
{
    [Alias("SubscribeAsync")]
    ValueTask SubscribeAsync(IMapChatObserver mapChat);

    [Alias("UnsubscribeAsync")]
    ValueTask UnsubscribeAsync(IMapChatObserver mapChat);

    [Alias("JoinAsync")]
    ValueTask JoinAsync(Guid userId, GrainCancellationToken grainCancellationToken);

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

    public ValueTask SubscribeAsync(IMapChatObserver mapChat)
    {
        logger.LogInformation(nameof(SubscribeAsync));
        _chatObservers.Subscribe(mapChat, mapChat);
        return ValueTask.CompletedTask;
    }

    public ValueTask UnsubscribeAsync(IMapChatObserver mapChat)
    {
        _chatObservers.Unsubscribe(mapChat);
        return ValueTask.CompletedTask;
    }

    public async ValueTask JoinAsync(Guid userId, GrainCancellationToken grainCancellationToken)
    {
        if (!_characterNames.ContainsKey(userId))
        {
            var user = grainFactory.GetGrain<IUserGrain>(userId);
            var userData = await user.GetDataAsync(grainCancellationToken);
            _characterNames[userId] = userData.Name;
        }

        foreach (var chatData in _chats)
            _chatObservers.NotifyIgnoreWarning(o => o.Receive(chatData));
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

    public async ValueTask LeaveAsync(Guid userId, GrainCancellationToken grainCancellationToken)
    {
        if (!_characterNames.TryGetValue(userId, out var name))
        {
            var user = grainFactory.GetGrain<IUserGrain>(userId);
            var userData = await user.GetDataAsync(grainCancellationToken);
            name = userData.Name;
        }
        else
            _characterNames.Remove(userId);

        var data = new ChatData(Sender: "<system>", Message: $"{name} leave.");
        _chatObservers.NotifyIgnoreWarning(o => o.Receive(data));
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
