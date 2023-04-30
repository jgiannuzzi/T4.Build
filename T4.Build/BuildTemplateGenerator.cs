using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.IO;
using System.Text;
using Mono.TextTemplating;

namespace T4.Build
{
    class BuildTemplateGenerator : TemplateGenerator
    {
        List<string> includedFiles = new List<string>();
        string lockPath;
        ParsedTemplate parsedTemplate;
        readonly Func<string, string> preprocessor = null;

        public BuildTemplateGenerator(string template)
        {
            TemplateFile = template;
            Engine.UseInProcessCompiler();
        }

        public BuildTemplateGenerator(string template, Func<string, string> preprocessor)
            : this(template)
        {
            this.preprocessor = preprocessor;
        }

        public string[] IncludedFiles
        {
            get
            {
                _ = this.ParsedTemplate;
                return includedFiles.ToArray();
            }
        }

        string LockPath
        {
            get
            {
                if (string.IsNullOrEmpty(lockPath))
                {
                    var info = new FileInfo(TemplateFile);
                    lockPath = Path.Combine(info.DirectoryName, $".{info.Name}.lock");
                }

                return lockPath;
            }
        }

        public new string OutputFile
        {
            get
            {
                if (string.IsNullOrEmpty(base.OutputFile))
                {
                    if (ParsedTemplate == null)
                        return null;

                    var ext = ".txt";
                    foreach (var dt in ParsedTemplate.Directives)
                    {
                        if (dt.Name == "output")
                            ext = dt.Attributes.GetValueOrDefault("extension", ext);
                    }
                    base.OutputFile = Path.ChangeExtension(TemplateFile, ext);
                }

                return base.OutputFile;
            }
        }

        ParsedTemplate ParsedTemplate
        {
            get
            {
                if (parsedTemplate == null)
                {
                    try
                    {
                        var pt = ParsedTemplate.FromText(File.ReadAllText(TemplateFile), this);
                        if (pt.Errors.HasErrors)
                            Errors.AddRange(pt.Errors);
                        else
                            parsedTemplate = pt;
                    }
                    catch (Exception e)
                    {
                        Errors.Add(new CompilerError { ErrorText = $"Could not parse template '{TemplateFile}':\n{e}" });
                    }
                }

                return parsedTemplate;
            }
        }

        protected override bool LoadIncludeText(string requestFileName, out string content, out string location)
        {
            var result = base.LoadIncludeText(requestFileName, out content, out location);
            includedFiles.Add(location);
            return result;
        }

        public bool ProcessTemplate(bool skipUpToDate, out bool skipped)
        {
            skipped = false;

            var outputFile = OutputFile;
            if (string.IsNullOrWhiteSpace(outputFile))
            {
                return false;
            }

            if (skipUpToDate)
            {
                var templateInfo = new FileInfo(TemplateFile);
                var outputInfo = new FileInfo(outputFile);
                if (outputInfo.Exists
                    && templateInfo.LastWriteTime.Ticks < outputInfo.LastWriteTime.Ticks
                    && includedFiles.TrueForAll(x => (new FileInfo(x)).LastWriteTime.Ticks < outputInfo.LastWriteTime.Ticks)
                )
                {
                    skipped = true;
                    return true;
                }
            }

            string content;
            try
            {
                content = File.ReadAllText(TemplateFile);
            }
            catch (IOException ex)
            {
                Errors.Clear();
                AddError($"Could not read input file '{TemplateFile}':\n{ex}");
                return false;
            }

            if (!(preprocessor is null))
            {
                content = preprocessor(content);
            }
            ProcessTemplate(TemplateFile, content, ref outputFile, out var output);

            try
            {
                if (!Errors.HasErrors)
                {
                    File.WriteAllText(outputFile, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }
            }
            catch (IOException ex)
            {
                AddError($"Could not write output file '{outputFile}':\n{ex}");
            }

            return !Errors.HasErrors;
        }

        CompilerError AddError(string error)
        {
            var err = new CompilerError { ErrorText = error };
            Errors.Add(err);
            return err;
        }
    }
}