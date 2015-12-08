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
    /// MAML content layout indexer.
    /// </summary>
    public class LayoutIndexer
    {
        /// <summary>
        /// Indexes the content layout.
        /// </summary>
        /// <param name="layoutPath">Layout path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Progress indicator.</param>
        /// <returns>Mapping from topic title to its ID.</returns>
        public static async Task<IDictionary<string, Guid>> IndexAsync(string layoutPath, CancellationToken cancellationToken, IProgress<Indicator> progress = null)
        {
            ContentLayout layout;
            var file = await FileSystem.Current.GetFileFromPathAsync(layoutPath, cancellationToken)
                .ConfigureAwait(false);
            using (var stream = await file.OpenAsync(FileAccess.Read, cancellationToken))
            using (var reader = new StreamReader(stream))
            {
                var serializer = new XmlSerializer(typeof(ContentLayout));
                layout = (ContentLayout)serializer.Deserialize(reader);
            }
            return Index(new Dictionary<string, Guid>(), layout.Topics, progress);
        }

        private static IDictionary<string, Guid> Index(IDictionary<string, Guid> title2id, Topic[] topics, IProgress<Indicator> progress)
        {
            for (var index = 0; index < topics.Length; index++)
                title2id = Index(title2id, topics[index], progress, index, topics.Length);
            return title2id;
        }

        private static IDictionary<string, Guid> Index(IDictionary<string, Guid> title2id, Topic topic, IProgress<Indicator> progress, int index, int count)
        {
            title2id.Add(topic.Title, topic.Id);
            var result = Index(title2id, topic.Topics, null);
            if (progress != null)
                progress.Report(Indicator.Create(topic.Title ?? topic.Id.ToString(), index + 1, count));
            return result;
        }
    }
}
