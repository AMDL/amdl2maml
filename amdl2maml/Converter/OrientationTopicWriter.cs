using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Amdl.Maml.Converter
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
