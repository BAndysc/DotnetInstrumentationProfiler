using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Microsoft.Build.Utilities;

namespace InstrumentationProfilingBuildTask;

internal class InstrumentationTool
{
    public TaskLoggingHelper Log { get; }

    public int MinimumMethodSize { get; }

    private HashSet<MethodDef> instrumentedMethods = new HashSet<MethodDef>();

    public InstrumentationTool(TaskLoggingHelper log, int minimumMethodSize)
    {
        Log = log;
        MinimumMethodSize = minimumMethodSize;
    }

    public bool Process(string assemblyPath, string profilerAssemblyPath)
    {
        // Load the main assembly and the Profiler assembly
        ModuleDefMD? module = ModuleDefMD.Load(assemblyPath);
        ModuleDefMD? profilerModule = ModuleDefMD.Load(profilerAssemblyPath);

        // Find the Profiler type and the Start method
        TypeDef? profilerType = profilerModule.Types.FirstOrDefault(t => t.FullName == "DotnetProfiler.Profiler");
        if (profilerType == null)
        {
            throw new InvalidOperationException("Profiler type not found in Profiler assembly.");
        }

        MethodDef? startMethod = profilerType.Methods.FirstOrDefault(m => m.Name == "Start" && m.ParamDefs.Count == 1);
        if (startMethod == null)
        {
            throw new InvalidOperationException("Start method not found in Profiler class.");
        }

        var stopMethod = profilerType.Methods.FirstOrDefault(m => m.Name == "Stop" && m.ParamDefs.Count == 0);
        if (stopMethod == null)
        {
            throw new InvalidOperationException("Stop method not found in Profiler class.");
        }

        var registerMethod = profilerType.Methods.FirstOrDefault(m => m.Name == "RegisterName");
        if (registerMethod == null)
        {
            throw new InvalidOperationException("Register method not found in Profiler class.");
        }

        // Import the Start method into the main module
        MemberRef? importedStartMethod = module.Import(startMethod);
        MemberRef? importedStopMethod = module.Import(stopMethod);
        MemberRef? importedRegisterMethod = module.Import(registerMethod);

        foreach (var type in module.Types.ToList())
            ProcessType(type, module, importedStartMethod, importedStopMethod, importedRegisterMethod);

        // Save the modified assembly
        module.Write(assemblyPath);

        return true;
    }

    private void ProcessType(TypeDef type, ModuleDef module, IMethod importedStartMethod, IMethod importedStopMethod, IMethod importedRegisterMethod)
    {
        foreach (var methodDef in type.Methods.ToList())
        {
            if (!methodDef.HasBody)
                continue;

            if (methodDef.Body.Instructions.Count < MinimumMethodSize)
                continue;

            if (!instrumentedMethods.Add(methodDef))
                continue;

            Log.LogWarning("Instrumenting method: " + methodDef.FullName);

            var methodIdField = TypeDefExtensions.GetOrCreateMethodIdField(type, methodDef, module, importedRegisterMethod);

            var ilProcessor = methodDef.Body.Instructions;

            ilProcessor.Insert(0, Instruction.Create(OpCodes.Ldsfld, methodIdField)); // Load the method id
            ilProcessor.Insert(1, Instruction.Create(OpCodes.Call, importedStartMethod)); // Call the Start method

            for (int i = 0; i < methodDef.Body.Instructions.Count; ++i)
            {
                if (methodDef.Body.Instructions[i].OpCode == OpCodes.Ret)
                {
                    methodDef.Body.Instructions.Insert(i, Instruction.Create(OpCodes.Call, importedStopMethod));
                    i += 1;
                }
            }

            for (int i = 0; i < methodDef.Body.Instructions.Count; ++i)
            {
                if (methodDef.Body.Instructions[i].IsConditionalBranch() ||
                    methodDef.Body.Instructions[i].IsLeave())
                {
                    var oper = (Instruction)methodDef.Body.Instructions[i].Operand;
                    if (oper.OpCode == OpCodes.Ret)
                        methodDef.Body.Instructions[i].Operand = methodDef.Body.Instructions[methodDef.Body.Instructions.IndexOf(oper) - 1];
                }
            }

            if (methodDef.Body.HasExceptionHandlers)
            {
                foreach (var handler in methodDef.Body.ExceptionHandlers)
                {
                    if (handler.HandlerEnd.OpCode == OpCodes.Ret)
                    {
                        handler.HandlerEnd = methodDef.Body.Instructions[methodDef.Body.Instructions.IndexOf(handler.HandlerEnd) - 1];
                    }
                }
            }

            // Recalculate the method's max stack size
            methodDef.Body.SimplifyMacros(methodDef.Parameters);
            methodDef.Body.OptimizeMacros();
        }

        // Process nested types recursively
        foreach (var nestedType in type.NestedTypes)
        {
            ProcessType(nestedType, module, importedStartMethod, importedStopMethod, importedRegisterMethod);
        }
    }
}