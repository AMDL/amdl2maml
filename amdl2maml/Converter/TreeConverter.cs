using System;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// AMDL tree converter paths.
    /// </summary>
    public struct Paths
    {
        /// <summary>
        /// Source folder path.
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Destination folder path.
        /// </summary>
        public string Destination { get; set; }

        /// <summary>
        /// Content layout file path.
        /// </summary>
        public string ContentLayout { get; set; }
    }

    /// <summary>
    /// AMDL tree converter.
    /// </summary>
    public class TreeConverter
    {
        /// <summary>
        /// Converts the tree to MAML.
        /// </summary>
        /// <param name="paths">Paths.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Progress indicator.</param>
        /// <param name="stepProgress">Step progress indicator.</param>
        /// <returns>Asynchronous task.</returns>
        public static Task ConvertAsync(Paths paths, CancellationToken cancellationToken, IProgress<Indicator> progress, IProgress<Indicator> stepProgress)
        {
            return new TreeConverter(paths, cancellationToken, progress, stepProgress).ConvertAsync();
        }

        private readonly Paths paths;
        private readonly CancellationToken cancellationToken;
        private readonly IProgress<Indicator> progress;
        private readonly IProgress<Indicator> stepProgress;
        private int index;

        private TreeConverter(Paths paths, CancellationToken cancellationToken, IProgress<Indicator> progress, IProgress<Indicator> stepProgress)
        {
            this.paths = paths;
            this.cancellationToken = cancellationToken;
            this.progress = progress;
            this.stepProgress = stepProgress;
        }

        private const int StepCount = 4;

        private async Task ConvertAsync()
        {
            Report("Reading");
            var title2id = await LayoutIndexer.IndexAsync(paths, cancellationToken);

            Report("Indexing");
            var topics = await FolderIndexer.IndexAsync(paths, cancellationToken, stepProgress);

            Report("Matching");
            var name2topic = await TopicMatcher.MatchAsync(topics, paths, title2id, cancellationToken, stepProgress);

            Report("Writing");
            await TopicConverter.ConvertAsync(paths, name2topic, cancellationToken, stepProgress);
        }

        private void Report(string title)
        {
            Indicator.Report(progress, StepCount, index++, title);
        }
    }
}
