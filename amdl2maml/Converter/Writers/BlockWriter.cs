using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    class BlockWriter
    {
        private readonly XmlWriter writer;

        public BlockWriter(XmlWriter writer)
        {
            this.writer = writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteStartElementAsync(string localName)
        {
            return writer.WriteStartElementAsync(null, localName, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteEndElementAsync()
        {
            return writer.WriteEndElementAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteAttributeStringAsync(string localName, string value)
        {
            return writer.WriteAttributeStringAsync(null, localName, null, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteAttributeStringAsync(string prefix, string localName, string ns, string value)
        {
            return writer.WriteAttributeStringAsync(prefix, localName, ns, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteStringAsync(string text)
        {
            return writer.WriteStringAsync(text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteRawAsync(string text)
        {
            return writer.WriteRawAsync(text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteCommentAsync(string text)
        {
            return writer.WriteCommentAsync(text);
        }
    }
}
