namespace DotnetProfiler;

internal static class PerfettoProto
{
    internal static class Trace
    {
        internal static int TracePacket = 1;
    }

    internal static class TracePacket
    {
        internal static int TimeStamp = 8;
        internal static int TrackDescriptor = 60;
        internal static int TrackEvent = 11;
        internal static int TrustedPacketSequenceId = 10;
        internal static int InternedData = 12;
        internal static int SequenceFlags = 13;
    }

    internal static class InternedData
    {
        internal static int EventName = 2;
    }

    internal static class EventName
    {
        internal static int Iid = 1;
        internal static int Name = 2;
    }

    internal static class TrackDescriptor
    {
        internal static int Uuid = 1;
        internal static int Name = 2;
    }

    internal static class TrackEvent
    {
        internal static int NameIid = 10;
        internal static int Name = 23;
        internal static int Type = 9;
        internal static int TrackUuid = 11;

        internal enum Types
        {
            TYPE_UNSPECIFIED = 0,
            TYPE_SLICE_BEGIN = 1,
            TYPE_SLICE_END = 2,
            TYPE_INSTANT = 3,
            TYPE_COUNTER = 4,
        }
    }
}
