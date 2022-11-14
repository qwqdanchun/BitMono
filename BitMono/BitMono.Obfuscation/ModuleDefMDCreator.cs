﻿using BitMono.Obfuscation.API;
using dnlib.DotNet;
using System.Threading.Tasks;

namespace BitMono.Obfuscation
{
    public class ModuleDefMDCreator : IModuleDefMDCreator
    {
        private readonly byte[] m_ModuleBytes;

        public ModuleDefMDCreator(byte[] moduleBytes)
        {
            m_ModuleBytes = moduleBytes;
        }

        public async Task<ModuleDefMDCreationResult> CreateAsync()
        {
            var assemblyResolver = new AssemblyResolver();
            var moduleContext = new ModuleContext(assemblyResolver);
            assemblyResolver.DefaultModuleContext = moduleContext;
            var moduleCreationOptions = new ModuleCreationOptions(assemblyResolver.DefaultModuleContext, CLRRuntimeReaderKind.Mono);
            var moduleDefMD = ModuleDefMD.Load(m_ModuleBytes, moduleCreationOptions);

            var moduleDefMDWriterOptions = await new ModuleDefMDWriterOptionsCreator().CreateAsync(moduleDefMD);
            return new ModuleDefMDCreationResult
            {
                AssemblyResolver = assemblyResolver,
                ModuleContext = moduleContext,
                ModuleCreationOptions = moduleCreationOptions,
                ModuleDefMD = moduleDefMD,
                ModuleWriterOptions = moduleDefMDWriterOptions,
            };
        }
    }
}