using CommonMark.Syntax;
using System;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// AMDL topic parser result.
    /// </summary>
    public class TopicParserResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TopicParserResult"/> class.
        /// </summary>
        /// <param name="doc">Document block.</param>
        public TopicParserResult(Block doc)
        {
            if (doc == null)
                throw new ArgumentNullException("doc");
            if (doc.Tag != BlockTag.Document)
                throw new InvalidOperationException("Unexpected block tag: " + doc.Tag);
            Document = doc;
        }

        /// <summary>
        /// Gets the document block.
        /// </summary>
        /// <value>Document block.</value>
        public Block Document { get; private set; }
    }
}
