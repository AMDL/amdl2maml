using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    class EmptyTopicWriter : TopicWriter
    {
        public EmptyTopicWriter(TopicData topic, IDictionary<string, TopicData> name2topic, XmlWriter writer)
            : base(topic, name2topic, writer)
        {
        }

        public override Task WriteAsync(TopicParserResult parserResult, TextReader reader)
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
