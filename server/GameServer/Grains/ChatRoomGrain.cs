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
    ValueTask JoinAsync(UserData userData, GrainCancellationToken grainCancellationToken);

    [Alias("ChangeNameAsync")]
    ValueTask ChangeNameAsync(Guid userId, string newName, GrainCancellationToken grainCancellationToken);

    [Alias("ChatAsync")]
    ValueTask<ChatData> ChatAsync(Guid userId, string message, GrainCancellationToken grainCancellationToken);

    [Alias("LeaveAsync")]
    ValueTask LeaveAsync(Guid userId, GrainCancellationToken grainCancellationToken);
}

sealed class ChatRoomGrain(
    ILogger<ChatRoomGrain> logger,
    ILogger<ObserverManager<IMapChatObserver>> observerManagerLogger)
    : Grain, IChatRoomGrain
{
    readonly Dictionary<Guid, string> _characterNames = [];

    readonly Queue<ChatData> _chats = [];

    readonly ObserverManager<IMapChatObserver> _chatObservers = new(TimeSpan.FromMinutes(3), observerManagerLogger);

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

    public ValueTask JoinAsync(UserData userData, GrainCancellationToken grainCancellationToken)
    {
        if (!_characterNames.ContainsKey(userData.ID))
            _characterNames[userData.ID] = userData.Name;

        foreach (var chatData in _chats)
            _chatObservers.NotifyIgnoreWarning(o => o.Receive(chatData));

        return ValueTask.CompletedTask;
    }

    public ValueTask ChangeNameAsync(Guid userId, string newName, GrainCancellationToken grainCancellationToken)
    {
        if (!_characterNames.TryGetValue(userId, out var originalName))
            throw new ArgumentException($"Character#{userId:N} not found", nameof(userId));

        _characterNames[userId] = newName;

        originalName = CharacterData.CutNameForDisplay(originalName);
        newName = CharacterData.CutNameForDisplay(newName);
        var data = new ChatData("<system>", $"Player '{originalName}' change name to '{newName}'");

        _chats.Enqueue(data);

        _chatObservers.NotifyIgnoreWarning(o => o.Receive(data));

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

    public ValueTask LeaveAsync(Guid userId, GrainCancellationToken grainCancellationToken)
    {
        if (!_characterNames.TryGetValue(userId, out var name))
            throw new ArgumentException($"Character#{userId:N} not found", nameof(userId));

        _characterNames.Remove(userId);

        var data = new ChatData(Sender: "<system>", Message: $"{name} leave.");
        _chatObservers.NotifyIgnoreWarning(o => o.Receive(data));

        return ValueTask.CompletedTask;
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
        if (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Write sync chat canceled");
            return;
        }
        try
        {
            await responseStream.WriteAsync(response, cancellationToken);
            logger.LogInformation("Write sync chat: {data}", data);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Write sync chat canceled");
        }
    }
}
