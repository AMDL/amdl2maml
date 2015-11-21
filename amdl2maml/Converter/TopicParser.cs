using CommonMark;
using CommonMark.Syntax;
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
            settings.AdditionalFeatures = CommonMarkAdditionalFeatures.None
                //| CommonMarkAdditionalFeatures.StrikethroughTilde
                | CommonMarkAdditionalFeatures.SubscriptTilde
                | CommonMarkAdditionalFeatures.SuperscriptCaret
#if TABLES
                | CommonMarkAdditionalFeatures.GithubStyleTables;
            settings.TrackSourcePosition = true;
#endif
                ;
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
            CancellationToken cancellationToken = default(CancellationToken), IProgress<int> progress = null)
        {
            var topics2 = new TopicData[topics.Count()];
            var index = 0;
            if (progress != null)
                progress.Report(index);
            foreach (var topic in topics)
            {
                topics2[index++] = await TopicParser.ParseAsync(topic, srcPath, cancellationToken);
                if (progress != null)
                    progress.Report(index);
            }
            return topics2;
        }

        private static async Task<TopicData> ParseAsync(TopicData topic, string srcPath, CancellationToken cancellationToken)
        {
            var srcFilePath = Path.Combine(srcPath, topic.RelativePath, topic.FileName);
            var file = await FileSystem.Current.GetFileFromPathAsync(srcFilePath, cancellationToken)
                .ConfigureAwait(false);
            using (var stream = await file.OpenAsync(FileAccess.Read, cancellationToken))
            using (var reader = new StreamReader(stream))
            {
                topic.ParserResult = TopicParser.Parse(reader);
                return topic;
            }
        }

        private static TopicParserResult Parse(TextReader reader)
        {
            var root = CommonMarkConverter.Parse(reader, settings);
            return new TopicParserResult(root);
        }
    }
}
