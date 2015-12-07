using PCLStorage;
using System;
using System.Collections.Generic;
using System.IO;
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
        /// <param name="topics">Topics data.</param>
        /// <param name="srcPath">Source base path.</param>
        /// <param name="destPath">Destination base path.</param>
        /// <param name="name2topic">Mapping from topic name to data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Progress indicator.</param>
        /// <returns>Asynchronous task.</returns>
        public static async Task ConvertAsync(IEnumerable<TopicData> topics, string srcPath, string destPath, IDictionary<string, TopicData> name2topic,
            CancellationToken cancellationToken = default(CancellationToken), IProgress<string> progress = null)
        {
            foreach (var topic in topics)
            {
                await ConvertAsync(topic, srcPath, destPath, name2topic, cancellationToken);
                if (progress != null)
                {
                    var path = Path.Combine(topic.RelativePath, topic.Name);
                    progress.Report(path);
                }
            }
        }

        /// <summary>
        /// Converts the topic to MAML.
        /// </summary>
        /// <param name="topic">Topic data.</param>
        /// <param name="srcPath">Source base path.</param>
        /// <param name="destPath">Destination base path.</param>
        /// <param name="name2topic">Mapping from topic name to data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Asynchronous task.</returns>
        public static async Task ConvertAsync(TopicData topic, string srcPath, string destPath, IDictionary<string, TopicData> name2topic, CancellationToken cancellationToken)
        {
            var srcFilePath = Path.Combine(srcPath, topic.RelativePath, topic.FileName);
            var name = Path.GetFileNameWithoutExtension(srcFilePath);
            var destDir = Path.Combine(destPath, topic.RelativePath);
            var fileName = Path.GetFileName(srcFilePath);
            var destName = Path.ChangeExtension(fileName, ".aml");

            var srcFile = await FileSystem.Current.GetFileFromPathAsync(srcFilePath, cancellationToken)
                .ConfigureAwait(false);
            var destFolder = await FileSystem.Current.LocalStorage.CreateFolderAsync(destDir, CreationCollisionOption.OpenIfExists, cancellationToken);
            var destFile = await destFolder.CreateFileAsync(destName, CreationCollisionOption.ReplaceExisting, cancellationToken);

            await ConvertAsync(topic, name2topic, cancellationToken, srcFile, destFile);
        }

        private static async Task ConvertAsync(TopicData topic, IDictionary<string, TopicData> name2topic, CancellationToken cancellationToken, IFile srcFile, IFile destFile)
        {
            using (var srcStream = await srcFile.OpenAsync(FileAccess.Read, cancellationToken))
            using (var destStream = await destFile.OpenAsync(FileAccess.ReadAndWrite, cancellationToken))
            {
                await ConvertAsync(topic, srcStream, destStream, name2topic);
            }
        }

        private static async Task ConvertAsync(TopicData topic, Stream srcStream, Stream destStream, IDictionary<string, TopicData> name2topic)
        {
            using (var reader = new StreamReader(srcStream))
            using (var writer = new StreamWriter(destStream))
            {
                await Writers.TopicWriter.WriteAsync(topic, name2topic, reader, writer);
            }
        }
    }
}
