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
    /// Topic updater.
    /// </summary>
    public static class TopicUpdater
    {
        /// <summary>
        /// Updates the topics with their IDs from content layout.
        /// </summary>
        /// <param name="topics">The topics.</param>
        /// <param name="srcPath">Source base path.</param>
        /// <param name="title2id">Gets the topic ID from the topic title.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The topics.</returns>
        public static async Task<IEnumerable<TopicData>> UpdateAsync(IEnumerable<TopicData> topics, string srcPath, IDictionary<string, Guid> title2id, CancellationToken cancellationToken)
        {
            var blocks = await Task.WhenAll(topics.Select(topic => GetHeaderBlockAsync(topic, srcPath, title2id, cancellationToken)));
            return topics.Zip(blocks, (topic, block) => Update(topic, block, title2id));
        }

        private static async Task<Block> GetHeaderBlockAsync(TopicData topic, string srcPath, IDictionary<string, Guid> title2id, CancellationToken cancellationToken)
        {
            var srcFilePath = Path.Combine(srcPath, topic.RelativePath, topic.FileName);
            var file = await FileSystem.Current.GetFileFromPathAsync(srcFilePath, cancellationToken);
            using (var stream = await file.OpenAsync(FileAccess.Read, cancellationToken))
            using (var reader = new StreamReader(stream))
            {
                return GetHeaderBlock(reader);
            }
        }

        private static TopicData Update(TopicData topic, Block block, IDictionary<string, Guid> title2id)
        {
            if (block == null || block.HeaderLevel != 1)
                throw new InvalidOperationException("Missing header");
            var title = string.Empty;
            for (var inline = block.InlineContent; inline != null; inline = inline.NextSibling)
                title += inline.LiteralContent;
            Guid id;
            if (!title2id.TryGetValue(title, out id))
                id = Guid.NewGuid();
            topic.Id = id;
            return topic;
        }

        private static Block GetHeaderBlock(StreamReader reader)
        {
            var block = CommonMarkConverter.Parse(reader);
            if (block.Tag != BlockTag.Document)
                throw new InvalidOperationException("Unexpected block tag: " + block.Tag);

            for (block = block.FirstChild;
                block != null && block.Tag != BlockTag.AtxHeader && block.Tag != BlockTag.SETextHeader;
                block = block.NextSibling) ;
            return block;
        }

        //private static Block GetHeaderBlock2(StreamReader reader, Block block)
        //{
        //    string line;
        //    while ((line = reader.ReadLine()) != null)
        //    {
        //        block = CommonMarkConverter.Parse(line);
        //        for (block = block.FirstChild;
        //            block != null && block.Tag != BlockTag.AtxHeader && block.Tag != BlockTag.SETextHeader;
        //            block = block.NextSibling) ;
        //        if (block != null)
        //            break;
        //    }
        //    return block;
        //}
    }
}
