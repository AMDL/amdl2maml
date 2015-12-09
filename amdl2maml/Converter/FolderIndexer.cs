using Amdl.Metadata;
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
        /// Indexes the input folder.
        /// </summary>
        /// <param name="paths">Paths.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Progress indicator.</param>
        /// <returns>Topic data.</returns>
        public static async Task<IEnumerable<TopicData>> IndexAsync(Paths paths, CancellationToken cancellationToken, IProgress<Indicator> progress = null)
        {
            return await IndexAsync(paths.Source, string.Empty, cancellationToken, progress);
        }

        private static Task<IEnumerable<TopicData>> IndexAsync(string path, string relativePath, CancellationToken cancellationToken, IProgress<Indicator> progress)
        {
            return new FolderIndexer(path, relativePath, cancellationToken, progress).IndexAsync();
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
        private readonly CancellationToken cancellationToken;
        private readonly IProgress<Indicator> progress;

        private FolderIndexer(string path, string relativePath, CancellationToken cancellationToken, IProgress<Indicator> progress)
        {
            this.path = path;
            this.relativePath = relativePath;
            if (relativePath != null)
                this.folderName = Path.GetFileName(relativePath);
            this.cancellationToken = cancellationToken;
            this.progress = progress;
        }

        private async Task<IEnumerable<TopicData>> IndexAsync()
        {
            var folder = await FileSystem.Current.GetFolderFromPathAsync(this.path, cancellationToken)
                .ConfigureAwait(false);
            var topics = await IndexFilesAsync(folder);
            var subfolderTopics = await IndexFoldersAsync(folder);
            return topics.Concat(subfolderTopics);
        }

        private async Task<IEnumerable<TopicData>> IndexFoldersAsync(IFolder folder)
        {
            var folders = await folder.GetFoldersAsync(cancellationToken);
            var foldersArray = folders.Where(IsTopic).ToArray();
            var count = foldersArray.Count();
            Indicator.Report(progress, count);
            var folderTopics = Enumerable.Range(0, count).Select(index =>
                IndexFolderAsync(foldersArray[index], relativePath, index, count));
            var topicsMany = await Task.WhenAll(folderTopics);
            return topicsMany.SelectMany(t => t);
        }

        private Task<IEnumerable<TopicData>> IndexFolderAsync(IFolder folder, string relativePath, int index, int count)
        {
            relativePath = Path.Combine(relativePath, folder.Name);
            Indicator.Report(progress, count, index + 1, relativePath);
            return IndexAsync(folder.Path, relativePath, cancellationToken, null);
        }

        private async Task<IEnumerable<TopicData>> IndexFilesAsync(IFolder folder)
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

                if (split.Last().Equals("Orientation", StringComparison.OrdinalIgnoreCase)) //TODO
                    return TopicType.Orientation;
            }
            
            return TopicType.General;
        }
    }
}
