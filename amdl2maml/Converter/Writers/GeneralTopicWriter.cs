using System.Collections.Generic;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    class GeneralTopicWriter : TopicWriter
    {
        public GeneralTopicWriter(TopicData topic, TopicParserResult parserResult, IDictionary<string, TopicData> name2topic, XmlWriter writer)
            : base(topic, parserResult, name2topic, writer)
        {
        }

        internal override string GetDocElementName()
        {
            return "developerConceptualDocument";
        }
    }
}
