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
            var titles = new[]
            {
                "Prepping",
                "Reading",
                "Indexing",
                "Parsing",
                "Updating",
                "Mapping",
                "Writing",
            };

            using (var stream = System.Console.OpenStandardOutput())
            using (var writer = new StreamWriter(stream))
            {
                var runner = new Runner(parameters, cancellationToken, writer, titles);

                var startTime = DateTime.Now;

                await runner.WritePrologueAsync(startTime);

                var paths = await runner.RunAsync((t, _) =>
                    GetPathsAsync(parameters));

                var srcPath = paths.Source;
                var destPath = paths.Destination;
                var layoutPath = paths.ContentLayout;

                var title2id = await runner.RunAsync((t, _) =>
                    LayoutIndexer.IndexAsync(layoutPath, t));

                var topics = await runner.RunAsync((t, p) =>
                    FolderIndexer.IndexAsync(srcPath, t, p));

                topics = await runner.RunAsync((t, p) =>
                    TopicParser.ParseAsync(topics, srcPath, t, p));

                topics = await runner.RunAsync((t, _) =>
                    UpdateAsync(srcPath, title2id, topics));

                var name2topic = await runner.RunAsync((t, _) =>
                    MapAsync(topics));

                await runner.RunAsync((t, p) =>
                    ConvertAsync(srcPath, destPath, topics, name2topic, t, p));

                var endTime = DateTime.Now;

                await runner.WriteEpilogueAsync(startTime, endTime);
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
    }

    sealed class Runner
    {
        #region Fields

        private readonly Verbosity verbosity;
        private readonly string timeFormat;
        private readonly string durationFormat;
        private readonly string stepPrologueFormat;
        private readonly string stepEpilogueFormat;
        private readonly CancellationToken cancellationToken;
        private readonly TextWriter writer;
        private readonly string[] titles;
        private readonly int maxLength;
        private int index;
        private readonly SemaphoreSlim _semaphore;

        #endregion Fields

        #region Constructor

        public Runner(Parameters parameters, CancellationToken cancellationToken, TextWriter writer, string[] titles)
        {
            this.verbosity = parameters.Verbosity;
            this.timeFormat = GetTimeFormat(parameters);
            this.durationFormat = GetDurationFormat(parameters);
            this.stepPrologueFormat = GetStepPrologueFormat(parameters);
            this.stepEpilogueFormat = GetStepEpilogueFormat(parameters);
            this.cancellationToken = cancellationToken;
            this.writer = writer;
            this.titles = titles;
            this.maxLength = titles.Max(s => s.Length);
            this.index = 0;
            this._semaphore = new SemaphoreSlim(1);
        }

        #endregion Constructor

        #region Properties

        private Verbosity Verbosity
        {
            get { return verbosity; }
        }

        private string TimeFormat
        {
            get { return timeFormat; }
        }

        private string DurationFormat
        {
            get { return durationFormat; }
        }

        private string StepPrologueFormat
        {
            get { return stepPrologueFormat; }
        }

        private string StepEpilogueFormat
        {
            get { return stepEpilogueFormat; }
        }

        #endregion Properties

        #region RunAsync

        public async Task<TResult> RunAsync<TResult>(Func<CancellationToken, IProgress<Indicator>, Task<TResult>> taskFactory)
        {
            var title = titles[index++];
            var indent = GetIndent(title);
            var titleUpper = title.ToUpperInvariant() + indent;

            var stepStartTime = DateTime.Now;

            await WriteStepPrologueAsync(titleUpper, index, titles.Length, stepStartTime);

            Progress<Indicator> progress;
            EventHandler<Indicator> handler;
            StartProgress(title + indent, out progress, out handler);

            var result = await taskFactory(cancellationToken, progress);

            var stepEndTime = DateTime.Now;

            StopProgress(progress, handler);

            await WriteStepEpilogueAsync(titleUpper, stepStartTime, stepEndTime);

            return result;
        }

        #endregion RunAsync

        #region Progress

        private void StartProgress(string title, out Progress<Indicator> progress, out EventHandler<Indicator> handler)
        {
            if (Verbosity < Verbosity.Detailed)
            {
                handler = null;
                progress = null;
                return;
            }
            handler = new EventHandler<Indicator>((_, value) => WriteProgress(title, value));
            progress = new Progress<Indicator>();
            progress.ProgressChanged += handler;
        }

        private void StopProgress(Progress<Indicator> progress, EventHandler<Indicator> handler)
        {
            if (progress != null)
                progress.ProgressChanged -= handler;
        }

        private void WriteProgress(string title, Indicator value)
        {
            if (value.Index == 0)
                return;
            var time = DateTime.Now;
            _semaphore.Wait(cancellationToken);
            if (Verbosity == Verbosity.Insane)
                Write("{0}  {1:000.00}%  ", time, 100.0 * value.Index / value.Count);
            else
                Write("{0}  ", time);
            Write(title);
            Write(value.Name);
            WriteLine();
            _semaphore.Release();
        }

        private string GetIndent(string title)
        {
            return new string(Enumerable.Repeat(' ', maxLength - title.Length + 1).ToArray());
        }

        #endregion Progress

        #region Prologue/Epilogue

        private const string PrologueFormat = " STARTED";

        public async Task WritePrologueAsync(DateTime startTime)
        {
            if (Verbosity < Verbosity.Minimal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await WriteLineAsync();
            await WriteAsync(startTime);
            await WriteLineAsync(PrologueFormat);
            _semaphore.Release();
        }

        private const string EpilogueFormat = " TOTAL    {0}";

        public async Task WriteEpilogueAsync(DateTime startTime, DateTime endTime)
        {
            if (Verbosity < Verbosity.Minimal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await WriteAsync(endTime);
            await WriteLineAsync(EpilogueFormat, endTime - startTime);
            _semaphore.Release();
        }

        #endregion Prologue/Epilogue

        #region Step

        private const string StepPrologueFormat1 = " STEP {1}/{2} {0}";
        private const string StepPrologueFormat2 = " {0}";

        private async Task WriteStepPrologueAsync(string title, int index, int count, DateTime stepStartTime)
        {
            if (Verbosity < Verbosity.Normal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await WriteAsync(stepStartTime);
            await WriteLineAsync(string.Format(StepPrologueFormat, title, index, count));
            _semaphore.Release();
        }

        private const string StepEpilogueFormat1 = " FINISHED {1}IN {0}";
        private const string StepEpilogueFormat2 = " {1}FINISHED IN {0}";

        private async Task WriteStepEpilogueAsync(string title, DateTime stepStartTime, DateTime stepEndTime)
        {
            if (Verbosity < Verbosity.Normal)
                return;
            await _semaphore.WaitAsync(cancellationToken);
            await WriteAsync(stepEndTime);
            await WriteLineAsync(StepEpilogueFormat, stepEndTime - stepStartTime, title);
            await WriteLineAsync();
            _semaphore.Release();
        }

        #endregion Step

        #region Write

        private void Write(string format, params object[] args)
        {
            Write(format, TimeFormat, writer.Write, args);
        }

        private void Write(string value)
        {
            writer.Write(value);
        }

        private void WriteLine()
        {
            writer.WriteLine();
        }

        #endregion Write

        #region WriteAsync

        private Task WriteAsync(DateTime time)
        {
            return WriteAsync("{0} ", TimeFormat, writer.WriteAsync, time);
        }

        private Task WriteLineAsync(string format, params object[] args)
        {
            return WriteAsync(format, DurationFormat, writer.WriteLineAsync, args);
        }

        private Task WriteLineAsync(string value)
        {
            return writer.WriteLineAsync(value);
        }

        private Task WriteLineAsync()
        {
            return writer.WriteLineAsync();
        }

        #endregion WriteAsync

        #region Static Helpers

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

        private static string GetStepPrologueFormat(Parameters parameters)
        {
            return parameters.Verbosity == Verbosity.Insane
                ? StepPrologueFormat1
                : StepPrologueFormat2;
        }

        private static string GetStepEpilogueFormat(Parameters parameters)
        {
            return parameters.Verbosity == Verbosity.Insane
                ? StepEpilogueFormat1
                : StepEpilogueFormat2;
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

        #endregion
    }
}
