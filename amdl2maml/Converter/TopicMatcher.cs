using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// AMDL topic matcher.
    /// </summary>
    public static class TopicMatcher
    {
        /// <summary>
        /// Matches the topics to their IDs from content layout and creates a mapping from topic name to topic.
        /// </summary>
        /// <param name="topics">The topics.</param>
        /// <param name="srcPath">Source base path.</param>
        /// <param name="title2id">Gets the topic ID from the topic title.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Progress indicator.</param>
        /// <returns>Matched topic mapping.</returns>
        public static async Task<Dictionary<string, TopicData>> MatchAsync(IEnumerable<TopicData> topics, string srcPath, IDictionary<string, Guid> title2id,
            CancellationToken cancellationToken = default(CancellationToken), IProgress<Indicator> progress = null)
        {
            var topicsArray = topics.ToArray();
            var count = topicsArray.Length;
            Indicator.Report(progress, count);
            var tasks = Enumerable.Range(0, count).Select(index =>
                MatchAsync(topicsArray[index], srcPath, title2id, cancellationToken, progress, count, index));
            topics = await Task.WhenAll(tasks);
            return topics.ToDictionary(topic => topic.Name, topic => topic);
        }

        private static async Task<TopicData> MatchAsync(TopicData topic, string srcPath, IDictionary<string, Guid> title2id, CancellationToken cancellationToken,
            IProgress<Indicator> progress, int count, int index)
        {
            topic = await TopicParser.ParseAsync(topic, srcPath, cancellationToken, progress, index, count);
            return TopicUpdater.Update(topic, title2id);
        }
    }
}
