using System;
using System.Collections.Generic;
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
            return new TreeConverter(paths, cancellationToken, stepProgress).ConvertAsync(progress);
        }

        private readonly Paths paths;
        private readonly CancellationToken cancellationToken;
        private readonly IProgress<Indicator> stepProgress;
        private readonly IEnumerable<Step<Data>> steps;

        private TreeConverter(Paths paths, CancellationToken cancellationToken, IProgress<Indicator> stepProgress)
        {
            this.paths = paths;
            this.cancellationToken = cancellationToken;
            this.stepProgress = stepProgress;
            this.steps = CreateSteps();
        }

        private Task ConvertAsync(IProgress<Indicator> progress)
        {
            return Step<Data>.ExecuteAllAsync(new Data(), steps, progress);
        }

        private IEnumerable<Step<Data>> CreateSteps()
        {
            yield return new Step<Data>("Scanning", ScanAsync);
            yield return new Step<Data>("Indexing", IndexAsync);
            yield return new Step<Data>("Reading", ReadAsync);
            yield return new Step<Data>("Writing", WriteAsync);
        }

        struct Data
        {
            public IEnumerable<TopicData> Topics;
            public IDictionary<string, Guid> Title2Id;
            public IDictionary<string, TopicData> Name2Topic;
        }

        private async Task<Data> ScanAsync(Data data)
        {
            data.Title2Id = await LayoutIndexer.IndexAsync(paths, cancellationToken);
            return data;
        }

        private async Task<Data> IndexAsync(Data data)
        {
            data.Topics = await FolderIndexer.IndexAsync(paths, cancellationToken, stepProgress);
            return data;
        }

        private async Task<Data> ReadAsync(Data data)
        {
            data.Name2Topic = await TopicMatcher.MatchAsync(data.Topics, paths, data.Title2Id, cancellationToken, stepProgress);
            return data;
        }

        private async Task<Data> WriteAsync(Data data)
        {
            await TopicConverter.ConvertAsync(paths, data.Name2Topic, cancellationToken, stepProgress);
            return data;
        }
    }
}
