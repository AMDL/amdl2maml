using CommonMark.Syntax;
using System;
using System.Collections.Generic;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// AMDL topic updater.
    /// </summary>
    public static class TopicUpdater
    {
        /// <summary>
        /// Updates the topic with its ID from content layout.
        /// </summary>
        /// <param name="topic">The topic.</param>
        /// <param name="title2id">Gets the topic ID from the topic title.</param>
        /// <returns>Updated topic.</returns>
        public static TopicData Update(TopicData topic, IDictionary<string, Guid> title2id)
        {
            var block = GetHeaderBlock(topic.ParserResult);
            topic.Title = GetTitle(topic, block);
            Guid id;
            if (title2id == null || !title2id.TryGetValue(topic.Title, out id))
                id = Guid.NewGuid();
            topic.Id = id;
            return topic;
        }

        private static string GetTitle(TopicData topic, Block block)
        {
            if (block == null || block.HeaderLevel != 1)
                return topic.Name;
            var title = string.Empty;
            for (var inline = block.InlineContent; inline != null; inline = inline.NextSibling)
                title += inline.LiteralContent;
            return title;
        }

        private static Block GetHeaderBlock(TopicParserResult result)
        {
            Block block;
            for (block = result.Document.FirstChild;
                block != null && block.Tag != BlockTag.AtxHeader && block.Tag != BlockTag.SETextHeader;
                block = block.NextSibling) ;
            return block;
        }
    }
}
