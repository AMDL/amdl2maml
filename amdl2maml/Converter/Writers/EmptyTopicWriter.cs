using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    class EmptyTopicWriter : TopicWriter
    {
        public EmptyTopicWriter(TopicData topic, TopicParserResult parserResult, IDictionary<string, TopicData> name2topic, XmlWriter writer)
            : base(topic, parserResult, name2topic, writer)
        {
        }

        public override Task WriteAsync()
        {
            var source = new TaskCompletionSource<object>();
            return source.Task;
        }

        internal override string GetDocElementName()
        {
            throw new NotImplementedException();
        }
    }
}
