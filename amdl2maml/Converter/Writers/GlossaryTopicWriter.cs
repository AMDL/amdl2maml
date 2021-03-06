﻿using CommonMark.Syntax;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    class GlossaryTopicWriter : TopicWriter
    {
        public GlossaryTopicWriter(TopicData topic, TopicParserResult parserResult, IDictionary<string, TopicData> name2topic, XmlWriter writer)
            : base(topic, parserResult, name2topic, writer)
        {
        }

        internal override string GetDocElementName()
        {
            return "developerGlossaryDocument";
        }

        internal override async Task WriteStartIntroductionAsync(Block block)
        {
            await WriteStartElementAsync("glossary");
        }

        internal override Task WriteEndIntroductionAsync()
        {
            return Task.FromResult<object>(null);
        }

        internal override async Task WriteRelatedTopicsAsync(Block block)
        {
            await WriteEndElementAsync(); //glossary
        }

        internal override async Task DoWriteStartSeeAlso(int level)
        {
            await WriteEndElementAsync(); //definition
        }

        internal override async Task WriteEndSectionAsync(SectionState state)
        {
            await WriteEndElementAsync(); //glossaryEntry
        }

        internal override async Task<SectionState> DoWriteStartSectionAsync(Block block, string title)
        {
            await WriteStartElementAsync("glossaryEntry");
            await WriteStartElementAsync("terms");
            foreach (var term in title.Split(','))
                await WriteTermAsync(term);
            await WriteEndElementAsync(); //terms
            await WriteStartElementAsync("definition");
            return SectionState.Content;
        }

        private async Task WriteTermAsync(string term)
        {
            var termTrim = term.Trim();
            var termId = termTrim.Replace(' ', '-');
            await WriteStartElementAsync("term");
            await WriteAttributeStringAsync("termId", termId);
            await WriteStringAsync(termTrim);
            await WriteEndElementAsync(); //term
        }

        internal override async Task WriteConceptualLinkAsync(Inline inline)
        {
            if (GetSectionState() != SectionState.SeeAlso)
            {
                await base.WriteConceptualLinkAsync(inline);
                return;
            }

            var target = GetConceptualLinkTarget(inline.TargetUrl);
            if (!target.StartsWith("#"))
                return;

            var termId = target.TrimStart('#');
            await WriteStartElementAsync("relatedEntry");
            await WriteAttributeStringAsync("termId", termId);
            await WriteEndElementAsync(); //relatedEntry
        }

        internal override async Task WriteExternalLinkAsync(Inline inline, string targetUrl)
        {
            if (GetSectionState() != SectionState.SeeAlso)
                await base.WriteExternalLinkAsync(inline, targetUrl);
        }
    }
}
