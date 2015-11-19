using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        public static Task WriteEndElementAsync(this XmlWriter writer)
        {
            return writer.WriteEndElementAsync();
        }

        public static Task WriteStringAsync(this XmlWriter writer, string term)
        {
            return writer.WriteStringAsync(term);
        }

        public static Task WriteAttributeStringAsync(this XmlWriter writer, string localName, string value)
        {
            return writer.WriteAttributeStringAsync(null, localName, null, value);
        }
    }
}
