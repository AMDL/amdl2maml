using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter.Console
{
    class Program
    {
        private const string TimeFormat = "{0:yyyy-MM-dd HH:mm:ss.fff} ";
        private const string TimeOffsetFormat = "{0:hh}:{0:mm}:{0:ss}.{0:fff}";

        private static SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        static void Main(string[] args)
        {
            var verbosity = Verbosity.Minimal;

            if (args.Length < 3)
            {
                WriteUsage(verbosity);
                return;
            }

            var srcPath     = args[0].Trim();
            var destPathRaw = args[1].Trim();
            var layoutPath  = args[2].Trim();

            if (args.Length > 3 && !Enum.TryParse<Verbosity>(args[3].Trim(), true, out verbosity))
                throw new InvalidOperationException("Invalid verbosity value: " + args[3].Trim());

            var destDir = new DirectoryInfo(destPathRaw);
            var destPath = destDir.FullName;

            Convert(layoutPath, srcPath, destPath, verbosity);
        }

        private static void Convert(string layoutPath, string srcPath, string destPath, Verbosity verbosity)
        {
            var task = ConvertAsync(layoutPath, srcPath, destPath, verbosity, CancellationToken.None);
            task.GetAwaiter().GetResult();
        }

        private static void WriteUsage(Verbosity verbosity)
        {
            System.Console.WriteLine("Usage:");
            System.Console.WriteLine();

            System.Console.Write(AppDomain.CurrentDomain.FriendlyName);
            System.Console.WriteLine(" srcPath destPath layoutPath [verbosity]");
            System.Console.WriteLine();

            System.Console.Write("\tsrcPath    ");
            System.Console.WriteLine("Source folder path");

            System.Console.Write("\tdestPath   ");
            System.Console.WriteLine("Destination folder path");

            System.Console.Write("\tlayoutPath ");
            System.Console.WriteLine("Content layout file path");

            var verbosityNames = string.Join(", ", Enum.GetNames(typeof(Verbosity)));
            System.Console.Write("\tverbosity  ");
            System.Console.WriteLine("{0} (the default is {1})", verbosityNames, verbosity);
            System.Console.WriteLine();
        }

        private static async Task ConvertAsync(string layoutPath, string srcPath, string destPath, Verbosity verbosity, CancellationToken cancellationToken)
        {
            using (var stream = System.Console.OpenStandardOutput())
            using (var writer = new StreamWriter(stream))
            {
                var startTime = DateTime.Now;

                await WritePrologueAsync(startTime, verbosity, cancellationToken, writer);

                var title2id = await RunAsync((t, _) =>
                    LayoutIndexer.IndexAsync(layoutPath, t),
                    verbosity, cancellationToken, writer, "READING ");

                var topics = await RunAsync((t, _) =>
                    FolderIndexer.IndexAsync(srcPath, t),
                    verbosity, cancellationToken, writer, "INDEXING");

                topics = await RunAsync((t, p) =>
                    TopicParser.ParseAsync(topics, srcPath, t, p),
                    verbosity, cancellationToken, writer, "PARSING ", "Parsing  {0}");

                topics = await RunAsync((t, _) =>
                    UpdateAsync(srcPath, title2id, topics),
                    verbosity, cancellationToken, writer, "UPDATING");

                var name2topic = await RunAsync((t, _) =>
                    MapAsync(topics),
                    verbosity, cancellationToken, writer, "MAPPING ");

                await RunAsync((t, p) =>
                    ConvertAsync(srcPath, destPath, topics, name2topic, t, p),
                    verbosity, cancellationToken, writer, "WRITING ", "Writing  {0}");

                var endTime = DateTime.Now;

                await WriteEpilogueAsync(startTime, endTime, verbosity, cancellationToken, writer);
            }
        }

        private static async Task<object> ConvertAsync(string srcPath, string destPath, IEnumerable<TopicData> topics, Dictionary<string, TopicData> name2topic, CancellationToken cancellationToken, IProgress<string> progress)
        {
            await TopicConverter.ConvertAsync(topics, srcPath, destPath, name2topic, cancellationToken, progress);
            return null;
        }

        private static Task<IEnumerable<TopicData>> UpdateAsync(string srcPath, IDictionary<string, Guid> title2id, IEnumerable<TopicData> topics)
        {
            return Task.Factory.StartNew(() => TopicUpdater.Update(topics, srcPath, title2id));
        }

        private static Task<Dictionary<string, TopicData>> MapAsync(IEnumerable<TopicData> topics)
        {
            return Task.Factory.StartNew(() => topics.ToDictionary(topic => topic.Name, topic => topic));
        }

        private static async Task<TResult> RunAsync<TResult>(Func<CancellationToken, IProgress<string>, Task<TResult>> taskFactory,
            Verbosity verbosity, CancellationToken cancellationToken, TextWriter writer, string title, string format = null)
        {
            var stepStartTime = DateTime.Now;

            await WriteStepPrologueAsync(title, stepStartTime, verbosity, cancellationToken, writer);

            Progress<string> progress;
            EventHandler<string> handler;
            StartProgress(format, verbosity, cancellationToken, writer, out progress, out handler);

            var result = await taskFactory(cancellationToken, progress);

            var stepEndTime = DateTime.Now;

            StopProgress(progress, handler);

            await WriteStepEpilogueAsync(title, stepStartTime, stepEndTime, verbosity, cancellationToken, writer);

            return result;
        }

        #region Progress

        private static void StartProgress(string format, Verbosity verbosity, CancellationToken cancellationToken, TextWriter writer, out Progress<string> progress, out EventHandler<string> handler)
        {
            if (format == null || verbosity < Verbosity.Detailed)
            {
                handler = null;
                progress = null;
                return;
            }
            handler = new EventHandler<string>((_, v) => WriteProgress(format, v, cancellationToken, writer));
            progress = new Progress<string>();
            progress.ProgressChanged += handler;
        }

        private static void StopProgress(Progress<string> progress, EventHandler<string> handler)
        {
            if (progress != null)
                progress.ProgressChanged -= handler;
        }

        #endregion Progress

        private static void WriteProgress(string format, string value, CancellationToken cancellationToken, TextWriter writer)
        {
            _semaphore.Wait(cancellationToken);
            writer.Write(TimeFormat, DateTime.Now);
            writer.WriteLine(format, value);
            _semaphore.Release();
        }

        private const string PrologueFormat = "STARTED";

        private static async Task WritePrologueAsync(DateTime startTime, Verbosity verbosity, CancellationToken cancellationToken, TextWriter writer)
        {
            if (verbosity < Verbosity.Minimal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await writer.WriteLineAsync();
            await writer.WriteAsync(string.Format(TimeFormat, startTime));
            await writer.WriteLineAsync(PrologueFormat);
            _semaphore.Release();
        }

        private const string EpilogueFormat = "TOTAL    " + TimeOffsetFormat;

        private static async Task WriteEpilogueAsync(DateTime startTime, DateTime endTime, Verbosity verbosity, CancellationToken cancellationToken, TextWriter writer)
        {
            if (verbosity < Verbosity.Minimal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await writer.WriteLineAsync();
            await writer.WriteAsync(string.Format(TimeFormat, endTime));
            await writer.WriteAsync(string.Format(EpilogueFormat, endTime - startTime));
            await writer.WriteLineAsync();
            _semaphore.Release();
        }

        private const string StepPrologueFormat = "STARTED  {0}";

        private static async Task WriteStepPrologueAsync(string stepTitle, DateTime stepStartTime, Verbosity verbosity, CancellationToken cancellationToken, TextWriter writer)
        {
            if (verbosity < Verbosity.Normal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await writer.WriteAsync(string.Format(TimeFormat, stepStartTime));
            await writer.WriteLineAsync(string.Format(StepPrologueFormat, stepTitle));
            _semaphore.Release();
        }

        private const string StepEpilogueFormat = "FINISHED {1} IN " + TimeOffsetFormat;

        private static async Task WriteStepEpilogueAsync(string stepTitle, DateTime stepStartTime, DateTime stepEndTime, Verbosity verbosity, CancellationToken cancellationToken, TextWriter writer)
        {
            if (verbosity < Verbosity.Normal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await writer.WriteAsync(string.Format(TimeFormat, stepEndTime));
            await writer.WriteLineAsync(string.Format(StepEpilogueFormat, stepEndTime - stepStartTime, stepTitle));
            await writer.WriteLineAsync();
            _semaphore.Release();
        }
    }
}
