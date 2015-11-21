using CommonMark.Syntax;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    class HowToTopicWriter : TopicWriter
    {
        public HowToTopicWriter(TopicData topic, IDictionary<string, TopicData> name2topic, XmlWriter writer)
            : base(topic, name2topic, writer)
        {
        }

        internal override string GetDocElementName()
        {
            return "developerHowToDocument";
        }

        internal override async Task<SectionState> DoWriteStartSectionAsync(Block block, string title)
        {
            await WriteStartElementAsync("procedure");
            await WriteTitleAsync(block);
            return SectionState.Content;
        }

        internal override async Task WriteListAsync(Block block)
        {
            if (SectionLevel < 2)
            {
                await base.WriteListAsync(block);
                return;
            }

            await WriteEndIntroductionAsync();

            //TODO procedure?
            await WriteStartElementAsync("steps");
            await WriteListClassAsync(block, "bullet");
            SetSectionState(SectionState.Sections);
            await WriteChildBlocksAsync(block);
            await WriteEndElementAsync(); //steps
        }

        internal override async Task WriteEndSectionAsync(SectionState state)
        {
            await WriteEndElementAsync(); //procedure
        }

        internal override async Task WriteListItemAsync(Block block)
        {
            if (GetSectionState() != SectionState.Sections)
            {
                await base.WriteListItemAsync(block);
                return;
            }

            await WriteStartElementAsync("step");
            await WriteStartElementAsync("content");
            await WriteChildBlocksAsync(block);
            await WriteEndElementAsync(); //content
            await WriteEndElementAsync(); //step
        }
    }
}
