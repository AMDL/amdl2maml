using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    internal abstract class WriterBase
    {
        internal readonly XmlWriter writer;

        public WriterBase(XmlWriter writer)
        {
            this.writer = writer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteStartDocumentAsync()
        {
            return writer.WriteStartDocumentAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteEndDocumentAsync()
        {
            return writer.WriteEndDocumentAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteStartElementAsync(string localName)
        {
            return writer.WriteStartElementAsync(null, localName, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteStartElementAsync(string prefix, string localName, string ns)
        {
            return writer.WriteStartElementAsync(prefix, localName, ns);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteEndElementAsync()
        {
            return writer.WriteEndElementAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteElementStringAsync(string localName, string value)
        {
            return writer.WriteElementStringAsync(null, localName, null, value);
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
