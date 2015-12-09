using PCLStorage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// AMDL topic converter.
    /// </summary>
    public class TopicConverter
    {
        /// <summary>
        /// Converts the topic to MAML.
        /// </summary>
        /// <param name="paths">Paths.</param>
        /// <param name="name2topic">Mapping from topic name to data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Progress indicator.</param>
        /// <returns>Asynchronous task.</returns>
        public static async Task ConvertAsync(Paths paths, IDictionary<string, TopicData> name2topic,
            CancellationToken cancellationToken = default(CancellationToken), IProgress<Indicator> progress = null)
        {
            var topicsArray = name2topic.Values.ToArray();
            var count = topicsArray.Length;
            Indicator.Report(progress, count);
            for (var index = 0; index < count; index++)
                await ConvertAsync(topicsArray[index], paths, name2topic, cancellationToken, progress, index, count);
        }

        /// <summary>
        /// Converts the topic to MAML.
        /// </summary>
        /// <param name="topic">Topic data.</param>
        /// <param name="paths">Paths.</param>
        /// <param name="name2topic">Mapping from topic name to data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Progress indicator.</param>
        /// <param name="index">Index.</param>
        /// <param name="count">Count.</param>
        /// <returns>Asynchronous task.</returns>
        public static async Task ConvertAsync(TopicData topic, Paths paths, IDictionary<string, TopicData> name2topic,
            CancellationToken cancellationToken, IProgress<Indicator> progress = null, int index = 0, int count = 0)
        {
            var destDir = Path.Combine(paths.Destination, topic.RelativePath);
            var fileName = Path.GetFileName(topic.FileName);
            var destName = Path.ChangeExtension(fileName, ".aml");

            var destFolder = await FileSystem.Current.LocalStorage.CreateFolderAsync(destDir, CreationCollisionOption.OpenIfExists, cancellationToken);
            var destFile = await destFolder.CreateFileAsync(destName, CreationCollisionOption.ReplaceExisting, cancellationToken);

            await ConvertAsync(topic, name2topic, paths, destFile, cancellationToken);

            Indicator.Report(progress, count, index + 1, () => Path.Combine(topic.RelativePath, topic.Name));
        }

        private static async Task ConvertAsync(TopicData topic, IDictionary<string, TopicData> name2topic, Paths paths, IFile destFile, CancellationToken cancellationToken)
        {
            using (var destStream = await destFile.OpenAsync(FileAccess.ReadAndWrite, cancellationToken))
            {
                await ConvertAsync(topic, destStream, name2topic, paths, cancellationToken);
            }
        }

        private static async Task ConvertAsync(TopicData topic, Stream destStream, IDictionary<string, TopicData> name2topic, Paths paths, CancellationToken cancellationToken)
        {
            using (var writer = new StreamWriter(destStream))
            {
                await Writers.TopicWriter.WriteAsync(topic, name2topic, writer, paths, cancellationToken);
            }
        }
    }
}
