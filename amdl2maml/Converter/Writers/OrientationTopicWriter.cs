using System.Collections.Generic;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    class OrientationTopicWriter : TopicWriter
    {
        public OrientationTopicWriter(TopicData topic, TopicParserResult parserResult, IDictionary<string, TopicData> name2topic, XmlWriter writer)
            : base(topic, parserResult, name2topic, writer)
        {
        }

        internal override string GetDocElementName()
        {
            return "developerOrientationDocument";
        }
    }
}
