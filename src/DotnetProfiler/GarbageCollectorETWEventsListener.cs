using System;
using System.Buffers.Text;
using System.Diagnostics.Tracing;
using System.Text;

namespace DotnetProfiler;

internal sealed class GarbageCollectorETWEventsListener : EventListener
{
    // from https://docs.microsoft.com/en-us/dotnet/framework/performance/garbage-collection-etw-events
    private const int GC_KEYWORD =                 0x0000001;
    private bool first = true;

    private const int GC_TRACK = (int)ushort.MaxValue + 1;
    private const int ALLOC_TRACK = (int)ushort.MaxValue + 2;

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name.Equals("Microsoft-Windows-DotNETRuntime"))
        {
            EnableEvents(
                eventSource,
                EventLevel.Verbose,
                (EventKeywords) (GC_KEYWORD)
            );
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (first)
        {
            Profiler.RegisterCustomTrack(GC_TRACK, "GC"u8);
            Profiler.RegisterCustomTrack(ALLOC_TRACK, "Allocations"u8);
            first = false;
        }
        if (eventData.EventName.StartsWith("GCStart_V"))
        {
            Profiler.Start("GC"u8, GC_TRACK);
        }
        else if (eventData.EventName.StartsWith("GCEnd_V"))
        {
            Profiler.Stop(GC_TRACK);
        }
        else if (eventData.EventName.StartsWith("GCAllocationTick_V"))
        {
            int allocIndex = eventData.PayloadNames.IndexOf("AllocationAmount64");
            int typeNameIndex = eventData.PayloadNames.IndexOf("TypeName");
            if (allocIndex == -1 || typeNameIndex == -1)
            {
                Profiler.InstantEvent("Unk alloc"u8, ALLOC_TRACK);
            }
            else
            {
                string? name = (string)eventData.Payload[typeNameIndex];
                ulong alloc = (ulong)eventData.Payload[allocIndex];
                Span<byte> allocBytes = stackalloc byte[64 + name.Length * 4];
                int written = 0;
                Utf8Formatter.TryFormat(alloc, allocBytes.Slice(written), out var bytes);
                written += bytes;
                allocBytes[written++] = (byte)' ';
                allocBytes[written++] = (byte)'B';
                allocBytes[written++] = (byte)' ';
                allocBytes[written++] = (byte)'(';
#if NETSTANDARD2_0
                unsafe
                {
                    fixed (char* namePtr = name)
                    fixed (byte* allocBytesPtr = allocBytes)
                    {
                        written += Encoding.UTF8.GetBytes(namePtr, name.Length, allocBytesPtr + written, allocBytes.Length - written);
                    }
                }
#else
                written += Encoding.UTF8.GetBytes(name, allocBytes.Slice(written));
#endif
                allocBytes[written++] = (byte)')';
                Profiler.InstantEvent(allocBytes.Slice(0, written), ALLOC_TRACK);
            }
        }
    }
}
