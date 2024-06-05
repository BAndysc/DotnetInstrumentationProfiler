using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ProtoZeroSharp;

namespace DotnetProfiler;

internal unsafe struct FastProfilerWriter
{
    private readonly ChunkedArray* memory;
    private bool ownsMemory;
    private static readonly int logEntrySizeOf = sizeof(LogEntry);

    public FastProfilerWriter() : this((ChunkedArray*)Marshal.AllocHGlobal(sizeof(ChunkedArray)))
    {
        ownsMemory = true;
        *memory = new ChunkedArray();
    }

    private FastProfilerWriter(ChunkedArray* memory)
    {
        this.memory = memory;
        ownsMemory = false;
    }

    private void AddLog(ulong timestamp, int trackId, ulong nameId, LogFlags flags)
    {
        var span = memory->ReserveContiguousSpan(logEntrySizeOf);
        fixed (byte* ptr = span)
        {
            ref LogEntry log = ref Unsafe.AsRef<LogEntry>(ptr);
            log.timestamp = timestamp;
            log.flags = flags;
            log.trackId = trackId;
            log.nameIdOrNameLength = nameId;
        }

        memory->MoveForward(span.Length);
    }

    private void AddLog(ulong timestamp, int trackId, LogFlags flags, ReadOnlySpan<byte> nameUtf8)
    {
        var span = memory->ReserveContiguousSpan(logEntrySizeOf + nameUtf8.Length);
        fixed (byte* ptr = span)
        {
            ref LogEntry log = ref Unsafe.AsRef<LogEntry>(ptr);
            log.timestamp = timestamp;
            log.flags = flags;
            log.trackId = trackId;
            log.nameIdOrNameLength = (ulong)nameUtf8.Length;
            fixed (byte* namePtr = nameUtf8)
                Unsafe.CopyBlockUnaligned(ptr + logEntrySizeOf, namePtr, (uint)nameUtf8.Length);
        }
        memory->MoveForward(span.Length);
    }

    public void LogStop(ulong timestamp, int trackId)
    {
        AddLog(timestamp, trackId, 0, LogFlags.End);
    }

    public void LogStart(ulong timestamp, int trackId, ulong nameId)
    {
        AddLog(timestamp, trackId, nameId, LogFlags.Begin);
    }

    public void LogStart(ulong timestamp, int trackId, ReadOnlySpan<byte> nameUtf8)
    {
        AddLog(timestamp, trackId, LogFlags.Begin | LogFlags.HasInlineName, nameUtf8);
    }

    public void LogInstantEvent(ulong timestamp, int trackId, ReadOnlySpan<byte> nameUtf8)
    {
        AddLog(timestamp, trackId, LogFlags.InstantEvent | LogFlags.HasInlineName, nameUtf8);
    }

    public void AddTrackDescription(ulong timestamp, int trackId, ReadOnlySpan<byte> nameUtf8)
    {
        AddLog(timestamp, trackId, LogFlags.TrackDescription | LogFlags.HasInlineName, nameUtf8);
    }

    public void WriteAsPerfettoProto(ref ProtoWriter protoWriter)
    {
        var enumerator = memory->GetEnumerator();
        while (enumerator.MoveNext(logEntrySizeOf))
        {
            var logEntrySpan = enumerator.Current;
            if (logEntrySpan.Length != logEntrySizeOf)
                throw new InvalidOperationException("Invalid log entry size, can't save profile.");

            fixed (byte* ptr = logEntrySpan)
            {
                ref var logEntry = ref Unsafe.AsRef<LogEntry>(ptr);
                protoWriter.StartTracePacket(logEntry.timestamp, 1);

                if ((logEntry.flags & LogFlags.TrackDescription) != 0)
                {
                    Debug.Assert((logEntry.flags & LogFlags.HasInlineName) != 0);
                    if (!enumerator.MoveNext((int)logEntry.nameIdOrNameLength))
                        throw new InvalidOperationException("Can't read name from the buffer");
                    var nameSpan = enumerator.Current;
                    if (nameSpan.Length != (int)logEntry.nameIdOrNameLength)
                        throw new InvalidOperationException("Invalid name length, can't save profile., expected: " + logEntry.nameIdOrNameLength + " got: " + nameSpan.Length);

                    protoWriter.AddTrackDescriptor((ulong)logEntry.trackId, nameSpan);
                }
                else
                {
                    PerfettoProto.TrackEvent.Types type = 0;
                    if ((logEntry.flags & LogFlags.Begin) != 0)
                        type = PerfettoProto.TrackEvent.Types.TYPE_SLICE_BEGIN;
                    else if ((logEntry.flags & LogFlags.End) != 0)
                        type = PerfettoProto.TrackEvent.Types.TYPE_SLICE_END;
                    else if ((logEntry.flags & LogFlags.InstantEvent) != 0)
                        type = PerfettoProto.TrackEvent.Types.TYPE_INSTANT;

                    if ((logEntry.flags & LogFlags.HasInlineName) == 0)
                    {
                        protoWriter.AddTrackEvent((ulong)logEntry.trackId, logEntry.nameIdOrNameLength, ""u8, type);
                    }
                    else
                    {
                        if (!enumerator.MoveNext((int)logEntry.nameIdOrNameLength))
                            throw new InvalidOperationException("Can't read name from the buffer");
                        var nameSpan = enumerator.Current;
                        if (nameSpan.Length != (int)logEntry.nameIdOrNameLength)
                            throw new InvalidOperationException("Invalid name length, can't save profile., expected: " + logEntry.nameIdOrNameLength + " got: " + nameSpan.Length);

                        protoWriter.AddTrackEvent((ulong)logEntry.trackId, 0, nameSpan, type);
                    }
                }

                protoWriter.CloseSub();
            }
        }
    }

    public void Free()
    {
        if (ownsMemory)
        {
            memory->Free();
            Marshal.FreeHGlobal((IntPtr)memory);
        }
    }

    [Flags]
    internal enum LogFlags
    {
        Begin = 1,
        End = 2,
        InstantEvent = 4,
        TrackDescription = 8,
        HasInlineName = 16,
    }

    internal struct LogEntry
    {
        public ulong timestamp;
        public int trackId;
        public ulong nameIdOrNameLength;
        public LogFlags flags;
    }
}