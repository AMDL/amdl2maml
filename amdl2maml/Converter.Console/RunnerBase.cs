using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter.Console
{
    internal struct Formats
    {
        public string Time;
        public string Duration;
        public string StepPrologue;
        public string StepEpilogue;
        public string StepProgress;
    }

    internal abstract class RunnerBase
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
}
