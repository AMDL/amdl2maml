using PCLStorage;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// AMDL topic file converter.
    /// </summary>
    public class FileConverter
    {
        /// <summary>
        /// Converts the topic file to MAML.
        /// </summary>
        /// <param name="srcFilePath">Source file path.</param>
        /// <param name="destPath">Destination base path.</param>
        /// <param name="getTopicFromName">Gets the topic data for the specified topic name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Asynchronous task.</returns>
        public static async Task ConvertAsync(string srcFilePath, string destPath, Func<string, TopicData> getTopicFromName, CancellationToken cancellationToken)
        {
            var name = Path.GetFileNameWithoutExtension(srcFilePath);
            var topic = getTopicFromName(name);
            var destDir = Path.Combine(destPath, topic.RelativePath);
            var destName = name + ".aml";

            var srcFile = await FileSystem.Current.GetFileFromPathAsync(srcFilePath, cancellationToken);
            var destFolder = await Storage.CreateFolderAsync(destDir, CreationCollisionOption.OpenIfExists, cancellationToken);
            var destFile = await destFolder.CreateFileAsync(destName, CreationCollisionOption.ReplaceExisting, cancellationToken);

            using (var srcStream = await srcFile.OpenAsync(FileAccess.Read, cancellationToken))
            using (var reader = new StreamReader(srcStream))
            {
                using (var destStream = await destFile.OpenAsync(FileAccess.ReadAndWrite, cancellationToken))
                using (var writer = new StreamWriter(destStream))
                {
                    var converter = new TopicConverter(topic, getTopicFromName);
                    converter.ConvertAsync(reader, writer).GetAwaiter().GetResult(); //TODO
                }
            }
        }

        private static IFolder Storage
        {
            get { return PCLStorage.FileSystem.Current.LocalStorage; }
        }
    }
}
