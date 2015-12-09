using System;
using System.Collections.Generic;
using System.Linq;
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

        private const int StepCount = 6;

        private async Task ConvertAsync()
        {
            var srcPath = paths.Source;
            var destPath = paths.Destination;
            var layoutPath = paths.ContentLayout;

            Report("Reading");
            var title2id = await LayoutIndexer.IndexAsync(layoutPath, cancellationToken);

            Report("Indexing");
            var topics = await FolderIndexer.IndexAsync(srcPath, cancellationToken, stepProgress);

            Report("Parsing");
            topics = await TopicParser.ParseAsync(topics, srcPath, cancellationToken, stepProgress);

            Report("Updating");
            topics = await UpdateAsync(srcPath, title2id, topics);

            Report("Mapping");
            var name2topic = await MapAsync(topics);

            Report("Writing");
            await ConvertAsync(srcPath, destPath, topics, name2topic);
        }

        private void Report(string title)
        {
            Indicator.Report(progress, StepCount, index++, title);
        }

        private Task<IEnumerable<TopicData>> UpdateAsync(string srcPath, IDictionary<string, Guid> title2id, IEnumerable<TopicData> topics)
        {
            return Task.Factory.StartNew(() => TopicUpdater.Update(topics, srcPath, title2id));
        }

        private Task<Dictionary<string, TopicData>> MapAsync(IEnumerable<TopicData> topics)
        {
            return Task.Factory.StartNew(() => topics.ToDictionary(topic => topic.Name, topic => topic));
        }

        private Task ConvertAsync(string srcPath, string destPath, IEnumerable<TopicData> topics, Dictionary<string, TopicData> name2topic)
        {
            return TopicConverter.ConvertAsync(topics, srcPath, destPath, name2topic, cancellationToken, stepProgress);
        }
    }
}
