﻿using Amdl.Metadata;
using CommonMark.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter.Writers
{
    internal enum ListClass
    {
        NoBullet,
        Bullet,
        Ordered,
    }

    /// <summary>
    /// AMDL topic writer.
    /// </summary>
    internal abstract class TopicWriter : WriterBase
    {
        #region Static Members

        private const string RelatedTopics = "relatedTopics";

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

        private static readonly IDictionary<string, SeeAlsoGroup> SeeAlsoGroups = new Dictionary<string, SeeAlsoGroup>(4)
        {
            { Properties.Resources.ConceptsTitle, SeeAlsoGroup.Concepts },
            { Properties.Resources.OtherResourcesTitle, SeeAlsoGroup.OtherResources },
            { Properties.Resources.ReferenceTitle, SeeAlsoGroup.Reference },
            { Properties.Resources.TasksTitle, SeeAlsoGroup.Tasks },
        };

        private static readonly IDictionary<SeeAlsoGroup, Guid> SeeAlsoGroupIds = new Dictionary<SeeAlsoGroup, Guid>(5)
        {
            { SeeAlsoGroup.None, Guid.Empty },
            { SeeAlsoGroup.Concepts, new Guid("1FE70836-AA7D-4515-B54B-E10C4B516E50") },
            { SeeAlsoGroup.OtherResources, new Guid("4A273212-0AC8-4D72-8349-EC11CD2FF8CD") },
            { SeeAlsoGroup.Reference, new Guid("A635375F-98C2-4241-94E7-E427B47C20B6") },
            { SeeAlsoGroup.Tasks, new Guid("DAC3A6A0-C863-4E5B-8F65-79EFC6A4BA09") },
        };

        private static readonly IDictionary<string, SeeAlsoGroup> SeeAlsoElements = new Dictionary<string, SeeAlsoGroup>(5)
        {
            { RelatedTopics, SeeAlsoGroup.None },
            { ToCamelCase(SeeAlsoGroup.Concepts.ToString()), SeeAlsoGroup.Concepts },
            { ToCamelCase(SeeAlsoGroup.OtherResources.ToString()), SeeAlsoGroup.OtherResources },
            { ToCamelCase(SeeAlsoGroup.Reference.ToString()), SeeAlsoGroup.Reference },
            { ToCamelCase(SeeAlsoGroup.Tasks.ToString()), SeeAlsoGroup.Tasks },
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
        /// <param name="writer">Writer.</param>
        /// <param name="topic">The topic.</param>
        /// <param name="name2topic">Mapping from topic name to data.</param>
        /// <param name="paths">Paths.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Asynchronous task.</returns>
        public static async Task WriteAsync(TopicData topic, IDictionary<string, TopicData> name2topic, StreamWriter writer, Paths paths, CancellationToken cancellationToken)
        {
            var parserResult = topic.ParserResult;
            if (parserResult == null)
            {
                parserResult = await TopicParser.ParseAsync(topic, paths, cancellationToken);
            }

            var xmlSettings = new XmlWriterSettings
            {
                Async = true,
                Indent = true,
            };

            using (var xmlWriter = XmlWriter.Create(writer, xmlSettings))
            {
                var topicWriter = TopicWriter.Create(topic, parserResult, name2topic, xmlWriter);
                await topicWriter.WriteAsync();
            }
        }

        private static TopicWriter Create(TopicData topic, TopicParserResult parserResult, IDictionary<string, TopicData> name2topic, XmlWriter writer)
        {
            switch (topic.Type)
            {
                case TopicType.Empty:
                    return new EmptyTopicWriter(topic, parserResult, name2topic, writer);
                case TopicType.General:
                    return new GeneralTopicWriter(topic, parserResult, name2topic, writer);
                case TopicType.Glossary:
                    return new GlossaryTopicWriter(topic, parserResult, name2topic, writer);
                case TopicType.HowTo:
                    return new HowToTopicWriter(topic, parserResult, name2topic, writer);
                case TopicType.Orientation:
                    return new OrientationTopicWriter(topic, parserResult, name2topic, writer);
                default:
                    throw new InvalidOperationException("Unexpected topic type: " + topic.Type);
            }
        }

        #endregion

        #region Fields

        private readonly TopicData topic;
        private readonly Block document;
        private readonly IDictionary<string, TopicData> name2topic;
        private readonly WriterState state;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TopicWriter"/> class.
        /// </summary>
        /// <param name="topic">Current topic.</param>
        /// <param name="parserResult">Parser result.</param>
        /// <param name="name2topic">Mapping from topic name to data.</param>
        /// <param name="writer">XML writer.</param>
        protected TopicWriter(TopicData topic, TopicParserResult parserResult, IDictionary<string, TopicData> name2topic, XmlWriter writer)
            : base(writer)
        {
            this.topic = topic;
            this.document = parserResult.Document;
            this.name2topic = name2topic;
            this.state = new WriterState();
            this.commandWriter = new Lazy<Writers.CommandWriter>(CreateCommandWriter);
            this.containerElementNames = new Lazy<IEnumerable<string>>(GetContainerElementNames);
            this.includeSeeAlso = new Lazy<bool>(GetIncludeSeeAlso);
        }

        #endregion

        #region Public Members

        /// <summary>
        /// Writes the topic as MAML.
        /// </summary>
        /// <returns>Asynchronous task.</returns>
        public virtual async Task WriteAsync()
        {
            await WriteDocumentAsync();
        }

        #endregion

        #region Internal Members

        internal abstract string GetDocElementName();

        internal virtual IEnumerable<string> GetContainerElementNames()
        {
            return SeeAlsoElements.Keys;
        }

        #endregion

        #region Document

        private async Task WriteDocumentAsync()
        {
            await WriteStartDocumentAsync()
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

            await WriteStartElementAsync(null, GetDocElementName(), "http://ddue.schemas.microsoft.com/authoring/2003/5");
            await WriteAttributeStringAsync("xmlns", "xlink", null, "http://www.w3.org/1999/xlink");

            await WriteStartSummaryAsync();

            await WriteChildBlocksAsync(document);

            await WriteEndSectionsAsync(2);
            await WriteEndSummaryAsync();
            await WriteEndIntroductionAsync();
            await WriteRelatedTopicsAsync(document);

            await WriteEndElementAsync(); //developer<TopicType>Document
            await WriteEndElementAsync(); //topic
            await WriteEndDocumentAsync();
        }

        private async Task WriteStartSummaryAsync()
        {
            await WriteStartElementAsync("summary");
            TopicState = TopicState.Summary;
        }

        private async Task WriteEndSummaryAsync()
        {
            if (TopicState == TopicState.Summary)
            {
                await WriteEndElementAsync(); //summary
                TopicState = TopicState.Content;
            }
        }

        internal virtual async Task WriteStartIntroductionAsync(Block block)
        {
            await WriteStartElementAsync("introduction");
            await WriteAutoOutlineAsync(block);

            TopicState = TopicState.Introduction;
        }

        internal virtual async Task WriteEndIntroductionAsync()
        {
            if (TopicState == TopicState.Introduction)
            {
                await WriteEndElementAsync(); //introduction
                TopicState = TopicState.Content;
            }
        }

        private async Task WriteStartSeeAlsoAsync(int level)
        {
            await DoWriteStartSeeAlso(level);
            SetSectionState(SectionState.SeeAlso);
            SeeAlsoGroup = SeeAlsoGroup.None;
        }

        internal virtual async Task DoWriteStartSeeAlso(int level)
        {
            await WriteEndSectionsAsync(level);
            if (level > 2)
                await WriteEndElementAsync(); //content | sections
            await WriteStartElementAsync(RelatedTopics);
        }

        private void WriteStartSeeAlsoGroup(string title)
        {
            SeeAlsoGroup group;
            if (SeeAlsoGroups.TryGetValue(title, out group))
                SeeAlsoGroup = group;
            else
                SeeAlsoGroup = SeeAlsoGroup.None;
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
                case BlockTag.CustomContainer:
                    await WriteContainerAsync(block);
                    break;

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
            var isParagraph = GetSectionState() != SectionState.SeeAlso;
            if (isParagraph)
                await WriteStartParagraphAsync();
            await WriteChildInlinesAsync(block);
            if (isParagraph)
                await WriteEndElementAsync(); //para
        }

        private async Task WriteStartParagraphAsync()
        {
            await WriteStartElementAsync("para");
            if (BlockState == BlockState.None)
                BlockState = BlockState.Start;
        }

        private async Task WriteContainerAsync(Block block)
        {
            var info = block.FencedCodeData.Info;
            if (string.IsNullOrEmpty(info) || !ContainerElementNames.Contains(info))
                await WriteAlertAsync(block, info);
            else
                await WriteElementAsync(block, info);
        }

        private async Task WriteElementAsync(Block block, string name)
        {
            await WriteEndIntroductionAsync();
            var isSeeAlso = SeeAlsoElements.ContainsKey(name);
            if (isSeeAlso)
            {
                await WriteStartSeeAlsoAsync(SectionLevel);
                SeeAlsoGroup = SeeAlsoElements[name];
            }
            else
                await WriteStartElementAsync(name);
            await WriteChildInlinesAsync(block);
            if (!isSeeAlso)
                await WriteEndElementAsync();
        }

        private async Task WriteAlertAsync(Block block, string info)
        {
            await WriteStartElementAsync("alert");
            if (!string.IsNullOrEmpty(info))
                await WriteAttributeStringAsync("class", info);
            await WriteChildInlinesAsync(block);
            await WriteEndElementAsync(); //alert
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
                    await WriteStringAsync(inline);
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

                case InlineTag.Math:
                    await WriteMathAsync(inline);
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
                InlineState = InlineState.Start;
            }
            InlineState = InlineState.None;
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

        private async Task WriteMathAsync(Inline inline)
        {
            await WriteStartElementAsync("math");
            await WriteChildInlinesAsync(inline);
            await WriteEndElementAsync(); //math;
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
                SectionStates.Push(state);
            }
        }

        internal virtual async Task<SectionState> DoWriteStartSectionAsync(Block block, string title)
        {
            var state = GetSectionState();
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

            BlockState = BlockState.None;

            return SectionState.Content;
        }

        private async Task WriteAutoOutlineAsync(Block block)
        {
            if (block.Tag == BlockTag.SETextHeader)
            {
                await WriteStartElementAsync("autoOutline");
                await WriteAttributeStringAsync("lead", "none");
                if (!IncludeSeeAlso)
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
            while (SectionStates.Count() >= level)
            {
                var state = SectionStates.Pop();
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
            SectionStates.Pop();
            SectionStates.Push(state);
        }

        internal SectionState GetSectionState()
        {
            return SectionStates.Peek();
        }

        internal int SectionLevel
        {
            get { return SectionStates.Count(); }
        }

        #endregion Section

        #region Code

        private async Task WriteIndentedCodeAsync(Block block)
        {
            var content = GetAllLiteralContent(block.InlineContent);
            if (!content.Cast<char>().Contains('\n'))
                await WriteCommandAsync(block);
            else
                await WriteCodeAsync(content, null);
        }

        private async Task WriteFencedCodeAsync(Block block)
        {
            var content = block.StringContent.ToString();
            var info = block.FencedCodeData.Info;
            await WriteCodeAsync(content, info);
        }

        private async Task WriteCommandAsync(Block block)
        {
            await CommandWriter.WriteAsync(block);
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
                await WriteCodeLinkAsync(inline, target);
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

        private async Task WriteCodeLinkAsync(Inline inline, string target)
        {
            if (target[0] == 'U')
                await WriteUnmanagedCodeLinkAsync(target.Substring(2));
            else
                await WriteManagedCodeLinkAsync(inline);
        }

        private async Task WriteUnmanagedCodeLinkAsync(string target)
        {
            await WriteStartElementAsync("unmanagedCodeEntityReference");
            await WriteStringAsync(target);
            await WriteEndElementAsync(); //unmanagedCodeEntityReference
        }

        private async Task WriteManagedCodeLinkAsync(Inline inline)
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
                await WriteStartElementAsync(RelatedTopics);
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
                return GetInnerLinkTarget(target, split);
            return '#' + split[1];
        }

        private string GetInnerLinkTarget(string target, string[] split)
        {
            TopicData topic;
            if (!name2topic.TryGetValue(split[0], out topic))
                throw new InvalidOperationException("Cannot find topic " + split[0] + ", source: " + this.topic.FileName);
            target = topic.Id.ToString();
            if (split.Length > 1)
                target += '#' + split[1];
            return target;
        }

        private Task WriteLinkTargetAsync(string target)
        {
            return WriteAttributeStringAsync("xlink", "href", "http://www.w3.org/1999/xlink", target);
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
            return SeeAlsoGroupIds[SeeAlsoGroup];
        }

        #endregion Link

        #region Image

        private async Task WriteImageAsync(Inline inline)
        {
            if (InlineState == InlineState.Start || inline.NextSibling != null)
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
            var isParagraph = SectionLevel > 2 && BlockState == BlockState.None
                && (listClass == ListClass.Ordered || listClass == ListClass.Bullet);
            if (isParagraph)
                await WriteStartParagraphAsync();

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
                MarkupState = MarkupState.Inline;
            }
        }

        private async Task WriteEndMarkupInlineAsync()
        {
            if (IsInMarkupInline)
            {
                await WriteEndElementAsync(); //markup
                MarkupState = MarkupState.None;
            }
        }

        #endregion HTML

        #region Private Members

        private bool IsInMarkupInline
        {
            get { return MarkupState == MarkupState.Inline; }
        }

        private TopicData Topic
        {
            get { return topic; }
        }

        private Guid Id
        {
            get { return Topic.Id; }
        }

        private TopicState TopicState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return state.TopicState;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                state.TopicState = value;
            }
        }

        private Stack<SectionState> SectionStates
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return state.SectionStates;
            }
        }

        private BlockState BlockState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return state.BlockState;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                state.BlockState = value;
            }
        }

        private InlineState InlineState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return state.InlineState;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                state.InlineState = value;
            }
        }

        private MarkupState MarkupState
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return state.MarkupState;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                state.MarkupState = value;
            }
        }

        private SeeAlsoGroup SeeAlsoGroup
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return state.SeeAlsoGroup;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                state.SeeAlsoGroup = value;
            }
        }

        private Lazy<bool> includeSeeAlso;
        private bool IncludeSeeAlso
        {
            get { return includeSeeAlso.Value; }
        }

        private Lazy<IEnumerable<string>> containerElementNames;
        private IEnumerable<string> ContainerElementNames
        {
            get { return containerElementNames.Value; }
        }

        private static string ToCamelCase(string groupName)
        {
            return char.ToLower(groupName[0]) + groupName.Substring(1);
        }

        private Lazy<CommandWriter> commandWriter;
        private CommandWriter CommandWriter
        {
            get { return commandWriter.Value; }
        }

        private Writers.CommandWriter CreateCommandWriter()
        {
            return new CommandWriter(writer);
        }

        private bool GetIncludeSeeAlso()
        {
            for (var block = document.FirstChild; block != null; block = block.NextSibling)
                if (block.Tag == BlockTag.SETextHeader && block.HeaderLevel == 2 && GetTitle(block).Equals(Properties.Resources.SeeAlsoTitle))
                    return true;
            return false;
        }

        #endregion
    }
}
