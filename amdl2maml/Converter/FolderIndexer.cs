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
    /// AMDL topic folder indexer.
    /// </summary>
    public class FolderIndexer
    {
        #region Static Members

        private static readonly string[] Extensions = new[] { ".md", ".markdown", ".amdl" };

        /// <summary>
        /// Indexes the specified input folder.
        /// </summary>
        /// <param name="path">Base path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Topic data.</returns>
        public static async Task<IEnumerable<TopicData>> IndexAsync(string path, CancellationToken cancellationToken)
        {
            return await IndexAsync(path, string.Empty, cancellationToken);
        }

        private static Task<IEnumerable<TopicData>> IndexAsync(string path, string relativePath, CancellationToken cancellationToken)
        {
            var converter = new FolderIndexer(path, relativePath);
            return converter.IndexAsync(cancellationToken);
        }

        private static bool IsTopic(IFolder folder)
        {
            return !folder.Name.StartsWith(".");
        }

        private static bool IsTopic(IFile file)
        {
            if (file.Name.StartsWith("."))
                return false;
            var extension = Path.GetExtension(file.Name);
            return Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        private readonly string path;
        private readonly string relativePath;
        private readonly string folderName;

        private FolderIndexer(string path, string relativePath)
        {
            this.path = path;
            this.relativePath = relativePath;
            if (relativePath != null)
                this.folderName = Path.GetFileName(relativePath);
        }

        private async Task<IEnumerable<TopicData>> IndexAsync(CancellationToken cancellationToken)
        {
            var folder = await FileSystem.Current.GetFolderFromPathAsync(this.path, cancellationToken)
                .ConfigureAwait(false);
            var topics = await IndexFilesAsync(folder, cancellationToken);
            var subfolderTopics = await IndexFoldersAsync(folder, cancellationToken);
            return topics.Concat(subfolderTopics);
        }

        private async Task<IEnumerable<TopicData>> IndexFoldersAsync(IFolder folder, CancellationToken cancellationToken)
        {
            var folders = await folder.GetFoldersAsync(cancellationToken);
            var folderTopics = folders.Where(IsTopic)
                .Select(f => IndexFolderAsync(f, relativePath, cancellationToken));
            var topicsMany = await Task.WhenAll(folderTopics);
            return topicsMany.SelectMany(t => t);
        }

        private static Task<IEnumerable<TopicData>> IndexFolderAsync(IFolder folder, string relativePath, CancellationToken cancellationToken)
        {
            relativePath = Path.Combine(relativePath, folder.Name);
            return IndexAsync(folder.Path, relativePath, cancellationToken);
        }

        private async Task<IEnumerable<TopicData>> IndexFilesAsync(IFolder folder, CancellationToken cancellationToken)
        {
            var files = await folder.GetFilesAsync(cancellationToken);
            return files.Where(IsTopic)
                .Select(CreateTopicData);
        }

        private TopicData CreateTopicData(IFile file)
        {
            var type = GetTopicType(file);
            return new TopicData(type, file.Name, relativePath);
        }

        private TopicType GetTopicType(IFile file)
        {
            if (file.Name == null)
                return TopicType.Empty;

            var name = Path.GetFileNameWithoutExtension(file.Name);
            if (name.Equals(folderName, StringComparison.OrdinalIgnoreCase)) //TODO
                return TopicType.Orientation;

            var split = name.Split('-', ' ');
            if (split.Any())
            {
                if (split.First().Equals("HowTo", StringComparison.OrdinalIgnoreCase)) //TODO
                    return TopicType.HowTo;

                if (split.Last().Equals("Glossary", StringComparison.OrdinalIgnoreCase)) //TODO
                    return TopicType.Glossary;
            }
            
            return TopicType.General;
        }
    }
}
