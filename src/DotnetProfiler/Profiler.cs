using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using ProtoZeroSharp;

namespace DotnetProfiler;

/// <summary>
/// A simple profiler that can be used to trace events and save them to a file in the Perfetto protobuf format.
/// </summary>
public static class Profiler
{
    private const int LongestDecimalInt32Length = 10;

    private static List<string> names = new();

    private static volatile bool enabled = false;

    private static GarbageCollectorETWEventsListener? gcListener;

    private static ThreadLocal<FastProfilerWriter> writers = new(() =>
    {
        FastProfilerWriter writer = new();
        int uuid = Environment.CurrentManagedThreadId;

        Span<byte> name = stackalloc byte["Thread "u8.Length + LongestDecimalInt32Length + 1];
        "Thread "u8.CopyTo(name);
        Utf8Formatter.TryFormat(uuid, name.Slice("Thread "u8.Length), out _);

        writer.AddTrackDescription((ulong)Stopwatch.GetTimestamp(), uuid, name);

        return writer;
    }, true);

    /// <summary>
    /// Enables the profiler.
    /// </summary>
    public static void Enable()
    {
        enabled = true;
        gcListener = new GarbageCollectorETWEventsListener();
    }

    /// <summary>
    /// Disables the profiler.
    /// </summary>
    public static void Disable()
    {
        enabled = false;
        gcListener?.Dispose();
        gcListener = null;
    }

    /// <summary>
    /// Registers a name for an event, which later can be used in <see cref="Start(ulong)"/>.
    /// </summary>
    /// <param name="name">Name to register</param>
    /// <returns>An identifier for the registered name</returns>
    public static ulong RegisterName(string name)
    {
        names.Add(name);
        return (ulong)names.Count;
    }

    /// <summary>
    /// Begins a new event with the given name identifier.
    /// </summary>
    /// <param name="nameId">Name id registered before with <see cref="RegisterName(string)"/></param>
    public static void Start(ulong nameId)
    {
        if (!enabled)
            return;

        var tid = Environment.CurrentManagedThreadId;

        var writer = writers.Value;
        writer.LogStart((ulong)Stopwatch.GetTimestamp(), tid, nameId);
        writers.Value = writer;
    }

    /// <summary>
    /// Adds a custom track to the trace, which later can be used in <see cref="InstantEvent(System.ReadOnlySpan{byte}, int)"/>.
    /// </summary>
    /// <param name="trackId">An id for the track to register, please note it shall not clash with possible Thread Ids thus choose high numbers.</param>
    /// <param name="name">Utf8 encoded name for the track</param>
    public static void RegisterCustomTrack(int trackId, ReadOnlySpan<byte> name)
    {
        var writer = writers.Value;
        writer.AddTrackDescription((ulong)Stopwatch.GetTimestamp(), trackId, name);
        writers.Value = writer;
    }

    /// <summary>
    /// Registers an instant event with the given name.
    /// </summary>
    /// <param name="nameUtf8">Utf8-encoded name for the event</param>
    public static void InstantEvent(ReadOnlySpan<byte> nameUtf8)
    {
        if (!enabled)
            return;

        InstantEvent(nameUtf8, Environment.CurrentManagedThreadId);
    }

    /// <summary>
    /// Registers an instant event at a specific track.
    /// </summary>
    /// <param name="nameUtf8"></param>
    /// <param name="trackId">Track id used before with <see cref="RegisterCustomTrack"/></param>
    public static void InstantEvent(ReadOnlySpan<byte> nameUtf8, int trackId)
    {
        if (!enabled)
            return;

        var writer = writers.Value;
        writer.LogInstantEvent((ulong)Stopwatch.GetTimestamp(), trackId, nameUtf8);
        writers.Value = writer;
    }


    /// <summary>
    /// Begins a new event with the given name.
    /// </summary>
    /// <param name="utf8Name"></param>
    public static void Start(ReadOnlySpan<byte> utf8Name)
    {
        if (!enabled)
            return;

        Start(utf8Name, Environment.CurrentManagedThreadId);
    }

    /// <summary>
    /// Begins a new event with the given name at a specific track.
    /// </summary>
    /// <param name="utf8Name"></param>
    /// <param name="trackId"></param>
    public static void Start(ReadOnlySpan<byte> utf8Name, int trackId)
    {
        if (!enabled)
            return;

        var writer = writers.Value;
        writer.LogStart((ulong)Stopwatch.GetTimestamp(), trackId, utf8Name);
        writers.Value = writer;
    }

    /// <summary>
    /// Stops profiling the current event.
    /// </summary>
    public static void Stop()
    {
        if (!enabled)
            return;

        Stop(Environment.CurrentManagedThreadId);
    }

    /// <summary>
    /// Stops profiling the current event at a specific track.
    /// </summary>
    /// <param name="trackId"></param>
    public static void Stop(int trackId)
    {
        if (!enabled)
            return;

        var writer = writers.Value;
        writer.LogStop((ulong)Stopwatch.GetTimestamp(), trackId);
        writers.Value = writer;
    }

    /// <summary>
    /// Saves the trace to a file in a Perfetto protobuf format. Not thread safe. Should be only used when the profiler is disabled.
    /// </summary>
    /// <param name="filePath">Path to the output file. If exits, will be overriden.</param>
    public static void SaveTrace(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);

        using var file = File.OpenWrite(filePath);

        ProtoWriter protoWriter = new();
        {
            protoWriter.StartTracePacket(0, 1, 1);
            protoWriter.StartInternedData();

            for (int index = 0; index < names.Count; index++)
            {
                string? name = names[index];
                protoWriter.AddEventName((ulong)(index+1), Encoding.UTF8.GetBytes(name));
            }
            protoWriter.CloseSub();
            protoWriter.CloseSub();
        }

        foreach (var fastWriter in writers.Values)
            fastWriter.WriteAsPerfettoProto(ref protoWriter);

        protoWriter.WriteTo(file);
        protoWriter.Free();

    }
}
