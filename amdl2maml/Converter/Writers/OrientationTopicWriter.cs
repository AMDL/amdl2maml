using System.Collections.Generic;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    class OrientationTopicWriter : TopicWriter
    {
        public OrientationTopicWriter(TopicData topic, IDictionary<string, TopicData> name2topic, XmlWriter writer)
            : base(topic, name2topic, writer)
        {
        }

        internal override string GetDocElementName()
        {
            return "developerOrientationDocument";
        }
    }
}
