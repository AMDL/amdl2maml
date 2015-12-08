using System;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// Progress report object.
    /// </summary>
    public struct Indicator
    {
        private readonly string name;
        private readonly int index;
        private readonly int count;

        private Indicator(int count, int index, string name)
        {
            this.name = name;
            this.index = index;
            this.count = count;
        }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>Name.</value>
        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// Gets the index.
        /// </summary>
        /// <value>Index.</value>
        public int Index
        {
            get { return index; }
        }

        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>Count.</value>
        public int Count
        {
            get { return count; }
        } 

        /// <summary>
        /// Reports progress.
        /// </summary>
        /// <param name="progress">Progress indicator.</param>
        /// <param name="count">Count.</param>
        /// <param name="index">Index.</param>
        /// <param name="name">Name.</param>
        public static void Report(IProgress<Indicator> progress, int count, int index = 0, string name = null)
        {
            if (progress != null)
                progress.Report(new Indicator(count, index, name));
        }

        /// <summary>
        /// Reports progress.
        /// </summary>
        /// <param name="progress">Progress indicator.</param>
        /// <param name="count">Count.</param>
        /// <param name="index">Index.</param>
        /// <param name="nameFactory">Name factory.</param>
        public static void Report(IProgress<Indicator> progress, int count, int index, Func<string> nameFactory = null)
        {
            if (progress != null)
                progress.Report(new Indicator(count, index, nameFactory()));
        }
    }
}
