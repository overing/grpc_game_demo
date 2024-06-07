using System;
using Google.Protobuf.WellKnownTypes;

namespace GameCore.Protos;

public static class TypeConvertExtensions
{
    public static DateTimeOffset ToDateTimeOffset(this TimeOffset timeOffset)
        => timeOffset.Time.ToDateTimeOffset().ToOffset(timeOffset.Offset.ToTimeSpan());

    public static TimeOffset ToTimeOffset(this DateTimeOffset dateTimeOffset)
        => new() { Time = dateTimeOffset.ToTimestamp(), Offset = dateTimeOffset.Offset.ToDuration() };
}
