using Ditto.CommandLine;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var parameters = ParseParameters(args);
            if (parameters == null)
            {
                CommandLine.WriteHelp(parameters, System.Console.Out);
                return;
            }
            Convert(parameters);
        }

        private static Parameters ParseParameters(string[] args)
        {
            try
            {
                Parameters parameters = null;
                var results = CommandLine.TryParse(args, out parameters);
                if (results.Any() || parameters.Help)
                {
                    if (results.Any(r => r != null))
                    {
                        foreach (var result in results.Where(r => r != null))
                            System.Console.Error.WriteLine(result.ErrorMessage);
                        System.Console.Error.WriteLine();
                    }
                    return null;
                }
                return parameters;
            }
            catch (AggregateException ex)
            {
                ex.Flatten();
                foreach (var ex2 in ex.InnerExceptions)
                    System.Console.Error.WriteLine(ex2.Message);
                System.Console.Error.WriteLine();
                return null;
            }
        }

        private static void Convert(Parameters parameters)
        {
            try
            {
                var paths = GetPaths(parameters);
                var task = ConvertAsync(paths, parameters, CancellationToken.None);
                task.GetAwaiter().GetResult();

            }
            catch (AggregateException ex)
            {
                ex.Flatten();
                foreach (var ex2 in ex.InnerExceptions)
                    System.Console.Error.WriteLine(ex2.Message);
                System.Console.Error.WriteLine();
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine(ex.Message);
                System.Console.Error.WriteLine();
            }
        }

        private static Paths GetPaths(Parameters parameters)
        {
            return new Paths
            {
                Source = GetPath(parameters.SourcePath, true),
                Destination = GetPath(parameters.DestinationPath, true),
                ContentLayout = GetPath(parameters.ContentLayoutPath, false),
            };
        }

        private static string GetPath(string rawPath, bool isDirectory)
        {
            if (rawPath == null)
                return null;
            var dest = isDirectory
                ? new DirectoryInfo(rawPath)
                : (FileSystemInfo)new FileInfo(rawPath);
            if (!dest.Exists)
            {
                var format = isDirectory
                    ? "Directory not found: {0}."
                    : "File not found: {0}.";
                throw new ArgumentException(string.Format(format, rawPath));
            }
            return dest.FullName;
        }

        private static async Task<CancellationToken> ConvertAsync(Paths paths, Parameters parameters, CancellationToken cancellationToken)
        {
            using (var stream = System.Console.OpenStandardOutput())
            using (var writer = new StreamWriter(stream))
            {
                var runner = new Runner(parameters, cancellationToken, writer);
                await runner.RunAsync((t, p1, p2) => TreeConverter.ConvertAsync(paths, t, p1, p2));
            }
            return cancellationToken;
        }
    }
}
