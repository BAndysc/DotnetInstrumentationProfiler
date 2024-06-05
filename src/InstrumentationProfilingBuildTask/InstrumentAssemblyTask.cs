using System;
using DotnetProfiler;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace InstrumentationProfilingBuildTask
{
    public class InstrumentAssemblyTask : Task
    {
        [Required]
        public string AssemblyPath { get; set; }

        public int MinimumMethodSize { get; set; } = 10;

        public override bool Execute()
        {
            Log.LogWarning("Will process assembly: " + AssemblyPath);
            Log.LogWarning("DotnetProfiler assembly: " + typeof(Profiler).Assembly.Location);
            Log.LogWarning("Minimum Method Size: " + MinimumMethodSize);
            var tool = new InstrumentationTool(Log, MinimumMethodSize);
            tool.Process(AssemblyPath, typeof(Profiler).Assembly.Location);
            return true;
        }
    }
}

