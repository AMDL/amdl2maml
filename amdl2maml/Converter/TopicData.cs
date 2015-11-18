using System;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// AMDL topic data.
    /// </summary>
    public class TopicData
    {
        ///// <summary>
        ///// Creates a new instance of the <see cref="TopicData"/> class.
        ///// </summary>
        ///// <param name="id">Topic ID.</param>
        //public TopicData(Guid id)
        //{
        //    Id = id;
        //}

        /// <summary>
        /// Creates a new instance of the <see cref="TopicData"/> class.
        /// </summary>
        /// <param name="name">Topic name.</param>
        /// <param name="type">Topic type.</param>
        /// <param name="relativePath">Relative path to the topic file.</param>
        public TopicData(string name, TopicType type, string relativePath)
        {
            Name = name;
            Type = type;
            RelativePath = relativePath;
        }

        /// <summary>
        /// Gets or sets the ID.
        /// </summary>
        /// <value>Topic ID.</value>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <value>Topic Name.</value>
        public string Name { get; private set; }

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        /// <value>Topic Type.</value>
        public TopicType Type { get; private set; }

        /// <summary>
        /// Gets the title.
        /// </summary>
        /// <value>Topic title.</value>
        public string Title { get; internal set; }

        /// <summary>
        /// Gets or sets the relative path.
        /// </summary>
        /// <value>Relative path.</value>
        public string RelativePath { get; private set; }
    }
}
