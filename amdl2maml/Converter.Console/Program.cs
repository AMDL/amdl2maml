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

    struct Formats
    {
        public string Time;
        public string Duration;
        public string StepPrologue;
        public string StepEpilogue;
        public string StepProgress;
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

    abstract class RunnerBase
    {
        #region Fields

        private readonly Verbosity verbosity;
        private readonly Formats formats;
        private readonly CancellationToken cancellationToken;
        private readonly TextWriter writer;
        private readonly Progress<Indicator> progress;
        private readonly EventHandler<Indicator> handler;
        private readonly SemaphoreSlim semaphore;

        #endregion Fields

        #region Constructors

        protected RunnerBase(Parameters parameters, TextWriter writer, CancellationToken cancellationToken)
            : this(parameters.Verbosity, GetFormats(parameters), writer, cancellationToken, new Progress<Indicator>(), new SemaphoreSlim(1))
        {
        }

        public RunnerBase(Verbosity verbosity, Formats formats, TextWriter writer, CancellationToken cancellationToken, Progress<Indicator> progress, SemaphoreSlim semaphore)
        {
            this.verbosity = verbosity;
            this.formats = formats;
            this.cancellationToken = cancellationToken;
            this.writer = writer;
            this.progress = progress;
            this.handler = new EventHandler<Indicator>((_, v) => WriteProgress(v));
            this.semaphore = semaphore;
        }

        #endregion Constructors

        #region Properties

        protected Verbosity Verbosity
        {
            get { return verbosity; }
        }

        protected Formats Formats
        {
            get { return formats; }
        }

        protected string TimeFormat
        {
            get { return Formats.Time; }
        }

        protected string DurationFormat
        {
            get { return Formats.Duration; }
        }

        protected TextWriter Writer
        {
            get { return writer; }
        }

        protected CancellationToken CancellationToken
        {
            get { return cancellationToken; }
        }

        protected Progress<Indicator> Progress
        {
            get { return progress; }
        }

        protected EventHandler<Indicator> Handler
        {
            get { return handler; }
        }

        protected SemaphoreSlim Semaphore
        {
            get { return semaphore; }
        }

        #endregion Properties

        #region Methods

        protected abstract Task WriteProgressAsync(Indicator value);

        protected Task WaitAsync()
        {
            return semaphore.WaitAsync(cancellationToken);
        }

        protected int Release()
        {
            return semaphore.Release();
        }

        protected Task WriteAsync(DateTime time)
        {
            return WriteAsync("{0} ", TimeFormat, writer.WriteAsync, time);
        }

        protected Task WriteAsync(string format, params object[] args)
        {
            return WriteAsync(format, TimeFormat, writer.WriteAsync, args);
        }

        protected Task WriteLineAsync(string format, params object[] args)
        {
            return WriteAsync(format, DurationFormat, writer.WriteLineAsync, args);
        }

        protected Task WriteLineAsync()
        {
            return writer.WriteLineAsync();
        }

        private void WriteProgress(Indicator value)
        {
            Task.Run(() => WriteProgressAsync(value));
        }

        #endregion Methods

        #region Static Helpers

        private static Formats GetFormats(Parameters parameters)
        {
            return new Formats
            {
                Time = GetTimeFormat(parameters),
                Duration = GetDurationFormat(parameters),
                StepPrologue = GetStepPrologueFormat(parameters),
                StepEpilogue = GetStepEpilogueFormat(parameters),
                StepProgress = GetStepProgressFormat(parameters),
            };
        }

        private static string GetStepPrologueFormat(Parameters parameters)
        {
            return parameters.Verbosity == Verbosity.Insane
                ? " STEP {1}/{2} {0}"
                : " {0}";
        }

        private static string GetStepEpilogueFormat(Parameters parameters)
        {
            return parameters.Verbosity == Verbosity.Insane
                ? " FINISHED {1}IN {0}"
                : " {1}FINISHED IN {0}";
        }

        private static string GetStepProgressFormat(Parameters parameters)
        {
            return parameters.Verbosity == Verbosity.Insane
                ? "{0}  {1:000.00}%  "
                : "{0}  ";
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

        #endregion Static Helpers
    }

    sealed class Runner : RunnerBase
    {
        #region Fields

        private readonly Progress<Indicator> stepProgress;
        private StepRunner stepRunner;

        #endregion Fields

        #region Constructor

        public Runner(Parameters parameters, CancellationToken cancellationToken, TextWriter writer)
            : base(parameters, writer, cancellationToken)
        {
            this.stepProgress = new Progress<Indicator>();
        }

        #endregion Constructor

        #region Properties

        private Progress<Indicator> StepProgress
        {
            get { return stepProgress; }
        }

        #endregion

        #region RunAsync

        public async Task RunAsync(Func<CancellationToken, IProgress<Indicator>, IProgress<Indicator>, Task> taskFactory)
        {
            var startTime = DateTime.Now;

            await WritePrologueAsync(startTime);

            await taskFactory(CancellationToken, Progress, StepProgress);

            var endTime = DateTime.Now;

            await WriteEpilogueAsync(startTime, endTime);
        }

        #endregion RunAsync

        #region Prologue

        private const string PrologueFormat = " STARTED";

        private async Task WritePrologueAsync(DateTime startTime)
        {
            if (Verbosity < Verbosity.Minimal)
                return;

            await WaitAsync();
            await WriteLineAsync();
            await WriteAsync(startTime);
            await WriteLineAsync(PrologueFormat);
            Release();

            if (Verbosity >= Verbosity.Normal)
                Progress.ProgressChanged += Handler;
        }

        private const string EpilogueFormat = " TOTAL    {0}";

        #endregion Prologue

        #region Epilogue

        private async Task WriteEpilogueAsync(DateTime startTime, DateTime endTime)
        {
            if (Verbosity < Verbosity.Minimal)
                return;

            Progress.ProgressChanged -= Handler;

            await WaitAsync();
            await WriteAsync(endTime);
            await WriteLineAsync(EpilogueFormat, endTime - startTime);
            Release();
        }

        #endregion Epilogue

        #region Progress

        protected override async Task WriteProgressAsync(Indicator value)
        {
            if (stepRunner != null)
            {
                var endTime = DateTime.Now;
                await stepRunner.WriteEpilogueAsync(endTime);
            }

            var startTime = DateTime.Now;
            stepRunner = new StepRunner(value, Verbosity, this.Formats, Writer, CancellationToken, StepProgress, Semaphore);
            await stepRunner.WritePrologueAsync(startTime);
        }

        #endregion Progress
    }

    sealed class StepRunner : RunnerBase
    {
        #region Fields

        private readonly Indicator indicator;
        private readonly string title;
        private DateTime startTime;

        #endregion Fields

        #region Constructor

        public StepRunner(Indicator indicator, Verbosity verbosity, Formats formats, TextWriter writer, CancellationToken cancellationToken, Progress<Indicator> progress, SemaphoreSlim semaphore)
            : base(verbosity, formats, writer, cancellationToken, progress, semaphore)
        {
            this.indicator = indicator;
            this.title = indicator.Name + GetIndent(indicator.Name);
        }

        #endregion Constructor

        #region Properties

        private string Title
        {
            get { return title; }
        }

        private int Index
        {
            get { return indicator.Index; }
        }

        private int Count
        {
            get { return indicator.Count; }
        }

        private string PrologueFormat
        {
            get { return Formats.StepPrologue; }
        }

        private string EpilogueFormat
        {
            get { return Formats.StepEpilogue; }
        }

        private string ProgressFormat
        {
            get { return Formats.StepProgress; }
        }

        #endregion Properties

        #region Prologue

        public async Task WritePrologueAsync(DateTime startTime)
        {
            this.startTime = startTime;

            await WaitAsync();
            await WriteAsync(startTime);
            await WriteLineAsync(string.Format(PrologueFormat, Title.ToUpperInvariant(), Index + 1, Count));
            Release();

            if (Verbosity >= Verbosity.Detailed)
                Progress.ProgressChanged += Handler;
        }

        #endregion Prologue

        #region Epilogue

        public async Task WriteEpilogueAsync(DateTime endTime)
        {
            Progress.ProgressChanged -= Handler;

            await WaitAsync();
            await WriteAsync(endTime);
            await WriteLineAsync(EpilogueFormat, endTime - startTime, Title.ToUpperInvariant());
            await WriteLineAsync();
            Release();
        }

        #endregion Epilogue

        #region Progress

        protected override async Task WriteProgressAsync(Indicator value)
        {
            if (value.Index == 0)
                return;

            var time = DateTime.Now;

            await WaitAsync();
            await WriteAsync(ProgressFormat, time, 100.0 * value.Index / value.Count);
            await WriteAsync(Title);
            await WriteAsync(value.Name);
            await WriteLineAsync();
            Release();
        }

        private string GetIndent(string title)
        {
            return new string(Enumerable.Repeat(' ', MaxLength - title.Length + 1).ToArray());
        }

        private const int MaxLength = 8;

        #endregion Progress
    }
}
