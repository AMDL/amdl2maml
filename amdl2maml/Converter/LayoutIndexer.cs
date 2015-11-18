using Amdl.Maml.Data.Layout;
using PCLStorage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// Content layout indexer.
    /// </summary>
    public class LayoutIndexer
    {
        /// <summary>
        /// Indexes the content layout.
        /// </summary>
        /// <param name="layoutPath">Layout path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Mapping from topic title to its ID.</returns>
        public static async Task<IDictionary<string, Guid>> IndexAsync(string layoutPath, CancellationToken cancellationToken)
        {
            ContentLayout layout;
            var file = await FileSystem.Current.GetFileFromPathAsync(layoutPath, cancellationToken);
            using (var stream = await file.OpenAsync(FileAccess.Read, cancellationToken))
            using (var reader = new StreamReader(stream))
            {
                var serializer = new XmlSerializer(typeof(ContentLayout));
                layout = (ContentLayout)serializer.Deserialize(reader);
            }
            return Index(new Dictionary<string, Guid>(), layout.Topics);
        }

        private static IDictionary<string, Guid> Index(IDictionary<string, Guid> title2id, Topic[] topics)
        {
            return topics.Aggregate(title2id, Index);
        }

        private static IDictionary<string, Guid> Index(IDictionary<string, Guid> title2id, Topic topic)
        {
            title2id.Add(topic.Title, topic.Id);
            return Index(title2id, topic.Topics);
        }
    }
}
