using System.Collections.Generic;

namespace Amdl.Maml.Converter.Writers
{
    class OrientationTopicWriter : TopicWriter
    {
        public OrientationTopicWriter(TopicData topic, IDictionary<string, TopicData> name2topic)
            : base(topic, name2topic)
        {
        }

        protected override string GetDocElementName()
        {
            return "developerOrientationDocument";
        }
    }
}
