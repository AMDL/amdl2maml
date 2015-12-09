using Ditto.CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter.Console
{
    struct Paths
    {
        public string Source;
        public string Destination;
        public string ContentLayout;
    }

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
                await runner.RunAsync((t, p1, p2) => ConvertAsync(paths, t, p1, p2));
            }
            return cancellationToken;
        }

        private const int StepCount = 6;
        private static int stepIndex = 0;

        private static async Task ConvertAsync(Paths paths, CancellationToken cancellationToken, IProgress<Indicator> progress, IProgress<Indicator> stepProgress)
        {
            var srcPath = paths.Source;
            var destPath = paths.Destination;
            var layoutPath = paths.ContentLayout;

            Report(progress, "Reading");
            var title2id = await LayoutIndexer.IndexAsync(layoutPath, cancellationToken);

            Report(progress, "Indexing");
            var topics = await FolderIndexer.IndexAsync(srcPath, cancellationToken, stepProgress);

            Report(progress, "Parsing");
            topics = await TopicParser.ParseAsync(topics, srcPath, cancellationToken, stepProgress);

            Report(progress, "Updating");
            topics = await UpdateAsync(srcPath, title2id, topics);

            Report(progress, "Mapping");
            var name2topic = await MapAsync(topics);

            Report(progress, "Writing");
            await ConvertAsync(srcPath, destPath, topics, name2topic, cancellationToken, stepProgress);
        }

        private static void Report(IProgress<Indicator> progress, string title)
        {
            Indicator.Report(progress, StepCount, stepIndex++, title);
        }

        private static Task<IEnumerable<TopicData>> UpdateAsync(string srcPath, IDictionary<string, Guid> title2id, IEnumerable<TopicData> topics)
        {
            return Task.Factory.StartNew(() => TopicUpdater.Update(topics, srcPath, title2id));
        }

        private static Task<Dictionary<string, TopicData>> MapAsync(IEnumerable<TopicData> topics)
        {
            return Task.Factory.StartNew(() => topics.ToDictionary(topic => topic.Name, topic => topic));
        }

        private static async Task<object> ConvertAsync(string srcPath, string destPath, IEnumerable<TopicData> topics, Dictionary<string, TopicData> name2topic,
            CancellationToken cancellationToken, IProgress<Indicator> progress)
        {
            await TopicConverter.ConvertAsync(topics, srcPath, destPath, name2topic, cancellationToken, progress);
            return null;
        }
    }
}
