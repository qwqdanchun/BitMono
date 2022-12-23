﻿namespace BitMono.Obfuscation;

public class BitMonoObfuscator
{
    private readonly ProtectionContext m_ProtectionContext;
    private IEnumerable<IMemberResolver> m_MemberDefinitionResolvers;
    private readonly ICollection<IPacker> m_Packers;
    private readonly ICollection<IProtection> m_Protections;
    private readonly IDataWriter m_DataWriter;
    private MembersResolver m_MemberResolver;
    private readonly ILogger m_Logger;

    public BitMonoObfuscator(
        ProtectionContext protectionContext,
        IEnumerable<IMemberResolver> memberDefinitionResolvers,
        ICollection<IProtection> protections,
        ICollection<IPacker> packers,
        IDataWriter dataWriter,
        ILogger logger)
    {
        m_MemberDefinitionResolvers = memberDefinitionResolvers;
        m_Protections = protections;
        m_Packers = packers;
        m_ProtectionContext = protectionContext;
        m_DataWriter = dataWriter;
        m_MemberResolver = new MembersResolver();
        m_Logger = logger.ForContext<BitMonoObfuscator>();
    }

    public async Task StartAsync(CancellationTokenSource cancellationTokenSource)
    {
        var cancellationToken = cancellationTokenSource.Token;
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var methodDefinition in m_ProtectionContext.Module.FindDefinitions().OfType<MethodDefinition>())
        {
            if (methodDefinition.CilMethodBody != null)
            {
                methodDefinition.CilMethodBody.Instructions.ExpandMacros();
                methodDefinition.CilMethodBody.Instructions.OptimizeMacros();
            }
        }
        foreach (var protection in m_Protections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((protection is IPipelineStage) == false)
            {
                var protectionName = protection.GetName();
                var protectionParameters = new ProtectionParametersCreator(m_MemberResolver, m_MemberDefinitionResolvers).Create(protectionName, m_ProtectionContext.Module);
                await protection.ExecuteAsync(m_ProtectionContext, protectionParameters, cancellationToken);
                m_Logger.Information("{0} -> OK", protectionName);
            }
        }
        foreach (var protection in m_Protections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (protection is IPipelineStage stage)
            {
                if (stage.Stage == PipelineStages.ModuleWrite)
                {
                    var protectionName = protection.GetName();
                    var protectionParameters = new ProtectionParametersCreator(m_MemberResolver, m_MemberDefinitionResolvers).Create(protectionName, m_ProtectionContext.Module);
                    await protection.ExecuteAsync(m_ProtectionContext, protectionParameters, cancellationToken);
                    m_Logger.Information("{0} -> OK", protectionName);
                }
            }

            if (protection is IPipelineProtection pipelineProtection)
            {
                foreach (var protectionPhase in pipelineProtection.PopulatePipeline())
                {
                    if (protectionPhase.Item2 == PipelineStages.ModuleWrite)
                    {
                        var protectionName = protection.GetName();
                        var protectionParameters = new ProtectionParametersCreator(m_MemberResolver, m_MemberDefinitionResolvers).Create(protectionName, m_ProtectionContext.Module);
                        await protectionPhase.Item1.ExecuteAsync(m_ProtectionContext, protectionParameters, cancellationToken);
                        m_Logger.Information("{0} -> Pipeline OK", protectionName);
                    }
                }
            }
        }

        try
        {
            var memoryStream = new MemoryStream();
            var image = m_ProtectionContext.PEImageBuilder.CreateImage(m_ProtectionContext.Module).ConstructedImage;
            new ManagedPEFileBuilder().CreateFile(image).Write(memoryStream);
            m_ProtectionContext.ModuleOutput = memoryStream.ToArray();
            await m_DataWriter.WriteAsync(m_ProtectionContext.BitMonoContext.OutputFile, m_ProtectionContext.ModuleOutput);
        }
        catch (Exception ex)
        {
            m_Logger.Fatal(ex, "Error while writing file!");
            cancellationTokenSource.Cancel();
            return;
        }

        foreach (var packer in m_Packers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var packerName = packer.GetName();
            var protectionParameters = new ProtectionParametersCreator(m_MemberResolver, m_MemberDefinitionResolvers).Create(packerName, m_ProtectionContext.Module);
            await packer.ExecuteAsync(m_ProtectionContext, protectionParameters, cancellationToken);
            m_Logger.Information("{0} -> Packer OK", packerName);
        }
    }
}