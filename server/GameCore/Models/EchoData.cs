using System;

namespace GameCore.Models;

public sealed record class EchoData(
    TimeSpan ClientToGateway,
    TimeSpan GatewayToSilo,
    TimeSpan SiloToGateway,
    DateTimeOffset SiloTime);