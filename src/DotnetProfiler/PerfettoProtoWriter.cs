using System;
using ProtoZeroSharp;

namespace DotnetProfiler;

internal static class PerfettoProtoWriter
{
    public static void StartTracePacket(this ref ProtoWriter writer, ulong timestamp, uint trustedPacketSequenceId, uint sequenceFlags = 0)
    {
        writer.StartSub(PerfettoProto.Trace.TracePacket);
        writer.AddVarInt(PerfettoProto.TracePacket.TimeStamp, timestamp);
        writer.AddVarInt(PerfettoProto.TracePacket.TrustedPacketSequenceId, trustedPacketSequenceId);
        if (sequenceFlags > 0)
            writer.AddVarInt(PerfettoProto.TracePacket.SequenceFlags, sequenceFlags);
    }

    public static void AddTrackDescriptor(this ref ProtoWriter writer, ulong uuid, ReadOnlySpan<byte> name)
    {
        writer.StartSub(PerfettoProto.TracePacket.TrackDescriptor);
        writer.AddVarInt(PerfettoProto.TrackDescriptor.Uuid, uuid);
        writer.AddBytes(PerfettoProto.TrackDescriptor.Name, name);
        writer.CloseSub();
    }

    public static void AddTrackEvent(this ref ProtoWriter writer, ulong trackUuid, ulong nameId, ReadOnlySpan<byte> name, PerfettoProto.TrackEvent.Types type)
    {
        writer.StartSub(PerfettoProto.TracePacket.TrackEvent);
        writer.AddVarInt(PerfettoProto.TrackEvent.TrackUuid, trackUuid);
        writer.AddVarInt(PerfettoProto.TrackEvent.Type, (ulong)type);
        if (nameId > 0)
            writer.AddVarInt(PerfettoProto.TrackEvent.NameIid, nameId);
        if (name.Length > 0)
            writer.AddBytes(PerfettoProto.TrackEvent.Name, name);
        writer.CloseSub();
    }

    public static void StartInternedData(this ref ProtoWriter writer)
    {
        writer.StartSub(PerfettoProto.TracePacket.InternedData);
    }

    public static void AddEventName(this ref ProtoWriter writer, ulong iid, ReadOnlySpan<byte> name)
    {
        writer.StartSub(PerfettoProto.InternedData.EventName);
        writer.AddVarInt(PerfettoProto.EventName.Iid, iid);
        writer.AddBytes(PerfettoProto.EventName.Name, name);
        writer.CloseSub();
    }
}
