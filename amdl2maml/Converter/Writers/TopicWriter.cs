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
    internal abstract class TopicWriter
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
            var xmlSettings = new XmlWriterSettings
            {
                Async = true,
                Indent = true,
            };

            using (var xmlWriter = XmlWriter.Create(writer, xmlSettings))
            {
                var topicWriter = TopicWriter.Create(topic, name2topic, xmlWriter);
                return topicWriter.WriteAsync(reader);
            }
        }

        private static TopicWriter Create(TopicData topic, IDictionary<string, TopicData> name2topic, XmlWriter writer)
        {
            switch (topic.Type)
            {
                case TopicType.Empty:
                    return new EmptyTopicWriter(topic, name2topic, writer);
                case TopicType.General:
                    return new GeneralTopicWriter(topic, name2topic, writer);
                case TopicType.Glossary:
                    return new GlossaryTopicWriter(topic, name2topic, writer);
                case TopicType.HowTo:
                    return new HowToTopicWriter(topic, name2topic, writer);
                case TopicType.Orientation:
                    return new OrientationTopicWriter(topic, name2topic, writer);
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
        private readonly XmlWriter writer;

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
        /// <param name="writer">XML writer.</param>
        protected TopicWriter(TopicData topic, IDictionary<string, TopicData> name2topic, XmlWriter writer)
        {
            this.topic = topic;
            this.name2topic = name2topic;
            this.writer = writer;
        }

        #endregion

        #region Public Members

        /// <summary>
        /// Writes the topic as MAML.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <returns>Asynchronous task.</returns>
        public virtual async Task WriteAsync(TextReader reader)
        {
            await WriteDocumentAsync(topic.ParserResult.Document);
        }

        #endregion

        #region Internal Members

        internal abstract string GetDocElementName();

        internal Task WriteStartElementAsync(string localName)
        {
            return writer.WriteStartElementAsync(localName);
        }

        internal Task WriteEndElementAsync()
        {
            return writer.WriteEndElementAsync();
        }

        internal Task WriteElementStringAsync(string localName, string value)
        {
            return writer.WriteElementStringAsync(localName, value);
        }

        internal Task WriteAttributeStringAsync(string localName, string value)
        {
            return writer.WriteAttributeStringAsync(localName, value);
        }

        internal Task WriteStringAsync(string text)
        {
            return writer.WriteStringAsync(text);
        }

        #endregion

        #region Document

        private async Task WriteDocumentAsync(Block block)
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

            await WriteStartSummaryAsync();

            await WriteChildBlocksAsync(block);

            await WriteEndSectionsAsync(2);
            await WriteEndSummaryAsync();
            await WriteEndIntroductionAsync();
            await WriteRelatedTopicsAsync(block);

            await writer.WriteEndElementAsync(); //developerConceptualDocument
            await writer.WriteEndElementAsync(); //topic
            await writer.WriteEndDocumentAsync();
        }

        private async Task WriteStartSummaryAsync()
        {
            await writer.WriteStartElementAsync(null, "summary", null);
            topicState = TopicState.Summary;
        }

        private async Task WriteEndSummaryAsync()
        {
            if (topicState == TopicState.Summary)
            {
                await writer.WriteEndElementAsync(); //summary
                topicState = TopicState.Content;
            }
        }

        internal virtual async Task WriteStartIntroductionAsync(Block block)
        {
            await WriteStartElementAsync("introduction");
            await WriteAutoOutlineAsync(block);

            topicState = TopicState.Introduction;
        }

        internal virtual async Task WriteEndIntroductionAsync()
        {
            if (topicState == TopicState.Introduction)
            {
                await writer.WriteEndElementAsync(); //introduction
                topicState = TopicState.Content;
            }
        }

        private async Task WriteStartSeeAlsoAsync(int level)
        {
            await DoWriteStartSeeAlso(level);
            SetSectionState(SectionState.SeeAlso);
            seeAlsoGroup = SeeAlsoGroupType.None;
        }

        internal virtual async Task DoWriteStartSeeAlso(int level)
        {
            await WriteEndSectionsAsync(level);
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

        private async Task WriteBlockAsync(Block block)
        {
            switch (block.Tag)
            {
                case BlockTag.AtxHeader:
                case BlockTag.SETextHeader:
                    await WriteStartSectionAsync(block);
                    break;

                case BlockTag.Paragraph:
                    await WriteParagraphAsync(block);
                    break;

                case BlockTag.BlockQuote:
                    await writer.WriteStartElementAsync(null, "quote", null);
                    await WriteChildBlocksAsync(block);
                    await writer.WriteEndElementAsync(); //quote
                    break;

                case BlockTag.HtmlBlock:
                    await writer.WriteStartElementAsync(null, "markup", null);
                    await writer.WriteRawAsync("\n");
                    await writer.WriteRawAsync(block.StringContent.ToString());
                    await writer.WriteEndElementAsync(); //markup
                    break;

                case BlockTag.IndentedCode:
                    await WriteIndentedCodeAsync(block);
                    break;

                case BlockTag.FencedCode:
                    await WriteFencedCodeAsync(block);
                    break;

                case BlockTag.List:
                    await WriteListAsync(block);
                    break;

                case BlockTag.ListItem:
                    await WriteListItemAsync(block);
                    break;

#if TABLES
                case BlockTag.Table:
                    await WriteTableAsync(block);
                    break;

                case BlockTag.TableRow:
                    await WriteTableRowAsync(block);
                    break;

                case BlockTag.TableCell:
                    await WriteTableCellAsync(block);
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

        internal async Task WriteChildBlocksAsync(Block block)
        {
            for (var child = block.FirstChild; child != null; child = child.NextSibling)
                await WriteBlockAsync(child);
        }

        private async Task WriteParagraphAsync(Block block)
        {
            if (GetSectionState() != SectionState.SeeAlso)
                await WriteStartElementAsync("para");
            await WriteChildInlinesAsync(block);
            if (GetSectionState() != SectionState.SeeAlso)
                await WriteEndElementAsync(); //para
        }

        #endregion Block

        #region Inline

        private async Task WriteInlineAsync(Inline inline)
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
                    await WriteChildInlinesAsync(inline);
                    await writer.WriteEndElementAsync();
                    break;

                case InlineTag.RawHtml:
                    await WriteStartMarkupInlineAsync();
                    await writer.WriteRawAsync(inline.LiteralContent);
                    await WriteChildInlinesAsync(inline);
                    break;

                case InlineTag.Strong:
                    await writer.WriteStartElementAsync(null, "legacyBold", null);
                    await WriteChildInlinesAsync(inline);
                    await writer.WriteEndElementAsync();
                    break;

                case InlineTag.Subscript:
                    await writer.WriteStartElementAsync(null, "subscript", null);
                    await WriteChildInlinesAsync(inline);
                    await writer.WriteEndElementAsync(); //subscript;
                    break;

                case InlineTag.Superscript:
                    await writer.WriteStartElementAsync(null, "superscript", null);
                    await WriteChildInlinesAsync(inline);
                    await writer.WriteEndElementAsync(); //superscript;
                    break;

                case InlineTag.SoftBreak:
                    if (GetSectionState() != SectionState.SeeAlso)
                        await writer.WriteRawAsync("\n");
                    break;

                case InlineTag.LineBreak:
                    await WriteLineBreakAsync();
                    break;

                case InlineTag.Link:
                    await WriteLinkAsync(inline);
                    break;

                case InlineTag.Image:
                    await WriteImageAsync(inline);
                    break;

                case InlineTag.Strikethrough:
                    await WriteStrikethroughAsync(inline);
                    break;

                default:
                    throw new InvalidOperationException("Unexpected inline tag: " + inline.Tag);
            }
        }

        private async Task WriteChildInlinesAsync(Block block)
        {
            for (var inline = block.InlineContent; inline != null; inline = inline.NextSibling)
            {
                await WriteInlineAsync(inline);
                inlineState = InlineState.Start;
            }
            inlineState = InlineState.None;
            await WriteEndMarkupInlineAsync();
        }

        private async Task WriteStrikethroughAsync(Inline inline)
        {
            await WriteStartMarkupInlineAsync();
            await writer.WriteStartElementAsync(null, "s", null);
            await WriteChildInlinesAsync(inline);
            await writer.WriteEndElementAsync(); //s
        }

        private async Task WriteLineBreakAsync()
        {
            if (GetSectionState() != SectionState.SeeAlso)
            {
                await WriteStartElementAsync("markup");
                await WriteElementStringAsync("br", null);
                await WriteEndElementAsync(); //markup
            }
        }

        private async Task WriteStartMarkupInlineAsync()
        {
            if (!IsInMarkupInline)
            {
                await writer.WriteStartElementAsync(null, "markup", null);
                markupState = MarkupState.Inline;
            }
        }

        private async Task WriteEndMarkupInlineAsync()
        {
            if (IsInMarkupInline)
            {
                await writer.WriteEndElementAsync(); //markup
                markupState = MarkupState.None;
            }
        }

        private async Task WriteChildInlinesAsync(Inline inline)
        {
            for (var child = inline.FirstChild; child != null; child = child.NextSibling)
                await WriteInlineAsync(child);
        }

        #endregion Inline

        #region Section

        private async Task WriteStartSectionAsync(Block block)
        {
            await WriteEndSummaryAsync();

            if (block.HeaderLevel == 1)
            {
                await WriteStartIntroductionAsync(block);
                return;
            }

            await WriteEndIntroductionAsync();

            var title = GetTitle(block);
            if (GetSectionState() == SectionState.SeeAlso && block.HeaderLevel > SectionLevel)
            {
                WriteStartSeeAlsoGroup(title);
                return;
            }

            if (title.Equals(Properties.Resources.SeeAlsoTitle))
            {
                await WriteStartSeeAlsoAsync(block.HeaderLevel);
                return;
            }

            await WriteEndSectionsAsync(block.HeaderLevel);
            var state = await DoWriteStartSectionAsync(block, title);
            sectionStates.Push(state);
        }

        internal virtual async Task<SectionState> DoWriteStartSectionAsync(Block block, string title)
        {
            var state = sectionStates.Peek();
            if (state == SectionState.Content)
            {
                await WriteEndElementAsync(); //content
                await WriteStartElementAsync("sections");
                SetSectionState(SectionState.Sections);
            }

            await WriteStartElementAsync("section");
            await WriteAttributeStringAsync("address", title);
            await WriteTitleAsync(block);
            await WriteStartElementAsync("content");
            await WriteAutoOutlineAsync(block);

            return SectionState.Content;
        }

        private async Task WriteAutoOutlineAsync(Block block)
        {
            if (block.Tag == BlockTag.SETextHeader)
            {
                await WriteStartElementAsync("autoOutline");
                await WriteAttributeStringAsync("lead", "none");
                await WriteAttributeStringAsync("excludeRelatedTopics", "true");
                await WriteEndElementAsync(); //autoOutline
            }
        }

        internal async Task WriteTitleAsync(Block block)
        {
            await WriteStartElementAsync("title");
            await WriteChildInlinesAsync(block);
            await WriteEndElementAsync(); //title
        }

        internal static string GetTitle(Block block)
        {
            var title = string.Empty;
            for (var inline = block.InlineContent; inline != null; inline = inline.NextSibling)
                title += inline.LiteralContent;
            return title;
        }

        private async Task WriteEndSectionsAsync(int level)
        {
            while (sectionStates.Count() >= level)
            {
                var state = sectionStates.Pop();
                await WriteEndSectionAsync(state);
            }
        }

        internal virtual async Task WriteEndSectionAsync(SectionState state)
        {
            await writer.WriteEndElementAsync(); //content | sections
            await writer.WriteEndElementAsync(); //section
        }

        internal void SetSectionState(SectionState state)
        {
            sectionStates.Pop();
            sectionStates.Push(state);
        }

        internal SectionState GetSectionState()
        {
            return sectionStates.Peek();
        }

        internal int SectionLevel
        {
            get { return sectionStates.Count(); }
        }

        #endregion Section

        #region Code

        private async Task WriteIndentedCodeAsync(Block block)
        {
            await writer.WriteStartElementAsync("code");
            await writer.WriteAttributeStringAsync("language", "none");
            await writer.WriteRawAsync("\n");
            await writer.WriteRawAsync(block.StringContent.ToString());
            await writer.WriteEndElementAsync(); //code
        }

        private async Task WriteFencedCodeAsync(Block block)
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

        private async Task WriteLinkAsync(Inline inline)
        {
            if (Uri.IsWellFormedUriString(inline.TargetUrl, UriKind.Absolute))
                await WriteExternalLinkAsync(inline);
            else
                await WriteConceptualLinkAsync(inline);
        }

        internal virtual async Task WriteConceptualLinkAsync(Inline inline)
        {
            var href = GetConceptualLinkTarget(inline);
            await writer.WriteStartElementAsync(null, "link", null);
            await writer.WriteAttributeStringAsync("xlink", "href", "http://www.w3.org/1999/xlink", href);
            await WriteTopicTypeAttributeAsync();
            if (inline.FirstChild != null)
                await WriteChildInlinesAsync(inline);
            //else
            //    await writer.WriteStringAsync(inline.LiteralContent);
            await writer.WriteEndElementAsync(); //link
        }

        internal virtual async Task WriteExternalLinkAsync(Inline inline)
        {
            await writer.WriteStartElementAsync(null, "externalLink", null);
            await writer.WriteStartElementAsync(null, "linkUri", null);
            await writer.WriteStringAsync(inline.TargetUrl);
            await writer.WriteEndElementAsync(); //linkUri

            await writer.WriteStartElementAsync(null, "linkText", null);
            if (!string.IsNullOrEmpty(inline.LiteralContent))
                await writer.WriteStringAsync(inline.LiteralContent);
            else
                await WriteChildInlinesAsync(inline);
            await writer.WriteEndElementAsync(); //linkText

            await writer.WriteEndElementAsync(); //externalLink
        }

        internal virtual async Task WriteRelatedTopicsAsync(Block block)
        {
            if (block.ReferenceMap.Count > 0 && GetSectionState() != SectionState.SeeAlso)
                await writer.WriteStartElementAsync(null, "relatedTopics", null);
            if (block.ReferenceMap.Count > 0 || GetSectionState() == SectionState.SeeAlso)
            {
                foreach (var reference in block.ReferenceMap.Values)
                    await WriteReferenceLinkAsync(reference);
            }
            if (block.ReferenceMap.Count > 0 || GetSectionState() == SectionState.SeeAlso)
                await writer.WriteEndElementAsync();  //relatedTopics
        }

        private async Task WriteReferenceLinkAsync(Reference reference)
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

        private async Task WriteTopicTypeAttributeAsync()
        {
            if (GetSectionState() == SectionState.SeeAlso)
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

        private async Task WriteImageAsync(Inline inline)
        {
            if (inlineState == InlineState.Start)
                await WriteInlineImageAsync(inline);
            else
                await WriteBlockImageAsync(inline);
        }

        private async Task WriteInlineImageAsync(Inline inline)
        {
            await writer.WriteStartElementAsync(null, "mediaLinkInline", null);
            await writer.WriteStartElementAsync(null, "image", null);
            await writer.WriteAttributeStringAsync("xlink", "href", "http://www.w3.org/1999/xlink", inline.TargetUrl);
            await writer.WriteEndElementAsync(); //image
            await writer.WriteEndElementAsync(); //mediaLinkInline
        }

        private async Task WriteBlockImageAsync(Inline inline)
        {
            await writer.WriteStartElementAsync(null, "mediaLink", null);
            await writer.WriteStartElementAsync(null, "image", null);
            await writer.WriteAttributeStringAsync("xlink", "href", "http://www.w3.org/1999/xlink", inline.TargetUrl);
            await writer.WriteEndElementAsync(); //image
            if (inline.FirstChild != null)
                await WriteCaptionAsync(inline);
            await writer.WriteEndElementAsync(); //mediaLink
        }

        private async Task WriteCaptionAsync(Inline inline)
        {
            await writer.WriteStartElementAsync(null, "caption", null);
            await writer.WriteAttributeStringAsync(null, "placement", null, "after");
            await WriteChildInlinesAsync(inline);
            await writer.WriteEndElementAsync(); //caption
        }

        #endregion Image

        #region List

        internal virtual async Task WriteListAsync(Block block)
        {
            await WriteStartElementAsync("list");
            await WriteListClassAsync(block);
            await WriteChildBlocksAsync(block);
            await WriteEndElementAsync(); //list
        }

        internal virtual async Task WriteListItemAsync(Block block)
        {
            await WriteStartElementAsync("listItem");
            await WriteChildBlocksAsync(block);
            await WriteEndElementAsync(); //listItem
        }

        internal async Task WriteListClassAsync(Block block, string defaultClass = null)
        {
            var listClass = GetListClass(block, defaultClass);
            if (listClass != null)
                await WriteAttributeStringAsync("class", listClass);
        }

        private static string GetListClass(Block block, string defaultClass)
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
                    return defaultClass;
            }
        }

        #endregion

        #region Table

        private async Task WriteTableAsync(Block block)
        {
            await writer.WriteStartElementAsync("table");
            await WriteChildBlocksAsync(block);
            await writer.WriteEndElementAsync(); //table
        }

        private async Task WriteTableRowAsync(Block block)
        {
            await writer.WriteStartElementAsync("row");
            await WriteChildBlocksAsync(block);
            await writer.WriteEndElementAsync(); //row
        }

        private async Task WriteTableCellAsync(Block block)
        {
            await writer.WriteStartElementAsync("entry");
            await WriteChildInlinesAsync(block);
            //await WriteChildBlocksAsync(block);
            await writer.WriteEndElementAsync(); //entry
        }

        #endregion Table

        #region Private Members

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
