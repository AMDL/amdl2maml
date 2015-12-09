using CommonMark;
using PCLStorage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// AMDL topic parser.
    /// </summary>
    public class TopicParser
    {
        private static CommonMarkSettings settings;

        static TopicParser()
        {
            settings = CommonMarkSettings.Default.Clone();
            settings.AdditionalFeatures = CommonMarkAdditionalFeatures.All & ~CommonMarkAdditionalFeatures.StrikethroughTilde;
        }

        /// <summary>
        /// Parses the topics.
        /// </summary>
        /// <param name="topics">The topics.</param>
        /// <param name="srcPath">Source base path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Progress indicator.</param>
        /// <returns>Parsed topics.</returns>
        public static async Task<IEnumerable<TopicData>> ParseAsync(IEnumerable<TopicData> topics, string srcPath,
            CancellationToken cancellationToken = default(CancellationToken), IProgress<Indicator> progress = null)
        {
            var topicsArray = topics.ToArray();
            var count = topicsArray.Length;
            Indicator.Report(progress, count);
            var tasks = Enumerable.Range(0, count).Select(index =>
                ParseAsync(topicsArray[index], srcPath, cancellationToken, progress, index, count));
            await Task.WhenAll(tasks);
            return topics;
        }

        /// <summary>
        /// Parses the topic.
        /// </summary>
        /// <param name="topic">The topic.</param>
        /// <param name="srcPath">Source base path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Progress indicator.</param>
        /// <param name="index">Index.</param>
        /// <param name="count">Count.</param>
        /// <returns>Parsed topic.</returns>
        public static async Task<TopicParserResult> ParseAsync(TopicData topic, string srcPath, CancellationToken cancellationToken,
            IProgress<Indicator> progress = null, int index = 0, int count = 0)
        {
            var srcFilePath = Path.Combine(srcPath, topic.RelativePath, topic.FileName);
            var file = await FileSystem.Current.GetFileFromPathAsync(srcFilePath, cancellationToken)
                .ConfigureAwait(false);
            using (var stream = await file.OpenAsync(FileAccess.Read, cancellationToken))
            using (var reader = new StreamReader(stream))
            {
                var parserResult = topic.ParserResult = TopicParser.Parse(reader);
                Indicator.Report(progress, count, index + 1, () => Path.Combine(topic.RelativePath, topic.Name));
                return parserResult;
            }
        }

        private static TopicParserResult Parse(TextReader reader)
        {
            var root = CommonMarkConverter.Parse(reader, settings);
            return new TopicParserResult(root);
        }
    }
}
