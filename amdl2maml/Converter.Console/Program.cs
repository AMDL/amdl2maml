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
        private static SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        static void Main(string[] args)
        {
            var parameters = ParseParameters(args);
            if (parameters == null)
            {
                CommandLine<Parameters>.WriteUsage(System.Console.Out);
                return;
            }
            Convert(parameters);
        }

        private static Parameters ParseParameters(string[] args)
        {
            try
            {
                Parameters parameters = null;
                var results = CommandLine<Parameters>.Parse(args, out parameters);
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
                var task = ConvertAsync(parameters, CancellationToken.None);
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
                    ? "Directory not found: {0}"
                    : "File not found: {0}";
                throw new ArgumentException(string.Format(format, rawPath));
            }
            return dest.FullName;
        }

        private static async Task<CancellationToken> ConvertAsync(Parameters parameters, CancellationToken cancellationToken)
        {
            using (var stream = System.Console.OpenStandardOutput())
            using (var writer = new StreamWriter(stream))
            {
                var startTime = DateTime.Now;

                await WritePrologueAsync(startTime, parameters, cancellationToken, writer);

                var paths = await RunAsync((t, _) =>
                    GetPathsAsync(parameters),
                    parameters, cancellationToken, writer, "PREPPING");

                var srcPath = paths.Source;
                var destPath = paths.Destination;
                var layoutPath = paths.ContentLayout;

                var title2id = await RunAsync((t, p) =>
                    LayoutIndexer.IndexAsync(layoutPath, t, p),
                    parameters, cancellationToken, writer, "READING ");

                var topics = await RunAsync((t, p) =>
                    FolderIndexer.IndexAsync(srcPath, t, p),
                    parameters, cancellationToken, writer, "INDEXING");

                topics = await RunAsync((t, p) =>
                    TopicParser.ParseAsync(topics, srcPath, t, p),
                    parameters, cancellationToken, writer, "PARSING ");

                topics = await RunAsync((t, _) =>
                    UpdateAsync(srcPath, title2id, topics),
                    parameters, cancellationToken, writer, "UPDATING");

                var name2topic = await RunAsync((t, _) =>
                    MapAsync(topics),
                    parameters, cancellationToken, writer, "MAPPING ");

                await RunAsync((t, p) =>
                    ConvertAsync(srcPath, destPath, topics, name2topic, t, p),
                    parameters, cancellationToken, writer, "WRITING ");

                var endTime = DateTime.Now;

                await WriteEpilogueAsync(startTime, endTime, parameters, cancellationToken, writer);
            }
            return cancellationToken;
        }

        private static Task<Paths> GetPathsAsync(Parameters parameters)
        {
            return Task.Factory.StartNew(() => GetPaths(parameters));
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

        private static async Task<TResult> RunAsync<TResult>(Func<CancellationToken, IProgress<Indicator>, Task<TResult>> taskFactory,
            Parameters parameters, CancellationToken cancellationToken, TextWriter writer, string title)
        {
            var stepStartTime = DateTime.Now;

            await WriteStepPrologueAsync(title, stepStartTime, parameters, cancellationToken, writer);

            Progress<Indicator> progress;
            EventHandler<Indicator> handler;
            StartProgress(title, parameters, cancellationToken, writer, out progress, out handler);

            var result = await taskFactory(cancellationToken, progress);

            var stepEndTime = DateTime.Now;

            StopProgress(progress, handler);

            await WriteStepEpilogueAsync(title, stepStartTime, stepEndTime, parameters, cancellationToken, writer);

            return result;
        }

        #region Progress

        private static void StartProgress(string title, Parameters parameters, CancellationToken cancellationToken, TextWriter writer, out Progress<Indicator> progress, out EventHandler<Indicator> handler)
        {
            if (parameters.Verbosity < Verbosity.Detailed)
            {
                handler = null;
                progress = null;
                return;
            }
            handler = new EventHandler<Indicator>((_, v) => WriteProgress(title, v, parameters, cancellationToken, writer));
            progress = new Progress<Indicator>();
            progress.ProgressChanged += handler;
        }

        private static void StopProgress(Progress<Indicator> progress, EventHandler<Indicator> handler)
        {
            if (progress != null)
                progress.ProgressChanged -= handler;
        }

        #endregion Progress

        private static void WriteProgress(string title, Indicator value, Parameters parameters, CancellationToken cancellationToken, TextWriter writer)
        {
            _semaphore.Wait(cancellationToken);
            Write(DateTime.Now, parameters, writer);
            writer.Write(" {0:000.00}%  ", 100.0 * value.Index / value.Count);
            writer.Write(title);
            writer.Write(' ');
            writer.Write(value.Name);
            writer.WriteLine();
            _semaphore.Release();
        }

        private const string PrologueFormat = " STARTED";

        private static async Task WritePrologueAsync(DateTime startTime, Parameters parameters, CancellationToken cancellationToken, TextWriter writer)
        {
            if (parameters.Verbosity < Verbosity.Minimal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await writer.WriteLineAsync();
            await WriteAsync(startTime, parameters, writer);
            await writer.WriteLineAsync(PrologueFormat);
            _semaphore.Release();
        }

        private const string EpilogueFormat = "TOTAL    {0}";

        private static async Task WriteEpilogueAsync(DateTime startTime, DateTime endTime, Parameters parameters, CancellationToken cancellationToken, TextWriter writer)
        {
            if (parameters.Verbosity < Verbosity.Minimal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await WriteAsync(endTime, parameters, writer);
            await WriteLineAsync(EpilogueFormat, parameters, writer, endTime - startTime);
            _semaphore.Release();
        }

        private const string StepPrologueFormat = " STARTED  {0}";

        private static async Task WriteStepPrologueAsync(string stepTitle, DateTime stepStartTime, Parameters parameters, CancellationToken cancellationToken, TextWriter writer)
        {
            if (parameters.Verbosity < Verbosity.Normal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await WriteAsync(stepStartTime, parameters, writer);
            await writer.WriteLineAsync(string.Format(StepPrologueFormat, stepTitle));
            _semaphore.Release();
        }

        private const string StepEpilogueFormat = " FINISHED {1} IN {0}";

        private static async Task WriteStepEpilogueAsync(string stepTitle, DateTime stepStartTime, DateTime stepEndTime, Parameters parameters, CancellationToken cancellationToken, TextWriter writer)
        {
            if (parameters.Verbosity < Verbosity.Normal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await WriteAsync(stepEndTime, parameters, writer);
            await WriteLineAsync(StepEpilogueFormat, parameters, writer, stepEndTime - stepStartTime, stepTitle);
            await writer.WriteLineAsync();
            _semaphore.Release();
        }

        private static void Write(DateTime time, Parameters parameters, TextWriter writer)
        {
            Write("{0} ", GetTimeFormat(parameters), writer.Write, time);
        }

        private static Task WriteAsync(DateTime time, Parameters parameters, TextWriter writer)
        {
            return WriteAsync("{0} ", GetTimeFormat(parameters), writer.WriteAsync, time);
        }

        private static Task WriteLineAsync(string format, Parameters parameters, TextWriter writer, params object[] args)
        {
            return WriteAsync(format, GetDurationFormat(parameters), writer.WriteLineAsync, args);
        }

        private static string GetTimeFormat(Parameters parameters)
        {
            return Reformat(parameters.TimeFormat);
        }

        private static string GetDurationFormat(Parameters parameters)
        {
            var init = parameters.DurationFormat.Trim();
            var split = parameters.DurationFormat.Split(':', '.', ',', '-', '/', ' ');
            int index = 0;
            string durationFormat = null;
            foreach (var s in split)
            {
                if (!string.IsNullOrEmpty(s))
                {
                    durationFormat += Reformat(s);
                    index += s.Length;
                }
                if (index < init.Length - 1)
                    durationFormat += init[index++];
            }
            return durationFormat;
        }

        private static void Write(string format, string reFormat, Action<string> write, params object[] args)
        {
            write(Format(format, reFormat, args));
        }

        private static Task WriteAsync(string format, string reFormat, Func<string, Task> write, params object[] args)
        {
            return write(Format(format, reFormat, args));
        }

        private static string Format(string format, string reFormat, object[] args)
        {
            format = format.Replace("{0}", reFormat);
            return string.Format(format, args);
        }

        private static string Reformat(string s)
        {
            return string.Format("{{0:{0}}}", s);
        }
    }
}
