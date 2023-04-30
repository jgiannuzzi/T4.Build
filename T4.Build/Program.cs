using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace T4.Build
{
    class Program
    {
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand("Tool to transform T4 templates during build");

            var timeoutOption = new Option<int>(new string[] { "--lock-timeout", "-t" }, () => 60, "Timeout in seconds to wait for another instance to be done");
            rootCommand.Add(timeoutOption);

            var tpArgument = new Argument<FileInfo[]>("templates", "The templates to transform").ExistingOnly();

            var transformCommand = new Command("transform", "Transform the T4 templates");
            transformCommand.Add(tpArgument);
            var skipOption = new Option<bool>(new[] { "--skip-up-to-date", "-s" }, "Skip templates whose output is up-to-date");
            transformCommand.Add(skipOption);
            var parallelOption = new Option<bool>(new[] { "--parallel", "-p" }, "Transform templates in parallel");
            transformCommand.Add(parallelOption);
            var variableOptions = new Option<string[]>(new[] { "--variable", "-v" }, "T4ParameterValues variable to replace within the template; syntax is \"Include=Value\"")
            {
                AllowMultipleArgumentsPerToken = false,
            };
            transformCommand.Add(variableOptions);
            transformCommand.Handler = CommandHandler.Create<FileInfo[], int, bool, bool, string[]>(Transform);
            rootCommand.Add(transformCommand);

            var cleanCommand = new Command("clean", "List the output files");
            cleanCommand.Add(tpArgument);
            cleanCommand.Handler = CommandHandler.Create<FileInfo[], int>(Clean);
            rootCommand.Add(cleanCommand);

            return rootCommand.InvokeAsync(args).Result;
        }

        static int Transform(FileInfo[] templates, int lockTimeout, bool skipUpToDate, bool parallel, string[] variable)
        {
            using (var locker = new Lock($"Global\\T4.Build.transform.{ComputeHash(templates)}.lock", lockTimeout))
            {
                var outputQueue = new ConcurrentQueue<String>();
                var errorQueue = new ConcurrentQueue<CompilerErrorCollection>();
                var didSomeWork = false;
                var stopwatch = new Stopwatch();

                var t4ParameterValues = ProcessT4ParameterValues(variable);
                stopwatch.Start();
                Parallel.ForEach(templates,
                    new ParallelOptions { MaxDegreeOfParallelism = parallel ? -1 : 1 },
                    t =>
                    {
                        var generator = new BuildTemplateGenerator(t.ToString(), templateContent => PreprocessTemplate(templateContent, t4ParameterValues));
                        try
                        {
                            bool skipped;
                            generator.ProcessTemplate(skipUpToDate, out skipped);
                            if (!skipped)
                                didSomeWork = true;
                            if (generator.Errors.HasErrors)
                                errorQueue.Enqueue(generator.Errors);
                            else
                                outputQueue.Enqueue(generator.OutputFile);
                        }
                        catch (Exception e)
                        {
                            errorQueue.Enqueue(new CompilerErrorCollection { new CompilerError { ErrorText = $"Could not process template '{t}':\n{e}" } });
                        }
                    });
                stopwatch.Stop();

                // Sort the generated file names so that the MSBuild compilation cache hash remains stable
                var outputs = outputQueue.ToArray();
                Array.Sort(outputs, new CaseInsensitiveComparer());
                foreach (var output in outputs)
                    Console.WriteLine(output);

                if (didSomeWork && errorQueue.IsEmpty)
                    Console.Error.WriteLine($"Templates transformed for {Path.GetFileName(Directory.GetCurrentDirectory())} (in {stopwatch.ElapsedMilliseconds / 1000.0} sec).");

                foreach (var errors in errorQueue)
                    LogErrors(errors);

                return errorQueue.IsEmpty ? 0 : 1;
            }
        }

        static int Clean(FileInfo[] templates, int lockTimeout)
        {
            using (var locker = new Lock($"Global\\T4.Build.clean.{ComputeHash(templates)}.lock", lockTimeout))
            {
                var hasErrors = false;

                foreach (var t in templates)
                {
                    var generator = new BuildTemplateGenerator(t.FullName);
                    var output = generator.OutputFile;
                    if (!string.IsNullOrEmpty(output) && File.Exists(output))
                    {
                        try
                        {
                            Console.WriteLine($"Deleting file \"{output}\"");
                            File.Delete(output);
                        }
                        catch (Exception e)
                        {
                            hasErrors = true;
                            Console.Error.WriteLine(e.Message);
                        }
                    }
                }

                return hasErrors ? 1 : 0;
            }
        }

        static string ComputeHash(FileInfo[] templates)
        {
            using (var hasher = SHA256.Create())
            {
                return String.Concat(
                    hasher.ComputeHash(
                        Encoding.UTF8.GetBytes(
                            String.Join(
                                ";",
                                templates.Select(x => x.FullName).Distinct().OrderBy(x => x)
                            )
                        )
                    ).Select(b => b.ToString("x2"))
                );
            }
        }

        static void LogErrors(CompilerErrorCollection errors)
        {
            var oldColor = Console.ForegroundColor;

            foreach (CompilerError err in errors)
            {
                Console.ForegroundColor = err.IsWarning ? ConsoleColor.Yellow : ConsoleColor.Red;
                if (!string.IsNullOrEmpty(err.FileName))
                {
                    var fileName = err.FileName;
                    if (fileName.StartsWith(Path.GetTempPath()))
                    {
                        string rel = Path.GetRelativePath(Path.GetTempPath(), fileName);
                        var components = rel.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                        fileName = string.Join(Path.DirectorySeparatorChar, components[1..^0]);
                    }
                    Console.Error.Write(fileName);
                }
                if (err.Line > 0)
                {
                    Console.Error.Write("(");
                    Console.Error.Write(err.Line);
                    if (err.Column > 0)
                    {
                        Console.Error.Write(",");
                        Console.Error.Write(err.Column);
                    }
                    Console.Error.Write(")");
                }
                if (!string.IsNullOrEmpty(err.FileName) || err.Line > 0)
                {
                    Console.Error.Write(": ");
                }
                Console.Error.Write(err.IsWarning ? "WARNING: " : "ERROR: ");
                Console.Error.WriteLine(err.ErrorText);
            }

            Console.ForegroundColor = oldColor;
        }

        static IReadOnlyDictionary<string, string> ProcessT4ParameterValues(string[] variableSets)
        {
            if (variableSets?.Any() != true)
            {
                return null;
            }

            var t4ParameterValues = new Dictionary<string, string>(variableSets.Length);
            foreach (var variableSet in variableSets)
            {
                var pair = variableSet.Split('=', 2, StringSplitOptions.None);
                t4ParameterValues.Add($"$({pair[0]})", pair[1]);
            }

            return t4ParameterValues;
        }

        static string PreprocessTemplate(string templateContent, IReadOnlyDictionary<string, string> t4ParameterValues)
        {
            if (string.IsNullOrWhiteSpace(templateContent) || t4ParameterValues?.Keys.Any() != true)
            {
                return templateContent;
            }

            var replacementBuilder = (StringBuilder)null;
            foreach (var t4ParameterValuesPair in t4ParameterValues)
            {
                if (replacementBuilder is null && templateContent.IndexOf(t4ParameterValuesPair.Key) > -1)
                {
                    replacementBuilder = new StringBuilder(templateContent);
                }
                replacementBuilder?.Replace(t4ParameterValuesPair.Key, t4ParameterValuesPair.Value);
            }

            return replacementBuilder is null ? templateContent : replacementBuilder.ToString();
        }
    }
}
