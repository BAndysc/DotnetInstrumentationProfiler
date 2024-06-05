using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace InstrumentationProfilingBuildTask;

internal static class TypeDefExtensions
{
    public static FieldDef GetOrCreateMethodIdField(TypeDef type, MethodDef methodDef, ModuleDef module, IMethod importedRegisterMethod)
    {
        // Check if the static field for the method identifier already exists
        var methodIdFieldName = $"<{methodDef.Name}>k__MethodId";
        var methodIdField = type.Fields.FirstOrDefault(f => f.Name == methodIdFieldName);
        if (methodIdField != null)
        {
            return methodIdField;
        }

        // Create the static field for the method identifier
        methodIdField = new FieldDefUser(methodIdFieldName, new FieldSig(module.CorLibTypes.UInt64), FieldAttributes.Private | FieldAttributes.Static);
        type.Fields.Add(methodIdField);

        // Ensure the type has a static constructor
        var cctor = type.FindOrCreateStaticConstructor();

        // Modify the static constructor to initialize the method identifier
        var ilProcessor = cctor.Body.Instructions;
        ilProcessor.Insert(0, Instruction.Create(OpCodes.Ldstr, methodDef.FullName)); // Load the method name
        ilProcessor.Insert(1, Instruction.Create(OpCodes.Call, importedRegisterMethod)); // Call the Register method
        ilProcessor.Insert(2, Instruction.Create(OpCodes.Stsfld, methodIdField)); // Store the returned id in the static field

        // Recalculate the static constructor's max stack size
        cctor.Body.SimplifyMacros(cctor.Parameters);
        cctor.Body.OptimizeMacros();

        return methodIdField;
    }
}