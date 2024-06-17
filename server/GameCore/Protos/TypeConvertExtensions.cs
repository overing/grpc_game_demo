using System;
using GameCore.Models;
using Google.Protobuf.WellKnownTypes;

namespace GameCore.Protos;

public static class TypeConvertExtensions
{
    public static DateTimeOffset ToDateTimeOffset(this TimeOffset timeOffset)
        => timeOffset.Time.ToDateTimeOffset().ToOffset(timeOffset.Offset.ToTimeSpan());

    public static TimeOffset ToTimeOffset(this DateTimeOffset dateTimeOffset)
        => new() { Time = dateTimeOffset.ToTimestamp(), Offset = dateTimeOffset.Offset.ToDuration() };

    public static PointFloat ToPointFloat(this Vector2 point)
        => new(point.X, point.Y);

    public static Vector2 ToVector2(this PointFloat point)
        => new() { X = point.X, Y = point.Y };
}
