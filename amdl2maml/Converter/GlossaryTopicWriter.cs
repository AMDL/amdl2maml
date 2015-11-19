using CommonMark.Syntax;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter
{
    class GlossaryTopicWriter : TopicWriter
    {
        public GlossaryTopicWriter(TopicData topic, IDictionary<string, TopicData> name2topic)
            : base(topic, name2topic)
        {
        }

        protected override string GetDocElementName()
        {
            return "developerGlossaryDocument";
        }

        internal override async Task WriteStartIntroductionAsync(Block block, XmlWriter writer)
        {
            await writer.WriteStartElementAsync("glossary");
        }

        internal override Task WriteEndIntroductionAsync(XmlWriter writer)
        {
            return Task.FromResult<object>(null);
        }

        internal override async Task WriteRelatedTopicsAsync(Block block, XmlWriter writer)
        {
            await writer.WriteEndElementAsync(); //glossary
        }

        internal override async Task DoWriteStartSeeAlso(XmlWriter writer, int level)
        {
            await writer.WriteEndElementAsync(); //definition
        }

        internal override async Task WriteEndSectionAsync(SectionState state, XmlWriter writer)
        {
            await writer.WriteEndElementAsync(); //glossaryEntry
        }

        internal override async Task<SectionState> DoWriteStartSectionAsync(Block block, string title, XmlWriter writer)
        {
            await writer.WriteStartElementAsync("glossaryEntry");
            await writer.WriteStartElementAsync("terms");
            foreach (var term in title.Split(','))
                await WriteTermAsync(term, writer);
            await writer.WriteEndElementAsync(); //terms
            await writer.WriteStartElementAsync("definition");
            return SectionState.Content;
        }

        private async Task WriteTermAsync(string term, XmlWriter writer)
        {
            var termTrim = term.Trim();
            var termId = termTrim.Replace(' ', '-');
            await writer.WriteStartElementAsync("term");
            await writer.WriteAttributeStringAsync("termId", termId);
            await writer.WriteStringAsync(termTrim);
            await writer.WriteEndElementAsync(); //term
        }

        internal override async Task WriteConceptualLinkAsync(Inline inline, XmlWriter writer)
        {
            if (!IsInSeeAlso)
            {
                await base.WriteConceptualLinkAsync(inline, writer);
                return;
            }

            var termId = GetConceptualLinkTarget(inline);
            if (!termId.StartsWith("#"))
                return;

            await writer.WriteStartElementAsync("relatedEntry");
            await writer.WriteAttributeStringAsync("termId", termId.TrimStart('#'));
            await writer.WriteEndElementAsync(); //relatedEntry
        }

        internal override async Task WriteExternalLinkAsync(Inline inline, XmlWriter writer)
        {
            if (!IsInSeeAlso)
                await base.WriteExternalLinkAsync(inline, writer);
        }
    }
}
