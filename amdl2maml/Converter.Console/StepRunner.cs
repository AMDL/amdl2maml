using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter.Console
{
    internal sealed class StepRunner : RunnerBase
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
