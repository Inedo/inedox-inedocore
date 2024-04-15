using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("InedoCore")]
[assembly: AssemblyDescription("Contains core functionality for Inedo products.")]
[assembly: AssemblyCompany("Inedo")]
[assembly: AssemblyCopyright("Copyright © Inedo 2023")]
[assembly: ComVisible(false)]
[assembly: AssemblyVersion("3.0.0")]
[assembly: AssemblyFileVersion("3.0.0")]
[assembly: AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter | InedoProduct.ProGet)]

#if DEBUG
[assembly: InternalsVisibleTo("InedoCoreTests")]
#endif
