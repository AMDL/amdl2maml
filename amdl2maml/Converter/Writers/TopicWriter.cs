using Amdl.Metadata;
using CommonMark.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// https://html.spec.whatwg.org/multipage/forms.html#e-mail-state-(type=email)
        /// </summary>
        private static readonly Regex EmailRegex =
            new Regex(@"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$");

        private static readonly Assembly Assembly = typeof(TopicWriter).GetTypeInfo().Assembly;
        private static readonly Version Version = Assembly.GetName().Version;
        private static readonly string Copyright = Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>().Copyright;

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

        internal enum ListClass
        {
            NoBullet,
            Bullet,
            Ordered,
        }

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
        private BlockState blockState;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteStartElementAsync(string localName)
        {
            return writer.WriteStartElementAsync(null, localName, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteEndElementAsync()
        {
            return writer.WriteEndElementAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteElementStringAsync(string localName, string value)
        {
            return writer.WriteElementStringAsync(null, localName, null, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteAttributeStringAsync(string localName, string value)
        {
            return writer.WriteAttributeStringAsync(null, localName, null, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteStringAsync(string text)
        {
            return writer.WriteStringAsync(text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteRawAsync(string text)
        {
            return writer.WriteRawAsync(text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task WriteCommentAsync(string text)
        {
            return writer.WriteCommentAsync(text);
        }

        #endregion

        #region Document

        private async Task WriteDocumentAsync(Block block)
        {
            sectionStates = new Stack<SectionState>();
            sectionStates.Push(SectionState.None);

            await writer.WriteStartDocumentAsync()
                .ConfigureAwait(false);

            await WriteCommentAsync(" This document was generated by a tool. ");
            await WriteCommentAsync(" Changes to this file may cause incorrect behavior and will be lost if the code is regenerated. ");
            var versionString = string.Format(" amdl2maml Version {0}.{1}                  ", Version.Major, Version.Minor); //TODO Replace with just Version when stable
            await WriteCommentAsync(versionString);
            var copyrightString = string.Format(" {0:045} ", Copyright);
            await WriteCommentAsync(copyrightString);

            await WriteStartElementAsync("topic");
            await WriteAttributeStringAsync("id", Id.ToString());
            await WriteAttributeStringAsync("revisionNumber", "1");

            await writer.WriteStartElementAsync(null, GetDocElementName(), "http://ddue.schemas.microsoft.com/authoring/2003/5");
            await writer.WriteAttributeStringAsync("xmlns", "xlink", null, "http://www.w3.org/1999/xlink");

            await WriteStartSummaryAsync();

            await WriteChildBlocksAsync(block);

            await WriteEndSectionsAsync(2);
            await WriteEndSummaryAsync();
            await WriteEndIntroductionAsync();
            await WriteRelatedTopicsAsync(block);

            await WriteEndElementAsync(); //developer<TopicType>Document
            await WriteEndElementAsync(); //topic
            await writer.WriteEndDocumentAsync();
        }

        private async Task WriteStartSummaryAsync()
        {
            await WriteStartElementAsync("summary");
            topicState = TopicState.Summary;
        }

        private async Task WriteEndSummaryAsync()
        {
            if (topicState == TopicState.Summary)
            {
                await WriteEndElementAsync(); //summary
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
                await WriteEndElementAsync(); //introduction
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
                await WriteEndElementAsync(); //content | sections
            await WriteStartElementAsync("relatedTopics");
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
                    await WriteQuoteAsync(block);
                    break;

                case BlockTag.HtmlBlock:
                    await WriteMarkupBlockAsync(block);
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
#if !COMMON_MARK
                    throw new NotImplementedException();
#else
                    break;
#endif

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
            {
                await WriteStartElementAsync("para");
                if (blockState == BlockState.None)
                    blockState = BlockState.Start;
            }
            await WriteChildInlinesAsync(block);
            if (GetSectionState() != SectionState.SeeAlso)
                await WriteEndElementAsync(); //para
        }

        private async Task WriteQuoteAsync(Block block)
        {
            await WriteStartElementAsync("quote");
            await WriteChildBlocksAsync(block);
            await WriteEndElementAsync(); //quote
        }

        #endregion Block

        #region Inline

        private async Task WriteInlineAsync(Inline inline)
        {
            switch (inline.Tag)
            {
                case InlineTag.String:
                    await WriteStringAsync(inline.LiteralContent);
                    break;

                case InlineTag.Code:
                    await WriteCodeInlineAsync(inline);
                    break;

                case InlineTag.RawHtml:
                    await WriteMarkupInlineAsync(inline);
                    break;

                case InlineTag.Emphasis:
                    await WriteWeakEmphasisAsync(inline);
                    break;

                case InlineTag.Strong:
                    await WriteStrongEmphasisAsync(inline);
                    break;

                case InlineTag.Subscript:
                    await WriteSubscriptAsync(inline);
                    break;

                case InlineTag.Superscript:
                    await WriteSuperscriptAsync(inline);
                    break;

                case InlineTag.SoftBreak:
                    await WriteSoftLineBreakAsync();
                    break;

                case InlineTag.LineBreak:
                    await WriteHardLineBreakAsync();
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

        private async Task WriteChildInlinesAsync(Inline inline)
        {
            for (var child = inline.FirstChild; child != null; child = child.NextSibling)
                await WriteInlineAsync(child);
        }

        internal static string GetAllLiteralContent(Inline inline)
        {
            var content = string.Empty;
            for (; inline != null; inline = inline.NextSibling)
                content += inline.LiteralContent;
            return content;
        }

        #endregion Inline

        #region Emphasis

        private async Task WriteWeakEmphasisAsync(Inline inline)
        {
            if (inline.FirstChild != null && inline.FirstChild.Tag == InlineTag.Emphasis && inline.FirstChild.LastSibling == inline.FirstChild)
            {
                await WriteSpecialEmphasisAsync(inline);
                return;
            }
            await WriteStartElementAsync("legacyItalic");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //legacyItalic
        }

        private async Task WriteStrongEmphasisAsync(Inline inline)
        {
            await WriteStartElementAsync("legacyBold");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //legacyBold
        }

        private async Task WriteSpecialEmphasisAsync(Inline inline)
        {
            switch (inline.DelimiterCharacter)
            {
                case '_':
                    await WriteSpecialWeakEmphasisAsync(inline.FirstChild);
                    return;
                case '*':
                    await WriteSpecialStrongEmphasisAsync(inline.FirstChild);
                    return;
                default:
                    throw new InvalidOperationException();
            }
        }

        private async Task WriteSpecialWeakEmphasisAsync(Inline inline)
        {
            await WriteStartElementAsync("legacyUnderline");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //legacyUnderline
        }

        private async Task WriteSpecialStrongEmphasisAsync(Inline inline)
        {
            await WriteStartElementAsync("literal");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //literal
        }

        #endregion Emphasis

        #region Misc

        private async Task WriteCodeInlineAsync(Inline inline)
        {
            await WriteStartElementAsync("codeInline");
            await WriteStringAsync(inline.LiteralContent);
            await WriteEndElementAsync();
        }

        private async Task WriteSubscriptAsync(Inline inline)
        {
            await WriteStartElementAsync("subscript");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //subscript;
        }

        private async Task WriteSuperscriptAsync(Inline inline)
        {
            await WriteStartElementAsync("superscript");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //superscript;
        }

        private async Task WriteStrikethroughAsync(Inline inline)
        {
            await WriteStartMarkupInlineAsync();
            await WriteStartElementAsync("s");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //s
        }

        private async Task WriteSoftLineBreakAsync()
        {
            if (GetSectionState() != SectionState.SeeAlso)
                await WriteRawAsync("\n");
        }

        private async Task WriteHardLineBreakAsync()
        {
            if (GetSectionState() != SectionState.SeeAlso)
            {
                await WriteStartElementAsync("markup");
                await WriteElementStringAsync("br", null);
                await WriteEndElementAsync(); //markup
            }
        }

        #endregion Misc

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

            while (block.HeaderLevel > SectionLevel)
            {
                var state = await DoWriteStartSectionAsync(block, title);
                sectionStates.Push(state);
            }
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
            if (block.HeaderLevel == SectionLevel + 1)
            {
                await WriteAttributeStringAsync("address", title);
                await WriteTitleAsync(block);
            }
            await WriteStartElementAsync("content");
            await WriteAutoOutlineAsync(block);

            blockState = BlockState.None;

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
            return GetAllLiteralContent(block.InlineContent);
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
            await WriteEndElementAsync(); //content | sections
            await WriteEndElementAsync(); //section
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
            var content = block.StringContent.ToString().Trim('\n');
            if (!content.Cast<char>().Contains('\n'))
                await WriteCommandAsync(content);
            else
                await WriteCodeAsync(content, null);
        }

        private async Task WriteFencedCodeAsync(Block block)
        {
            var content = block.StringContent.ToString();
            var info = block.FencedCodeData.Info;
            await WriteCodeAsync(content, info);
        }

        private async Task WriteCommandAsync(string content)
        {
            await WriteStartElementAsync("para");
            await WriteStartElementAsync("command");
            await WriteStringAsync(content);
            await WriteEndElementAsync(); //command
            await WriteEndElementAsync(); //para
        }

        private async Task WriteCodeAsync(string content, string info)
        {
            await WriteStartElementAsync("code");
            await WriteCodeAttributesAsync(info);
            await WriteRawAsync("\n");
            await WriteStringAsync(content.ToString());
            await WriteEndElementAsync(); //code
        }

        private async Task WriteCodeAttributesAsync(string info)
        {
            if (!string.IsNullOrEmpty(info))
            {
                if (Languages.Contains(info))
                {
                    await WriteAttributeStringAsync("language", info);
                    return;
                }

                await WriteAttributeStringAsync("title", info);
            }

            await WriteAttributeStringAsync("language", "none");
        }

        #endregion Code

        #region Link

        private async Task WriteLinkAsync(Inline inline)
        {
            var target = inline.TargetUrl;
            //TODO Move to parser
            if (target.StartsWith("@"))
                await WriteNewTermAsync(inline);
            else
                await WriteLinkAsync(inline, target);
        }

        private async Task WriteNewTermAsync(Inline inline)
        {
            await WriteStartElementAsync("newTerm");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //newTerm
        }

        private async Task WriteLinkAsync(Inline inline, string target)
        {
            var extTargetUrl = GetExternalLinkTarget(target);
            if (extTargetUrl != null)
                await WriteExternalLinkAsync(inline, extTargetUrl);
            else if (target.Length >= 2 && target[1] == ':')
                await WriteCodeLinkAsync(inline);
            else
                await WriteConceptualLinkAsync(inline);
        }

        internal virtual async Task WriteConceptualLinkAsync(Inline inline)
        {
            var target = GetConceptualLinkTarget(inline.TargetUrl);
            await WriteStartElementAsync("link");
            await WriteLinkTargetAsync(target);
            await WriteTopicTypeAttributeAsync();
            if (inline.FirstChild != null)
                await WriteChildInlinesAsync(inline);
            //else
            //    await WriteStringAsync(inline.LiteralContent);
            await WriteEndElementAsync(); //link
        }

        private async Task WriteCodeLinkAsync(Inline inline)
        {
            await WriteStartElementAsync("codeEntityReference");
            var linkText = GetAllLiteralContent(inline.FirstChild);
            await WriteAttributeStringAsync("linkText", linkText);
            await WriteStringAsync(inline.TargetUrl);
            await WriteEndElementAsync(); //codeEntityReference
        }

        internal virtual async Task WriteExternalLinkAsync(Inline inline, string target)
        {
            await WriteStartElementAsync("externalLink");
            await WriteStartElementAsync("linkUri");
            await WriteStringAsync(target);
            await WriteEndElementAsync(); //linkUri

            await WriteStartElementAsync("linkText");
            if (!string.IsNullOrEmpty(inline.LiteralContent))
                await WriteStringAsync(inline.LiteralContent);
            else
                await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //linkText

            await WriteEndElementAsync(); //externalLink
        }

        internal virtual async Task WriteRelatedTopicsAsync(Block block)
        {
            if (block.ReferenceMap.Count > 0 && GetSectionState() != SectionState.SeeAlso)
                await WriteStartElementAsync("relatedTopics");
            if (block.ReferenceMap.Count > 0 || GetSectionState() == SectionState.SeeAlso)
            {
                foreach (var reference in block.ReferenceMap.Values)
                    await WriteReferenceLinkAsync(reference);
            }
            if (block.ReferenceMap.Count > 0 || GetSectionState() == SectionState.SeeAlso)
                await WriteEndElementAsync();  //relatedTopics
        }

        private async Task WriteReferenceLinkAsync(Reference reference)
        {
            await WriteStartElementAsync("externalLink");
            await WriteStartElementAsync("linkUri");
            await WriteStringAsync(reference.Url);
            await WriteEndElementAsync(); //linkUri

            var text = string.IsNullOrEmpty(reference.Title)
                ? reference.Label
                : reference.Title;
            await WriteElementStringAsync("linkText", text);

            await WriteEndElementAsync(); //externalLink
        }

        private static string GetExternalLinkTarget(string target)
        {
            if (Uri.IsWellFormedUriString(target, UriKind.Absolute))
                return target;
            if (EmailRegex.IsMatch(target))
                return "mailto:" + target;
            return null;
        }

        internal string GetConceptualLinkTarget(string target)
        {
            var split = target.Split('#');
            if (split[0].Length > 0)
            {
                TopicData topic;
                if (!name2topic.TryGetValue(split[0], out topic))
                    throw new InvalidOperationException("Cannot find topic " + split[0] + ", source: " + this.topic.FileName);
                return topic.Id.ToString();
            }
            return '#' + split[1];
        }

        private Task WriteLinkTargetAsync(string target)
        {
            return writer.WriteAttributeStringAsync("xlink", "href", "http://www.w3.org/1999/xlink", target);
        }

        private async Task WriteTopicTypeAttributeAsync()
        {
            if (GetSectionState() == SectionState.SeeAlso)
            {
                Guid guid = GetGroupId();
                if (guid != default(Guid))
                    await WriteAttributeStringAsync("topicType_id", guid.ToString());
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
            if (inlineState == InlineState.Start || inline.NextSibling != null)
                await WriteInlineImageAsync(inline);
            else
                await WriteBlockImageAsync(inline);
        }

        private async Task WriteInlineImageAsync(Inline inline)
        {
            await WriteStartElementAsync("mediaLinkInline");
            await WriteStartElementAsync("image");
            await WriteLinkTargetAsync(inline.TargetUrl);
            await WriteEndElementAsync(); //image
            await WriteEndElementAsync(); //mediaLinkInline
        }

        private async Task WriteBlockImageAsync(Inline inline)
        {
            await WriteStartElementAsync("mediaLink");
            await WriteStartElementAsync("image");
            await WriteLinkTargetAsync(inline.TargetUrl);
            await WriteEndElementAsync(); //image
            if (inline.FirstChild != null)
                await WriteCaptionAsync(inline);
            await WriteEndElementAsync(); //mediaLink
        }

        private async Task WriteCaptionAsync(Inline inline)
        {
            await WriteStartElementAsync("caption");
            await WriteAttributeStringAsync("placement", "after");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //caption
        }

        #endregion Image

        #region List

        internal virtual async Task WriteListAsync(Block block)
        {
            var listClass = GetListClass(block);
            var isParagraph = SectionLevel > 2 && blockState == BlockState.None
                && (listClass == ListClass.Ordered || listClass == ListClass.Bullet);
            if (isParagraph)
            {
                await WriteStartElementAsync("para");
                blockState = BlockState.Start;
            }

            await WriteStartElementAsync("list");
            await WriteListClassAsync(listClass);
            await WriteChildBlocksAsync(block);
            await WriteEndElementAsync(); //list

            if (isParagraph)
                await WriteEndElementAsync(); //para
        }

        internal virtual async Task WriteListItemAsync(Block block)
        {
            await WriteStartElementAsync("listItem");
            await WriteChildBlocksAsync(block);
            await WriteEndElementAsync(); //listItem
        }

        internal async Task WriteListClassAsync(Block block, ListClass? defaultClass = null)
        {
            var listClass = GetListClass(block, defaultClass);
            await WriteListClassAsync(listClass);
        }

        private async Task WriteListClassAsync(ListClass? listClass)
        {
            if (listClass != null)
                await WriteAttributeStringAsync("class", listClass.ToString().ToLowerInvariant());
        }

        private static ListClass? GetListClass(Block block, ListClass? defaultClass = null)
        {
            switch (block.ListData.ListType)
            {
                case ListType.Bullet:
                    if (block.ListData.BulletChar == '*')
                        return ListClass.Bullet;
                    return ListClass.NoBullet;

                case ListType.Ordered:
                    return ListClass.Ordered;

                default:
                    return defaultClass;
            }
        }

        #endregion

        #region Table

        private async Task WriteTableAsync(Block block)
        {
            await WriteStartElementAsync("table");
            await WriteChildBlocksAsync(block);
            await WriteEndElementAsync(); //table
        }

        private async Task WriteTableRowAsync(Block block)
        {
            await WriteStartElementAsync("row");
            await WriteChildBlocksAsync(block);
            await WriteEndElementAsync(); //row
        }

        private async Task WriteTableCellAsync(Block block)
        {
            await WriteStartElementAsync("entry");
            await WriteChildInlinesAsync(block);
            //await WriteChildBlocksAsync(block);
            await WriteEndElementAsync(); //entry
        }

        #endregion Table

        #region HTML

        private async Task WriteMarkupBlockAsync(Block block)
        {
            var content = block.StringContent.ToString();
            if (await TryWriteCommentAsync(content))
                return;
            await WriteStartElementAsync("markup");
            await WriteRawAsync("\n");
            await WriteRawAsync(content);
            await WriteEndElementAsync(); //markup
        }

        private async Task WriteMarkupInlineAsync(Inline inline)
        {
            var content = inline.LiteralContent;
            if (await TryWriteCommentAsync(content))
                return;
            await WriteStartMarkupInlineAsync();
            await WriteRawAsync(inline.LiteralContent);
            await WriteChildInlinesAsync(inline);
        }

        /// <summary>
        /// http://www.w3.org/TR/html5/syntax.html#comments
        /// </summary>
        private async Task<bool> TryWriteCommentAsync(string content)
        {
            var trim = content.Trim();
            if (!trim.StartsWith("<!--") || !trim.EndsWith("-->"))
                return false;
            var text = trim.Substring("<!--".Length, trim.Length - ("<!--".Length + "-->".Length));
            if (text.StartsWith(">") || text.StartsWith("->") || text.EndsWith("-") || text.Contains("--"))
                return false;
            await WriteRawAsync(trim);
            return true;
        }

        private async Task WriteStartMarkupInlineAsync()
        {
            if (!IsInMarkupInline)
            {
                await WriteStartElementAsync("markup");
                markupState = MarkupState.Inline;
            }
        }

        private async Task WriteEndMarkupInlineAsync()
        {
            if (IsInMarkupInline)
            {
                await WriteEndElementAsync(); //markup
                markupState = MarkupState.None;
            }
        }

        #endregion HTML

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
