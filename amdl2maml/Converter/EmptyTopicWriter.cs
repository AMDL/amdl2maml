using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter
{
    class EmptyTopicWriter : TopicWriter
    {
        public EmptyTopicWriter(TopicData topic, IDictionary<string, TopicData> name2topic)
            : base(topic, name2topic)
        {
        }

        public override Task ConvertAsync(TextReader reader, TextWriter writer)
        {
            var source = new TaskCompletionSource<object>();
            return source.Task;
        }

        protected override string GetDocElementName()
        {
            throw new NotImplementedException();
        }
    }
}
