﻿using CommonMark;
using CommonMark.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// AMDL topic converter.
    /// </summary>
    public abstract class TopicConverter
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

        private static readonly Version Version = typeof(TopicConverter).GetTypeInfo().Assembly.GetName().Version;

        /// <summary>
        /// Converts the topic to MAML.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="writer">Writer.</param>
        /// <param name="topic">The topic.</param>
        /// <param name="name2topic">Mapping from topic name to data.</param>
        /// <returns>Asynchronous task.</returns>
        public static Task ConvertAsync(TopicData topic, IDictionary<string, TopicData> name2topic, StreamReader reader, StreamWriter writer)
        {
            var converter = Create(topic, name2topic);
            return converter.ConvertAsync(reader, writer);
        }

        private static TopicConverter Create(TopicData topic, IDictionary<string, TopicData> name2topic)
        {
            switch (topic.Type)
            {
                case TopicType.Conceptual:
                    return new ConceptualTopicConverter(topic, name2topic);
                case TopicType.Orientation:
                    return new OrientationTopicConverter(topic, name2topic);
                default:
                    throw new InvalidOperationException("Unexpected topic type: " + topic.Type);
            }
        }

        private static Block Parse(TextReader reader)
        {
            var settings = CommonMarkSettings.Default.Clone();
            settings.AdditionalFeatures = CommonMarkAdditionalFeatures.None
                | CommonMarkAdditionalFeatures.StrikethroughTilde
                | CommonMarkAdditionalFeatures.SubscriptTilde
                | CommonMarkAdditionalFeatures.SuperscriptCaret;
            var doc = CommonMarkConverter.Parse(reader, settings);
            return doc;
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

        enum SectionState
        {
            None,
            Content,
            Sections,
        }

        enum InlineState
        {
            None,
            Start,
        }

        #endregion

        #region Fields

        private readonly TopicData topic;
        private readonly IDictionary<string, TopicData> name2topic;

        private TopicState topicState;
        private Stack<SectionState> sectionStates;
        private InlineState inlineState;

        private bool isMarkupInline;
        private bool isInSeeAlso;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="TopicConverter"/> class.
        /// </summary>
        /// <param name="topic">Current topic.</param>
        /// <param name="name2topic">Mapping from topic name to data.</param>
        protected TopicConverter(TopicData topic, IDictionary<string, TopicData> name2topic)
        {
            this.topic = topic;
            this.name2topic = name2topic;
        }

        #endregion

        #region Public Members

        /// <summary>
        /// Converts the topic to MAML.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="writer">Writer.</param>
        /// <returns>Asynchronous task.</returns>
        public Task ConvertAsync(TextReader reader, TextWriter writer)
        {
            var doc = Parse(reader);
            return WriteDocumentAsync(doc, writer);
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

        private async Task WriteDocumentAsync(Block doc, TextWriter writer)
        {
            if (doc.Tag != BlockTag.Document)
                throw new InvalidOperationException("Unexpected block tag: " + doc.Tag);

            var xmlSettings = new XmlWriterSettings
            {
                Async = true,
                Indent = true,
            };

            using (var xmlWriter = XmlWriter.Create(writer, xmlSettings))
            {
                await DoWriteDocumentAsync(doc, xmlWriter);
            }
        }

        private async Task DoWriteDocumentAsync(Block block, XmlWriter writer)
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

        private async Task WriteStartIntroductionAsync(Block block, XmlWriter writer)
        {
            SetTopicTitle(block);
            await writer.WriteStartElementAsync(null, "introduction", null);
            if (block.Tag == BlockTag.SETextHeader)
                await writer.WriteElementStringAsync(null, "autoOutline", null, null);
            topicState = TopicState.Introduction;
        }

        private async Task WriteEndIntroductionAsync(XmlWriter writer)
        {
            if (topicState == TopicState.Introduction)
            {
                await writer.WriteEndElementAsync(); //introduction
                topicState = TopicState.Content;
            }
        }

        private async Task WriteStartSeeAlsoAsync(XmlWriter writer)
        {
            await WriteEndSectionsAsync(2, writer);
            await writer.WriteStartElementAsync(null, "relatedTopics", null);
            isInSeeAlso = true;
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
                    await writer.WriteStartElementAsync(null, "code", null);
                    await writer.WriteRawAsync("\n");
                    await writer.WriteRawAsync(block.StringContent.ToString());
                    await writer.WriteEndElementAsync(); //code
                    break;

                case BlockTag.FencedCode:
                    await writer.WriteStartElementAsync(null, "code", null);
                    if (!string.IsNullOrEmpty(block.FencedCodeData.Info))
                    {
                        if (Languages.Contains(block.FencedCodeData.Info))
                            await writer.WriteAttributeStringAsync(null, "language", null, block.FencedCodeData.Info);
                        else
                            await writer.WriteAttributeStringAsync(null, "title", null, block.FencedCodeData.Info);
                    }
                    await writer.WriteRawAsync("\n");
                    await writer.WriteStringAsync(block.StringContent.ToString());
                    await writer.WriteEndElementAsync(); //code
                    break;

                case BlockTag.List:
                    await WriteListAsync(block, writer);
                    break;

                case BlockTag.ListItem:
                    await WriteListItemAsync(block, writer);
                    break;

                case BlockTag.ReferenceDefinition:
                    break;

                case BlockTag.HorizontalRuler:
                    break;

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
            if (!isInSeeAlso)
                await writer.WriteStartElementAsync(null, "para", null);
            for (var inline = block.InlineContent; inline != null; inline = inline.NextSibling)
            {
                await WriteInlineAsync(inline, writer);
                inlineState = InlineState.Start;
            }
            inlineState = InlineState.None;
            await WriteEndMarkupInlineAsync(writer);
            if (!isInSeeAlso)
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

        private async Task WriteStrikethroughAsync(Inline inline, XmlWriter writer)
        {
            await WriteStartMarkupInlineAsync(writer);
            await writer.WriteStartElementAsync(null, "s", null);
            await WriteChildInlinesAsync(inline, writer);
            await writer.WriteEndElementAsync(); //s
        }

        private async Task WriteStartMarkupInlineAsync(XmlWriter writer)
        {
            if (!isMarkupInline)
            {
                await writer.WriteStartElementAsync(null, "markup", null);
                isMarkupInline = true;
            }
        }

        private async Task WriteEndMarkupInlineAsync(XmlWriter writer)
        {
            if (isMarkupInline)
            {
                await writer.WriteEndElementAsync(); //markup
                isMarkupInline = false;
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

            if (isInSeeAlso)
                return;

            var title = block.InlineContent.LiteralContent;
            if (title.Equals(Properties.Resources.SeeAlsoTitle))
            {
                await WriteStartSeeAlsoAsync(writer);
                return;
            }

            await DoWriteStartSectionAsync(block, writer);
        }

        private async Task DoWriteStartSectionAsync(Block block, XmlWriter writer)
        {
            await WriteEndSectionsAsync(block.HeaderLevel, writer);

            var state = sectionStates.Peek();
            if (state == SectionState.Content)
            {
                await writer.WriteEndElementAsync(); //content
                await writer.WriteStartElementAsync(null, "sections", null);
                sectionStates.Pop();
                sectionStates.Push(SectionState.Sections);
            }

            var title = block.InlineContent.LiteralContent;
            await writer.WriteStartElementAsync(null, "section", null);
            await writer.WriteAttributeStringAsync(null, "address", null, title);
            await writer.WriteElementStringAsync(null, "title", null, title);
            await writer.WriteStartElementAsync(null, "content", null);

            if (block.Tag == BlockTag.SETextHeader)
                await writer.WriteElementStringAsync(null, "autoOutline", null, null);

            sectionStates.Push(SectionState.Content);
        }

        private async Task WriteEndSectionsAsync(int level, XmlWriter writer)
        {
            while (sectionStates.Count() >= level)
            {
                var state = sectionStates.Pop();
                await writer.WriteEndElementAsync(); //content | sections
                await writer.WriteEndElementAsync(); //section
            }
        }

        #endregion Section

        #region Link

        private async Task WriteLinkAsync(Inline inline, XmlWriter writer)
        {
            if (Uri.IsWellFormedUriString(inline.TargetUrl, UriKind.Absolute))
                await WriteExternalLinkAsync(inline, writer);
            else
                await WriteConceptualLinkAsync(inline, writer);
        }

        private async Task WriteConceptualLinkAsync(Inline inline, XmlWriter writer)
        {
            string href = inline.TargetUrl.StartsWith("#")
                ? inline.TargetUrl
                : name2topic[inline.TargetUrl].Id.ToString();
            await writer.WriteStartElementAsync(null, "link", null);
            await writer.WriteAttributeStringAsync("xlink", "href", "http://www.w3.org/1999/xlink", href);
            //await writer.WriteStringAsync(inline.LiteralContent);
            await WriteChildInlinesAsync(inline, writer);
            await writer.WriteEndElementAsync(); //link
        }

        private async Task WriteExternalLinkAsync(Inline inline, XmlWriter writer)
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

        private async Task WriteRelatedTopicsAsync(Block block, XmlWriter writer)
        {
            if (block.ReferenceMap.Count > 0 && !isInSeeAlso)
                await writer.WriteStartElementAsync(null, "relatedTopics", null);
            if (block.ReferenceMap.Count > 0 || isInSeeAlso)
            {
                foreach (var reference in block.ReferenceMap.Values)
                    await WriteReferenceLinkAsync(reference, writer);
            }
            if (block.ReferenceMap.Count > 0 || isInSeeAlso)
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

        #region Private Members

        private void SetTopicTitle(Block block)
        {
            if (Title != null)
                throw new InvalidOperationException("Topic title is already set");
            Title = block.InlineContent.LiteralContent;
        }

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

        private TopicData Topic
        {
            get { return topic; }
        }

        private Guid Id
        {
            get { return Topic.Id; }
        }

        private string Title
        {
            get { return Topic.Title; }
            set { Topic.Title = value; }
        }

        #endregion
    }
}
