﻿using BitMono.API.Protecting;
using BitMono.API.Protecting.Pipeline;
using BitMono.API.Protecting.Resolvers;
using BitMono.Core.Models;
using BitMono.Core.Protecting.Resolvers;
using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ILogger = Serilog.ILogger;

namespace BitMono.Obfuscation
{
    public class ProtectionsSorter
    {
        private readonly IDnlibDefFeatureObfuscationAttributeHavingResolver m_DnlibDefFeatureObfuscationAttributeHavingResolver;
        private readonly AssemblyDef m_ModuleDefMDAssembly;
        private readonly ILogger m_Logger;

        public ProtectionsSorter(
            IDnlibDefFeatureObfuscationAttributeHavingResolver dnlibDefFeatureObfuscationAttributeHavingResolver,
            AssemblyDef moduleDefMDAssembly,
            ILogger logger)
        {
            m_DnlibDefFeatureObfuscationAttributeHavingResolver = dnlibDefFeatureObfuscationAttributeHavingResolver;
            m_ModuleDefMDAssembly = moduleDefMDAssembly;
            m_Logger = logger;
        }

        public Task<ProtectionsSortingResult> SortAsync(ICollection<IProtection> protections, List<ProtectionSettings> protectionSettings)
        {
            protections = new DependencyResolver(protections, protectionSettings, m_Logger)
                .Sort(out ICollection<string> skipped);
            var deprecatedProtections = protections.Where(p => p.GetType().GetCustomAttribute<ObsoleteAttribute>(false) != null);
            var stageProtections = protections.Where(p => p is IStageProtection).Cast<IStageProtection>();
            var pipelineProtections = protections.Where(p => p is IPipelineProtection).Cast<IPipelineProtection>();
            var obfuscationAttributeExcludingProtections = protections.Where(p =>
                m_DnlibDefFeatureObfuscationAttributeHavingResolver.Resolve(m_ModuleDefMDAssembly, p.GetType().Name));
            
            protections.Except(stageProtections).Except(obfuscationAttributeExcludingProtections);

            return Task.FromResult(new ProtectionsSortingResult
            {
                Protections = protections,
                DeprecatedProtections = deprecatedProtections,
                Skipped = skipped,
                StageProtections = stageProtections,
                PipelineProtections = pipelineProtections,
                ObfuscationAttributeExcludingProtections = obfuscationAttributeExcludingProtections
            });
        }
    }
}