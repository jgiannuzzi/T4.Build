using System;
using System.IO;
using System.Reflection;
using Mono.TextTemplating;

namespace T4.Build
{
    public static class RoslynTemplatingEngineExtensions
    {
        private static readonly object sync = new object();
        private static MethodInfo useInProcessCompilerMethod;

        private static MethodInfo UseInProcessCompilerMethod
        {
            get
            {
                lock (sync)
                {
                    if (useInProcessCompilerMethod == null)
                    {
                        var context = new SdkAssemblyLoadContext();
                        var assembly = context.LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(typeof(RoslynTemplatingEngineExtensions).Assembly.Location), "Mono.TextTemplating.Roslyn.dll"));
                        var extensions = assembly.GetType("Mono.TextTemplating.RoslynTemplatingEngineExtensions");
                        useInProcessCompilerMethod = extensions.GetMethod("UseInProcessCompiler", new Type[] { typeof(TemplatingEngine) });
                    }
                }

                return useInProcessCompilerMethod;
            }
        }

        public static void UseInProcessCompiler(this TemplatingEngine engine)
        {
            UseInProcessCompilerMethod.Invoke(null, new object[] { engine });
        }
    }
}