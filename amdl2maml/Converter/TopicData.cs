using System;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// AMDL topic data.
    /// </summary>
    public class TopicData
    {
        /// <summary>
        /// Creates a new instance of the <see cref="TopicData"/> class.
        /// </summary>
        /// <param name="id">Topic ID.</param>
        public TopicData(Guid id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets the ID.
        /// </summary>
        /// <value>Topic ID.</value>
        public Guid Id { get; private set; }

        ///// <summary>
        ///// Gets the name.
        ///// </summary>
        ///// <value>Topic Name.</value>
        //public string Name { get; private set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>Topic Type.</value>
        public TopicType Type { get; set; }

        /// <summary>
        /// Gets the title.
        /// </summary>
        /// <value>Topic title.</value>
        public string Title { get; internal set; }

        /// <summary>
        /// Gets or sets the relative path.
        /// </summary>
        /// <value>Relative path.</value>
        public string RelativePath { get; set; }
    }
}
