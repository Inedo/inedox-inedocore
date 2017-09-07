﻿using System.Collections;
using System.ComponentModel;
using System.Linq;
using Inedo.Documentation;
#if Otter
using Inedo.Otter.Data;
using Inedo.Otter.Extensibility;
#elif Hedgehog
using Inedo.Extensibility;
using Inedo.Extensibility.VariableFunctions;
#elif BuildMaster
using Inedo.BuildMaster.Data;
using Inedo.BuildMaster.Extensibility;
#endif

namespace Inedo.Extensions.VariableFunctions.Server
{
    [ScriptAlias("AllEnvironments")]
    [Description("Returns a list of all environments.")]
    [Tag("servers")]
    [Example(@"
# log all environments in context to the execution log
foreach $Env in @AllEnvironments
{
    Log-Information $Env;
}
")]
    public sealed class AllEnvironmentsVariableFunction : CommonVectorVariableFunction
    {
        protected override IEnumerable EvaluateVector(object context)
        {
#if Hedgehog
            return SDK.GetEnvironments().Select(e => e.Name);
#else
            return DB.Environments_GetEnvironments().Select(e => e.Environment_Name);
#endif
        }
    }
}
