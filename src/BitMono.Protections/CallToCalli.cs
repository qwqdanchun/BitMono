﻿namespace BitMono.Protections;

[DoNotResolve(Members.SpecialRuntime)]
public class CallToCalli : IStageProtection
{
    private readonly IInjector m_Injector;
    private readonly IRenamer m_Renamer;

    public CallToCalli(IInjector injector, IRenamer renamer)
    {
        m_Injector = injector;
        m_Renamer = renamer;
    }

    public PipelineStages Stage => PipelineStages.ModuleWrite;

    public Task ExecuteAsync(ProtectionContext context, ProtectionParameters parameters, CancellationToken cancellationToken = default)
    {
        var runtimeMethodHandle = context.Importer.ImportType(typeof(RuntimeMethodHandle)).ToTypeSignature(isValueType: true);
        var getTypeFromHandleMethod = context.Importer.ImportMethod(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), new Type[]
        {
            typeof(RuntimeTypeHandle)
        }));
        var getModuleMethod = context.Importer.ImportMethod(typeof(Type).GetProperty(nameof(Type.Module)).GetMethod);
        var resolveMethodMethod = context.Importer.ImportMethod(typeof(Module).GetMethod(nameof(Module.ResolveMethod), new Type[]
        {
            typeof(int)
        }));
        var getMethodHandleMethod = context.Importer.ImportMethod(typeof(MethodBase).GetProperty(nameof(MethodBase.MethodHandle)).GetMethod);
        var getFunctionPointerMethod = context.Importer.ImportMethod(typeof(RuntimeMethodHandle).GetMethod(nameof(RuntimeMethodHandle.GetFunctionPointer)));

        var moduleType = context.Module.GetOrCreateModuleType(); 
        foreach (var method in parameters.Targets.OfType<MethodDefinition>())
        {
            if (method.CilMethodBody is { } body && method.DeclaringType != moduleType)
            {
                for (var i = 0; i < body.Instructions.Count; i++)
                {
                    if (body.Instructions[i].OpCode == CilOpCodes.Call
                        && body.Instructions[i].Operand is IMethodDescriptor methodDescriptor)
                    {
                        var callingMethod = methodDescriptor.Resolve();
                        if (callingMethod != null)
                        {
                            if (context.Module.TryLookupMember(callingMethod.MetadataToken, out var callingMethodMetadata))
                            {
                                var runtimeMethodHandleLocal = new CilLocalVariable(runtimeMethodHandle);
                                body.LocalVariables.Add(runtimeMethodHandleLocal);
                                body.Instructions[i].ReplaceWith(CilOpCodes.Ldtoken, moduleType);
                                body.Instructions.InsertRange(i + 1, new CilInstruction[]
                                {
                                    new CilInstruction(CilOpCodes.Call, getTypeFromHandleMethod),
                                    new CilInstruction(CilOpCodes.Callvirt, getModuleMethod),
                                    new CilInstruction(CilOpCodes.Ldc_I4, callingMethodMetadata.MetadataToken.ToInt32()),
                                    new CilInstruction(CilOpCodes.Call, resolveMethodMethod),
                                    new CilInstruction(CilOpCodes.Callvirt, getMethodHandleMethod),
                                    new CilInstruction(CilOpCodes.Stloc, runtimeMethodHandleLocal),
                                    new CilInstruction(CilOpCodes.Ldloca, runtimeMethodHandleLocal),
                                    new CilInstruction(CilOpCodes.Call, getFunctionPointerMethod),
                                    new CilInstruction(CilOpCodes.Calli, callingMethod.Signature.MakeStandAloneSignature())
                                });
                            }
                        }
                    }
                }
            }
        }
        return Task.CompletedTask;
    }
}