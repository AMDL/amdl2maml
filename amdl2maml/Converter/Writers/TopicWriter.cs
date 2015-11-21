using CommonMark.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    /// <summary>
    /// AMDL topic writer.
    /// </summary>
    public abstract class TopicWriter
    {
        #region Static Members

        private static readonly string[] Languages =
        {
            "C#", "CSharp",
            "C++", "cpp",
            "C",
            "F#", "FSharp",
            "JavaScript", "js",
            "VB.NET",
            "HTML", "XML", "XSL",
            "PowerShell",
            "Python",
            "SQL",
        };

        private static readonly IDictionary<string, SeeAlsoGroupType> SeeAlsoGroups = new Dictionary<string, SeeAlsoGroupType>
        {
            { Properties.Resources.ConceptsTitle, SeeAlsoGroupType.Concepts },
            { Properties.Resources.OtherResourcesTitle, SeeAlsoGroupType.OtherResources },
            { Properties.Resources.ReferenceTitle, SeeAlsoGroupType.Reference },
            { Properties.Resources.TasksTitle, SeeAlsoGroupType.Tasks },
        };

        private static readonly IDictionary<SeeAlsoGroupType, Guid> SeeAlsoGroupIds = new Dictionary<SeeAlsoGroupType, Guid>(5)
        {
            { SeeAlsoGroupType.None, Guid.Empty },
            { SeeAlsoGroupType.Concepts, new Guid("1FE70836-AA7D-4515-B54B-E10C4B516E50") },
            { SeeAlsoGroupType.OtherResources, new Guid("4A273212-0AC8-4D72-8349-EC11CD2FF8CD") },
            { SeeAlsoGroupType.Reference, new Guid("A635375F-98C2-4241-94E7-E427B47C20B6") },
            { SeeAlsoGroupType.Tasks, new Guid("DAC3A6A0-C863-4E5B-8F65-79EFC6A4BA09") },
        };

        private static readonly Version Version = typeof(TopicWriter).GetTypeInfo().Assembly.GetName().Version;

        /// <summary>
        /// Writes the topic as MAML.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="writer">Writer.</param>
        /// <param name="topic">The topic.</param>
        /// <param name="name2topic">Mapping from topic name to data.</param>
        /// <returns>Asynchronous task.</returns>
        public static Task WriteAsync(TopicData topic, IDictionary<string, TopicData> name2topic, StreamReader reader, StreamWriter writer)
        {
            return TopicWriter.Create(topic, name2topic).WriteAsync(reader, writer);
        }

        private static TopicWriter Create(TopicData topic, IDictionary<string, TopicData> name2topic)
        {
            switch (topic.Type)
            {
                case TopicType.Empty:
                    return new EmptyTopicWriter(topic, name2topic);
                case TopicType.General:
                    return new GeneralTopicWriter(topic, name2topic);
                case TopicType.Glossary:
                    return new GlossaryTopicWriter(topic, name2topic);
                case TopicType.Orientation:
                    return new OrientationTopicWriter(topic, name2topic);
                default:
                    throw new InvalidOperationException("Unexpected topic type: " + topic.Type);
            }
        }

        #endregion

        #region Nested Types

        enum TopicState
        {
            None,
            Summary,
            Introduction,
            Content,
        }

        internal enum SectionState
        {
            None,
            Content,
            Sections,
            SeeAlso,
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
        enum SeeAlsoGroupType
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

        #endregion

        #region Fields

        private readonly TopicData topic;
        private readonly IDictionary<string, TopicData> name2topic;

        private TopicState topicState;
        private Stack<SectionState> sectionStates;
        private InlineState inlineState;
        private MarkupState markupState;
        private SeeAlsoGroupType seeAlsoGroup;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TopicWriter"/> class.
        /// </summary>
        /// <param name="topic">Current topic.</param>
        /// <param name="name2topic">Mapping from topic name to data.</param>
        protected TopicWriter(TopicData topic, IDictionary<string, TopicData> name2topic)
        {
            this.topic = topic;
            this.name2topic = name2topic;
        }

        #endregion

        #region Public Members

        /// <summary>
        /// Writes the topic as MAML.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="writer">Writer.</param>
        /// <returns>Asynchronous task.</returns>
        public virtual async Task WriteAsync(TextReader reader, TextWriter writer)
        {
            var xmlSettings = new XmlWriterSettings
            {
                Async = true,
                Indent = true,
            };

            using (var xmlWriter = XmlWriter.Create(writer, xmlSettings))
            {
                await WriteDocumentAsync(topic.ParserResult.Document, xmlWriter);
            }
        }

        #endregion

        #region Protected Members

        /// <summary>
        /// Gets the name of the document element.
        /// </summary>
        /// <returns>The name of the document element.</returns>
        protected abstract string GetDocElementName();

        #endregion

        #region Document

        private async Task WriteDocumentAsync(Block block, XmlWriter writer)
        {
            sectionStates = new Stack<SectionState>();
            sectionStates.Push(SectionState.None);

            await writer.WriteStartDocumentAsync()
                .ConfigureAwait(false);

            await writer.WriteCommentAsync(" This document was generated by a tool. ");
            await writer.WriteCommentAsync(" Changes to this file may cause incorrect behavior and will be lost if the code is regenerated. ");
            var versionString = string.Format(" amdl2maml Version {0}.{1} ", Version.Major, Version.Minor); //TODO Replace with just Version when stable
            await writer.WriteCommentAsync(versionString);

            await writer.WriteStartElementAsync(null, "topic", null);
            await writer.WriteAttributeStringAsync(null, "id", null, Id.ToString());
            await writer.WriteAttributeStringAsync(null, "revisionNumber", null, "1");

            await writer.WriteStartElementAsync(null, GetDocElementName(), "http://ddue.schemas.microsoft.com/authoring/2003/5");
            await writer.WriteAttributeStringAsync("xmlns", "xlink", null, "http://www.w3.org/1999/xlink");

            await WriteStartSummaryAsync(writer);

            await WriteChildBlocksAsync(block, writer);

            await WriteEndSectionsAsync(2, writer);
            await WriteEndSummaryAsync(writer);
            await WriteEndIntroductionAsync(writer);
            await WriteRelatedTopicsAsync(block, writer);

            await writer.WriteEndElementAsync(); //developerConceptualDocument
            await writer.WriteEndElementAsync(); //topic
            await writer.WriteEndDocumentAsync();
        }

        private async Task WriteStartSummaryAsync(XmlWriter writer)
        {
            await writer.WriteStartElementAsync(null, "summary", null);
            topicState = TopicState.Summary;
        }

        private async Task WriteEndSummaryAsync(XmlWriter writer)
        {
            if (topicState == TopicState.Summary)
            {
                await writer.WriteEndElementAsync(); //summary
                topicState = TopicState.Content;
            }
        }

        internal virtual async Task WriteStartIntroductionAsync(Block block, XmlWriter writer)
        {
            await writer.WriteStartElementAsync(null, "introduction", null);
            if (block.Tag == BlockTag.SETextHeader)
                await writer.WriteElementStringAsync(null, "autoOutline", null, null);
            topicState = TopicState.Introduction;
        }

        internal virtual async Task WriteEndIntroductionAsync(XmlWriter writer)
        {
            if (topicState == TopicState.Introduction)
            {
                await writer.WriteEndElementAsync(); //introduction
                topicState = TopicState.Content;
            }
        }

        private async Task WriteStartSeeAlsoAsync(XmlWriter writer, int level)
        {
            await DoWriteStartSeeAlso(writer, level);
            sectionStates.Pop();
            sectionStates.Push(SectionState.SeeAlso);
            seeAlsoGroup = SeeAlsoGroupType.None;
        }

        internal virtual async Task DoWriteStartSeeAlso(XmlWriter writer, int level)
        {
            await WriteEndSectionsAsync(level, writer);
            if (level > 2)
                await writer.WriteEndElementAsync(); //content | sections
            await writer.WriteStartElementAsync(null, "relatedTopics", null);
        }

        private void WriteStartSeeAlsoGroup(string title)
        {
            SeeAlsoGroupType group;
            if (SeeAlsoGroups.TryGetValue(title, out group))
                seeAlsoGroup = group;
            else
                seeAlsoGroup = SeeAlsoGroupType.None;
        }

        #endregion

        #region Block

        private async Task WriteBlockAsync(Block block, XmlWriter writer)
        {
            switch (block.Tag)
            {
                case BlockTag.AtxHeader:
                case BlockTag.SETextHeader:
                    await WriteStartSectionAsync(block, writer);
                    break;

                case BlockTag.Paragraph:
                    await WriteParagraphAsync(block, writer);
                    break;

                case BlockTag.BlockQuote:
                    await writer.WriteStartElementAsync(null, "quote", null);
                    await WriteChildBlocksAsync(block, writer);
                    await writer.WriteEndElementAsync(); //quote
                    break;

                case BlockTag.HtmlBlock:
                    await writer.WriteStartElementAsync(null, "markup", null);
                    await writer.WriteRawAsync("\n");
                    await writer.WriteRawAsync(block.StringContent.ToString());
                    await writer.WriteEndElementAsync(); //markup
                    break;

                case BlockTag.IndentedCode:
                    await WriteIndentedCodeAsync(block, writer);
                    break;

                case BlockTag.FencedCode:
                    await WriteFencedCodeAsync(block, writer);
                    break;

                case BlockTag.List:
                    await WriteListAsync(block, writer);
                    break;

                case BlockTag.ListItem:
                    await WriteListItemAsync(block, writer);
                    break;

#if TABLES
                case BlockTag.Table:
                    await WriteTableAsync(block, writer);
                    break;

                case BlockTag.TableRow:
                    await WriteTableRowAsync(block, writer);
                    break;

                case BlockTag.TableCell:
                    await WriteTableCellAsync(block, writer);
                    break;
#endif

                case BlockTag.ReferenceDefinition:
                    break;

                case BlockTag.HorizontalRuler:
                    throw new NotImplementedException();

                default:
                    throw new InvalidOperationException("Unexpected block tag: " + block.Tag);
            }
        }

        private async Task WriteChildBlocksAsync(Block block, XmlWriter writer)
        {
            for (var child = block.FirstChild; child != null; child = child.NextSibling)
                await WriteBlockAsync(child, writer);
        }

        private async Task WriteParagraphAsync(Block block, XmlWriter writer)
        {
            if (!IsInSeeAlso)
                await writer.WriteStartElementAsync(null, "para", null);
            await WriteChildInlinesAsync(block, writer);
            if (!IsInSeeAlso)
                await writer.WriteEndElementAsync(); //para
        }

        #endregion Block

        #region Inline

        private async Task WriteInlineAsync(Inline inline, XmlWriter writer)
        {
            switch (inline.Tag)
            {
                case InlineTag.String:
                    await writer.WriteStringAsync(inline.LiteralContent);
                    break;

                case InlineTag.Code:
                    await writer.WriteStartElementAsync(null, "codeInline", null);
                    await writer.WriteRawAsync(inline.LiteralContent);
                    await writer.WriteEndElementAsync();
                    break;

                case InlineTag.Emphasis:
                    await writer.WriteStartElementAsync(null, "legacyItalic", null);
                    await WriteChildInlinesAsync(inline, writer);
                    await writer.WriteEndElementAsync();
                    break;

                case InlineTag.RawHtml:
                    await WriteStartMarkupInlineAsync(writer);
                    await writer.WriteRawAsync(inline.LiteralContent);
                    await WriteChildInlinesAsync(inline, writer);
                    break;

                case InlineTag.Strong:
                    await writer.WriteStartElementAsync(null, "legacyBold", null);
                    await WriteChildInlinesAsync(inline, writer);
                    await writer.WriteEndElementAsync();
                    break;

                case InlineTag.Subscript:
                    await writer.WriteStartElementAsync(null, "subscript", null);
                    await WriteChildInlinesAsync(inline, writer);
                    await writer.WriteEndElementAsync(); //subscript;
                    break;

                case InlineTag.Superscript:
                    await writer.WriteStartElementAsync(null, "superscript", null);
                    await WriteChildInlinesAsync(inline, writer);
                    await writer.WriteEndElementAsync(); //superscript;
                    break;

                case InlineTag.SoftBreak:
                    if (!IsInSeeAlso)
                        await writer.WriteRawAsync("\n");
                    break;

                case InlineTag.Link:
                    await WriteLinkAsync(inline, writer);
                    break;

                case InlineTag.Image:
                    await WriteImageAsync(inline, writer);
                    break;

                case InlineTag.LineBreak:
                    throw new NotImplementedException();

                case InlineTag.Strikethrough:
                    await WriteStrikethroughAsync(inline, writer);
                    break;

                default:
                    throw new InvalidOperationException("Unexpected inline tag: " + inline.Tag);
            }
        }

        private async Task WriteChildInlinesAsync(Block block, XmlWriter writer)
        {
            for (var inline = block.InlineContent; inline != null; inline = inline.NextSibling)
            {
                await WriteInlineAsync(inline, writer);
                inlineState = InlineState.Start;
            }
            inlineState = InlineState.None;
            await WriteEndMarkupInlineAsync(writer);
        }

        private async Task WriteStrikethroughAsync(Inline inline, XmlWriter writer)
        {
            await WriteStartMarkupInlineAsync(writer);
            await writer.WriteStartElementAsync(null, "s", null);
            await WriteChildInlinesAsync(inline, writer);
            await writer.WriteEndElementAsync(); //s
        }

        private async Task WriteStartMarkupInlineAsync(XmlWriter writer)
        {
            if (!IsInMarkupInline)
            {
                await writer.WriteStartElementAsync(null, "markup", null);
                markupState = MarkupState.Inline;
            }
        }

        private async Task WriteEndMarkupInlineAsync(XmlWriter writer)
        {
            if (IsInMarkupInline)
            {
                await writer.WriteEndElementAsync(); //markup
                markupState = MarkupState.None;
            }
        }

        private async Task WriteChildInlinesAsync(Inline inline, XmlWriter writer)
        {
            for (var child = inline.FirstChild; child != null; child = child.NextSibling)
                await WriteInlineAsync(child, writer);
        }

        #endregion Inline

        #region Section

        private async Task WriteStartSectionAsync(Block block, XmlWriter writer)
        {
            await WriteEndSummaryAsync(writer);

            if (block.HeaderLevel == 1)
            {
                await WriteStartIntroductionAsync(block, writer);
                return;
            }

            await WriteEndIntroductionAsync(writer);

            var title = block.InlineContent.LiteralContent;
            if (IsInSeeAlso && block.HeaderLevel > sectionStates.Count())
            {
                WriteStartSeeAlsoGroup(title);
                return;
            }

            if (title.Equals(Properties.Resources.SeeAlsoTitle))
            {
                await WriteStartSeeAlsoAsync(writer, block.HeaderLevel);
                return;
            }

            await WriteEndSectionsAsync(block.HeaderLevel, writer);
            var state = await DoWriteStartSectionAsync(block, title, writer);
            sectionStates.Push(state);
        }

        internal virtual async Task<SectionState> DoWriteStartSectionAsync(Block block, string title, XmlWriter writer)
        {
            var state = sectionStates.Peek();
            if (state == SectionState.Content)
            {
                await writer.WriteEndElementAsync(); //content
                await writer.WriteStartElementAsync(null, "sections", null);
                sectionStates.Pop();
                sectionStates.Push(SectionState.Sections);
            }

            await writer.WriteStartElementAsync(null, "section", null);
            await writer.WriteAttributeStringAsync(null, "address", null, title);
            await writer.WriteElementStringAsync(null, "title", null, title);
            await writer.WriteStartElementAsync(null, "content", null);

            if (block.Tag == BlockTag.SETextHeader)
                await writer.WriteElementStringAsync(null, "autoOutline", null, null);

            return SectionState.Content;
        }

        private async Task WriteEndSectionsAsync(int level, XmlWriter writer)
        {
            while (sectionStates.Count() >= level)
            {
                var state = sectionStates.Pop();
                await WriteEndSectionAsync(state, writer);
            }
        }

        internal virtual async Task WriteEndSectionAsync(SectionState state, XmlWriter writer)
        {
            await writer.WriteEndElementAsync(); //content | sections
            await writer.WriteEndElementAsync(); //section
        }

        #endregion Section

        #region Code

        private static async Task WriteIndentedCodeAsync(Block block, XmlWriter writer)
        {
            await writer.WriteStartElementAsync("code");
            await writer.WriteAttributeStringAsync("language", "none");
            await writer.WriteRawAsync("\n");
            await writer.WriteRawAsync(block.StringContent.ToString());
            await writer.WriteEndElementAsync(); //code
        }

        private static async Task WriteFencedCodeAsync(Block block, XmlWriter writer)
        {
            await writer.WriteStartElementAsync("code");
            if (!string.IsNullOrEmpty(block.FencedCodeData.Info))
            {
                if (Languages.Contains(block.FencedCodeData.Info))
                    await writer.WriteAttributeStringAsync("language", block.FencedCodeData.Info);
                else
                {
                    await writer.WriteAttributeStringAsync("language", "none");
                    await writer.WriteAttributeStringAsync("title", block.FencedCodeData.Info);
                }
            }
            await writer.WriteRawAsync("\n");
            await writer.WriteStringAsync(block.StringContent.ToString());
            await writer.WriteEndElementAsync(); //code
        }

        #endregion Code

        #region Link

        private async Task WriteLinkAsync(Inline inline, XmlWriter writer)
        {
            if (Uri.IsWellFormedUriString(inline.TargetUrl, UriKind.Absolute))
                await WriteExternalLinkAsync(inline, writer);
            else
                await WriteConceptualLinkAsync(inline, writer);
        }

        internal virtual async Task WriteConceptualLinkAsync(Inline inline, XmlWriter writer)
        {
            var href = GetConceptualLinkTarget(inline);
            await writer.WriteStartElementAsync(null, "link", null);
            await writer.WriteAttributeStringAsync("xlink", "href", "http://www.w3.org/1999/xlink", href);
            await WriteTopicTypeAttributeAsync(writer);
            if (inline.FirstChild != null)
                await WriteChildInlinesAsync(inline, writer);
            //else
            //    await writer.WriteStringAsync(inline.LiteralContent);
            await writer.WriteEndElementAsync(); //link
        }

        internal virtual async Task WriteExternalLinkAsync(Inline inline, XmlWriter writer)
        {
            await writer.WriteStartElementAsync(null, "externalLink", null);
            await writer.WriteStartElementAsync(null, "linkUri", null);
            await writer.WriteStringAsync(inline.TargetUrl);
            await writer.WriteEndElementAsync(); //linkUri

            await writer.WriteStartElementAsync(null, "linkText", null);
            if (!string.IsNullOrEmpty(inline.LiteralContent))
                await writer.WriteStringAsync(inline.LiteralContent);
            else
                await WriteChildInlinesAsync(inline, writer);
            await writer.WriteEndElementAsync(); //linkText

            await writer.WriteEndElementAsync(); //externalLink
        }

        internal virtual async Task WriteRelatedTopicsAsync(Block block, XmlWriter writer)
        {
            if (block.ReferenceMap.Count > 0 && !IsInSeeAlso)
                await writer.WriteStartElementAsync(null, "relatedTopics", null);
            if (block.ReferenceMap.Count > 0 || IsInSeeAlso)
            {
                foreach (var reference in block.ReferenceMap.Values)
                    await WriteReferenceLinkAsync(reference, writer);
            }
            if (block.ReferenceMap.Count > 0 || IsInSeeAlso)
                await writer.WriteEndElementAsync();  //relatedTopics
        }

        private async Task WriteReferenceLinkAsync(Reference reference, XmlWriter writer)
        {
            await writer.WriteStartElementAsync(null, "externalLink", null);
            await writer.WriteStartElementAsync(null, "linkUri", null);
            await writer.WriteStringAsync(reference.Url);
            await writer.WriteEndElementAsync(); //linkUri

            var text = string.IsNullOrEmpty(reference.Title)
                ? reference.Label
                : reference.Title;
            await writer.WriteElementStringAsync(null, "linkText", null, text);

            await writer.WriteEndElementAsync(); //externalLink
        }

        internal string GetConceptualLinkTarget(Inline inline)
        {
            var split = inline.TargetUrl.Split('#');
            if (split[0].Length > 0)
                return name2topic[split[0]].Id.ToString();
            return '#' + split[1];
        }

        private async Task WriteTopicTypeAttributeAsync(XmlWriter writer)
        {
            if (IsInSeeAlso)
            {
                Guid guid = GetGroupId();
                if (guid != default(Guid))
                    await writer.WriteAttributeStringAsync(null, "topicType_id", null, guid.ToString());
            }
        }

        private Guid GetGroupId()
        {
            return SeeAlsoGroupIds[seeAlsoGroup];
        }

        #endregion

        #region Image

        private async Task WriteImageAsync(Inline inline, XmlWriter writer)
        {
            if (inlineState == InlineState.Start)
                await WriteInlineImageAsync(inline, writer);
            else
                await WriteBlockImageAsync(inline, writer);
        }

        private async Task WriteInlineImageAsync(Inline inline, XmlWriter writer)
        {
            await writer.WriteStartElementAsync(null, "mediaLinkInline", null);
            await writer.WriteStartElementAsync(null, "image", null);
            await writer.WriteAttributeStringAsync("xlink", "href", "http://www.w3.org/1999/xlink", inline.TargetUrl);
            await writer.WriteEndElementAsync(); //image
            await writer.WriteEndElementAsync(); //mediaLinkInline
        }

        private async Task WriteBlockImageAsync(Inline inline, XmlWriter writer)
        {
            await writer.WriteStartElementAsync(null, "mediaLink", null);
            await writer.WriteStartElementAsync(null, "image", null);
            await writer.WriteAttributeStringAsync("xlink", "href", "http://www.w3.org/1999/xlink", inline.TargetUrl);
            await writer.WriteEndElementAsync(); //image
            if (inline.FirstChild != null)
                await WriteCaptionAsync(inline, writer);
            await writer.WriteEndElementAsync(); //mediaLink
        }

        private async Task WriteCaptionAsync(Inline inline, XmlWriter writer)
        {
            await writer.WriteStartElementAsync(null, "caption", null);
            await writer.WriteAttributeStringAsync(null, "placement", null, "after");
            await WriteChildInlinesAsync(inline, writer);
            await writer.WriteEndElementAsync(); //caption
        }

        #endregion Image

        #region List

        private async Task WriteListAsync(Block block, XmlWriter writer)
        {
            await writer.WriteStartElementAsync(null, "list", null);
            var listClass = GetListClass(block);
            if (listClass != null)
                await writer.WriteAttributeStringAsync(null, "class", null, listClass);
            await WriteChildBlocksAsync(block, writer);
            await writer.WriteEndElementAsync(); //list
        }

        private async Task WriteListItemAsync(Block block, XmlWriter writer)
        {
            await writer.WriteStartElementAsync(null, "listItem", null);
            await WriteChildBlocksAsync(block, writer);
            await writer.WriteEndElementAsync(); //listItem
        }

        #endregion

        #region Table

        private async Task WriteTableAsync(Block block, XmlWriter writer)
        {
            await writer.WriteStartElementAsync("table");
            await WriteChildBlocksAsync(block, writer);
            await writer.WriteEndElementAsync(); //table
        }

        private async Task WriteTableRowAsync(Block block, XmlWriter writer)
        {
            await writer.WriteStartElementAsync("row");
            await WriteChildBlocksAsync(block, writer);
            await writer.WriteEndElementAsync(); //row
        }

        private async Task WriteTableCellAsync(Block block, XmlWriter writer)
        {
            await writer.WriteStartElementAsync("entry");
            await WriteChildInlinesAsync(block, writer);
            //await WriteChildBlocksAsync(block, writer);
            await writer.WriteEndElementAsync(); //entry
        }

        #endregion Table

        #region Private Members

        private static string GetListClass(Block block)
        {
            switch (block.ListData.ListType)
            {
                case ListType.Bullet:
                    if (block.ListData.BulletChar == '*')
                        return "bullet";
                    return "nobullet";

                case ListType.Ordered:
                    return "ordered";

                default:
                    return null;
            }
        }

        internal bool IsInSeeAlso
        {
            get { return sectionStates.Peek() == SectionState.SeeAlso; }
        }

        private bool IsInMarkupInline
        {
            get { return markupState == MarkupState.Inline; }
        }

        private TopicData Topic
        {
            get { return topic; }
        }

        private Guid Id
        {
            get { return Topic.Id; }
        }

        #endregion
    }
}
