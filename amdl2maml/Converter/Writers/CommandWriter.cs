using CommonMark.Syntax;
using System;
using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    internal class CommandWriter : WriterBase
    {
        public CommandWriter(XmlWriter writer)
            : base(writer)
        {
        }

        public async Task WriteAsync(Block block)
        {
            await WriteStartElementAsync("para");
            await WriteStartElementAsync("command");
            await WriteChildInlinesAsync(block);
            await WriteEndElementAsync(); //command
            await WriteEndElementAsync(); //para
        }

        private async Task WriteInlineAsync(Inline inline)
        {
            switch (inline.Tag)
            {
                case InlineTag.String:
                    await WriteStringAsync(inline);
                    break;

                case InlineTag.Emphasis:
                    await WriteWeakEmphasisAsync(inline);
                    break;

                case InlineTag.Strong:
                    await WriteStrongEmphasisAsync(inline);
                    break;

                default:
                    throw new InvalidOperationException("Unexpected inline tag: " + inline.Tag);
            }
        }

        private async Task WriteChildInlinesAsync(Block block)
        {
            for (var inline = block.InlineContent; inline != null; inline = inline.NextSibling)
                await WriteInlineAsync(inline);
        }

        private async Task WriteChildInlinesAsync(Inline inline)
        {
            for (var child = inline.FirstChild; child != null; child = child.NextSibling)
                await WriteInlineAsync(child);
        }

        private async Task WriteWeakEmphasisAsync(Inline inline)
        {
            await WriteStartElementAsync("replaceable");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //replaceable
        }

        private async Task WriteStrongEmphasisAsync(Inline inline)
        {
            await WriteStartElementAsync("system");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //system
        }
    }
}
