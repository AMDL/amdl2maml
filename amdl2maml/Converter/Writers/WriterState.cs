using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Amdl.Maml.Converter.Writers
{
    enum TopicState
    {
        None,
        Summary,
        Introduction,
        Content,
    }

    enum SectionState
    {
        None,
        Content,
        Sections,
        SeeAlso,
    }

    enum BlockState
    {
        None,
        Start,
    }

    enum InlineState
    {
        None,
        Start,
    }

    enum MarkupState
    {
        None,
        Inline,
    }

    /// <summary>
    /// See Also group type.
    /// </summary>
    enum SeeAlsoGroup
    {
        /// <summary>
        /// None.
        /// </summary>
        None,

        /// <summary>
        /// Concepts.
        /// </summary>
        Concepts,

        /// <summary>
        /// Other Resources.
        /// </summary>
        OtherResources,

        /// <summary>
        /// Reference.
        /// </summary>
        Reference,

        /// <summary>
        /// Tasks.
        /// </summary>
        Tasks,
    }

    sealed class WriterState
    {
        private readonly Stack<SectionState> sectionStates;

        public WriterState()
        {
            sectionStates = new Stack<SectionState>();
            sectionStates.Push(SectionState.None);
        }

        public Stack<SectionState> SectionStates
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return sectionStates; }
        }

        public TopicState TopicState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        }

        public BlockState BlockState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        }

        public InlineState InlineState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        }

        public MarkupState MarkupState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        }

        public SeeAlsoGroup SeeAlsoGroup
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set;
        }
    }
}
