using GameRepository.Repositories;

namespace GameServer.GrpcServices;

public sealed partial class GameService(
    ILogger<GameService> logger,
    ISessionRepository sessionRepository,
    IClusterClient clusterClient,
    TimeProvider timeProvider)
    : GameCore.Protos.GameService.GameServiceBase
{
    readonly ILogger<GameService> _logger = logger;
    readonly ISessionRepository _sessionRepository = sessionRepository;
    readonly IClusterClient _clusterClient = clusterClient;
    readonly TimeProvider _timeProvider = timeProvider;
}
