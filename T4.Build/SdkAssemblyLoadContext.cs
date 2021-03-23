using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Mono.TextTemplating.CodeCompilation;

namespace T4.Build
{
    class SdkAssemblyLoadContext : AssemblyLoadContext
    {
        readonly string runtimeDir;
        readonly string roslynDir;

        public SdkAssemblyLoadContext()
        {
            runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);

            var dotnetRoot = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(runtimeDir)));
            string MakeRoslynPath(string d) => Path.Combine(d, "Roslyn", "bincore");
            var sdkDir = FindHighestVersionedDirectory(Path.Combine(dotnetRoot, "sdk"), d => Directory.Exists(MakeRoslynPath(d)));
            roslynDir = MakeRoslynPath(sdkDir);
        }

        static string FindHighestVersionedDirectory(string parentFolder, Func<string, bool> validate)
        {
            string bestMatch = null;
            SemVersion bestVersion = SemVersion.Zero;
            foreach (var dir in Directory.EnumerateDirectories(parentFolder))
            {
                var name = Path.GetFileName(dir);
                if (SemVersion.TryParse(name, out var version) && version.Major >= 0)
                {
                    if (version > bestVersion && (validate == null || validate(dir)))
                    {
                        bestVersion = version;
                        bestMatch = dir;
                    }
                }
            }
            return bestMatch;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            switch (assemblyName.Name)
            {
                case "Microsoft.CodeAnalysis":
                case "Microsoft.CodeAnalysis.CSharp":
                    return LoadFromAssemblyPath(Path.Combine(roslynDir, $"{assemblyName.Name}.dll"));
                case "System.Collections.Immutable":
                case "System.Reflection.Metadata":
                    return LoadFromAssemblyPath(Path.Combine(runtimeDir, $"{assemblyName.Name}.dll"));
            }

            return null;
        }
    }
}