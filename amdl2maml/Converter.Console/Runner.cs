using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter.Console
{
    internal sealed class Runner : RunnerBase
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
}
