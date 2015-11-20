using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter
{
    static class XmlWriterExtensions
    {
        public static Task WriteStartElementAsync(this XmlWriter writer, string localName)
        {
            return writer.WriteStartElementAsync(null, localName, null);
        }

        public static Task WriteAttributeStringAsync(this XmlWriter writer, string localName, string value)
        {
            return writer.WriteAttributeStringAsync(null, localName, null, value);
        }
    }
}
