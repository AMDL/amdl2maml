﻿using CommonMark;
using CommonMark.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace Amdl.Maml.Converter
{
    /// <summary>
    /// AMDL topic converter.
    /// </summary>
    public class TopicConverter
    {
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

        private readonly TopicData topic;
        private readonly IDictionary<string, TopicData> topics;

        private TopicState topicState;
        private Stack<SectionState> sectionStates;
        private InlineState inlineState;

        private bool isMarkupInline;
        private bool isInSeeAlso;

        /// <summary>
        /// Initializes a new instance of the <see cref="TopicConverter"/> class.
        /// </summary>
        /// <param name="topic">Current topic.</param>
        /// <param name="topics">Known topics.</param>
        public TopicConverter(TopicData topic, IDictionary<string, TopicData> topics)
        {
            this.topic = topic;
            this.topics = topics;
        }

        /// <summary>
        /// Converts the topic to MAML.
        /// </summary>
        /// <param name="reader">Reader.</param>
        /// <param name="writer">Writer.</param>
        public void Convert(TextReader reader, TextWriter writer)
        {
            var settings = CommonMarkSettings.Default.Clone();
            settings.OutputFormat = OutputFormat.CustomDelegate;
            settings.OutputDelegate = WriteDocument;
            settings.AdditionalFeatures = CommonMarkAdditionalFeatures.StrikethroughTilde
                | CommonMarkAdditionalFeatures.SubscriptTilde | CommonMarkAdditionalFeatures.SuperscriptCaret;
            CommonMarkConverter.Convert(reader, writer, settings);
        }

        #region Document

        private void WriteDocument(Block doc, TextWriter writer, CommonMarkSettings settings)
        {
            if (doc.Tag != BlockTag.Document)
                throw new InvalidOperationException("Unexpected block tag: " + doc.Tag);

            var xmlSettings = new XmlWriterSettings
            {
                Indent = true,
            };

            using (var xmlWriter = XmlWriter.Create(writer, xmlSettings))
            {
                DoWriteDocument(doc, xmlWriter);
            }
        }

        private void DoWriteDocument(Block block, XmlWriter writer)
        {
            writer.WriteStartDocument();

            writer.WriteComment(" This document was generated by a tool. ");
            writer.WriteComment(" Changes to this file may cause incorrect behavior and will be lost if the code is regenerated. ");
            writer.WriteComment(" amdl2maml Version " + GetType().GetTypeInfo().Assembly.GetName().Version + ' ');

            writer.WriteStartElement("topic");
            writer.WriteAttributeString("id", Id.ToString());
            writer.WriteAttributeString("revisionNumber", "1");

            sectionStates = new Stack<SectionState>();
            sectionStates.Push(SectionState.None);

            writer.WriteStartElement(GetRootElementName(), "http://ddue.schemas.microsoft.com/authoring/2003/5");

            WriteStartSummary(writer);

            WriteChildBlocks(block, writer);

            WriteEndSections(2, writer);
            WriteEndSummary(writer);
            WriteEndIntroduction(writer);
            WriteRelatedTopics(block, writer);

            writer.WriteEndElement(); //developerConceptualDocument
            writer.WriteEndElement(); //topic
            writer.WriteEndDocument();
        }

        private void WriteStartSummary(XmlWriter writer)
        {
            writer.WriteStartElement("summary");
            topicState = TopicState.Summary;
        }

        private void WriteEndSummary(XmlWriter writer)
        {
            if (topicState == TopicState.Summary)
            {
                writer.WriteEndElement(); //summary
                topicState = TopicState.Content;
            }
        }

        private void WriteStartIntroduction(Block block, XmlWriter writer)
        {
            SetTopicTitle(block);
            writer.WriteStartElement("introduction");
            if (block.Tag == BlockTag.SETextHeader)
                writer.WriteElementString("autoOutline", null);

            topicState = TopicState.Introduction;
        }

        private void WriteEndIntroduction(XmlWriter writer)
        {
            if (topicState == TopicState.Introduction)
            {
                writer.WriteEndElement(); //introduction
                topicState = TopicState.Content;
            }
        }

        private void WriteStartSeeAlso(XmlWriter writer)
        {
            WriteEndSections(2, writer);
            writer.WriteStartElement("relatedTopics");
            isInSeeAlso = true;
        }

        private string GetRootElementName()
        {
            return string.Format("developer{0}Document", Topic.Type);
        }

        private void SetTopicTitle(Block block)
        {
            if (Title != null)
                throw new InvalidOperationException("Topic title is already set");
            Title = block.InlineContent.LiteralContent;
        }

        #endregion

        #region Block

        private void WriteBlock(Block block, XmlWriter writer)
        {
            switch (block.Tag)
            {
                case BlockTag.AtxHeader:
                case BlockTag.SETextHeader:
                    WriteStartSection(block, writer);
                    break;

                case BlockTag.Paragraph:
                    WriteParagraph(block, writer);
                    break;

                case BlockTag.BlockQuote:
                    writer.WriteStartElement("quote");
                    WriteChildBlocks(block, writer);
                    writer.WriteEndElement(); //quote
                    break;

                case BlockTag.HtmlBlock:
                    writer.WriteStartElement("markup");
                    writer.WriteRaw("\n");
                    writer.WriteRaw(block.StringContent.ToString());
                    writer.WriteEndElement(); //markup
                    break;

                case BlockTag.IndentedCode:
                    writer.WriteStartElement("code");
                    writer.WriteRaw("\n");
                    writer.WriteRaw(block.StringContent.ToString());
                    writer.WriteEndElement(); //code
                    break;

                case BlockTag.FencedCode:
                    writer.WriteStartElement("code");
                    if (!string.IsNullOrEmpty(block.FencedCodeData.Info))
                    {
                        if (Languages.Contains(block.FencedCodeData.Info))
                            writer.WriteAttributeString("language", block.FencedCodeData.Info);
                        else
                            writer.WriteAttributeString("title", block.FencedCodeData.Info);
                    }
                    writer.WriteRaw("\n");
                    writer.WriteString(block.StringContent.ToString());
                    writer.WriteEndElement(); //code
                    break;

                case BlockTag.List:
                    WriteList(block, writer);
                    break;

                case BlockTag.ListItem:
                    WriteListItem(block, writer);
                    break;

                case BlockTag.ReferenceDefinition:
                    break;

                case BlockTag.HorizontalRuler:
                    break;

                default:
                    throw new InvalidOperationException("Unexpected block tag: " + block.Tag);
            }
        }

        private void WriteChildBlocks(Block block, XmlWriter writer)
        {
            for (var child = block.FirstChild; child != null; child = child.NextSibling)
                WriteBlock(child, writer);
        }

        private void WriteParagraph(Block block, XmlWriter writer)
        {
            if (!isInSeeAlso)
                writer.WriteStartElement("para");
            for (var inline = block.InlineContent; inline != null; inline = inline.NextSibling)
            {
                WriteInline(inline, writer);
                inlineState = InlineState.Start;
            }
            inlineState = InlineState.None;
            WriteEndMarkupInline(writer);
            if (!isInSeeAlso)
                writer.WriteEndElement(); //para
        }

        #endregion Block

        #region Inline

        private void WriteInline(Inline inline, XmlWriter writer)
        {
            switch (inline.Tag)
            {
                case InlineTag.String:
                    writer.WriteString(inline.LiteralContent);
                    break;

                case InlineTag.Code:
                    writer.WriteStartElement("codeInline");
                    writer.WriteRaw(inline.LiteralContent);
                    writer.WriteEndElement();
                    break;

                case InlineTag.Emphasis:
                    writer.WriteStartElement("legacyItalic");
                    WriteChildInlines(inline, writer);
                    writer.WriteEndElement();
                    break;

                case InlineTag.RawHtml:
                    WriteStartMarkupInline(writer);
                    writer.WriteRaw(inline.LiteralContent);
                    WriteChildInlines(inline, writer);
                    break;

                case InlineTag.Strong:
                    writer.WriteStartElement("legacyBold");
                    WriteChildInlines(inline, writer);
                    writer.WriteEndElement();
                    break;

                case InlineTag.Subscript:
                    writer.WriteStartElement("subscript");
                    WriteChildInlines(inline, writer);
                    writer.WriteEndElement(); //subscript;
                    break;

                case InlineTag.Superscript:
                    writer.WriteStartElement("superscript");
                    WriteChildInlines(inline, writer);
                    writer.WriteEndElement(); //superscript;
                    break;

                case InlineTag.SoftBreak:
                    writer.WriteRaw("\n");
                    break;

                case InlineTag.Link:
                    WriteLink(inline, writer);
                    break;

                case InlineTag.Image:
                    WriteImage(inline, writer);
                    break;

                case InlineTag.LineBreak:
                    throw new NotImplementedException();

                case InlineTag.Strikethrough:
                    WriteStartMarkupInline(writer);
                    writer.WriteStartElement("s");
                    WriteChildInlines(inline, writer);
                    writer.WriteEndElement(); //s
                    break;

                default:
                    throw new InvalidOperationException("Unexpected inline tag: " + inline.Tag);
            }
        }

        private void WriteStartMarkupInline(XmlWriter writer)
        {
            if (!isMarkupInline)
            {
                writer.WriteStartElement("markup");
                isMarkupInline = true;
            }
        }

        private void WriteEndMarkupInline(XmlWriter writer)
        {
            if (isMarkupInline)
            {
                writer.WriteEndElement(); //markup
                isMarkupInline = false;
            }
        }

        private void WriteChildInlines(Inline inline, XmlWriter writer)
        {
            for (var child = inline.FirstChild; child != null; child = child.NextSibling)
                WriteInline(child, writer);
        }

        #endregion Inline

        #region Section

        private void WriteStartSection(Block block, XmlWriter writer)
        {
            WriteEndSummary(writer);

            if (block.HeaderLevel == 1)
            {
                WriteStartIntroduction(block, writer);
                return;
            }

            WriteEndIntroduction(writer);

            if (isInSeeAlso)
                return;

            var title = block.InlineContent.LiteralContent;
            if (title.Equals(Properties.Resources.SeeAlsoTitle))
            {
                WriteStartSeeAlso(writer);
                return;
            }

            DoWriteStartSection(block, writer);
        }

        private void DoWriteStartSection(Block block, XmlWriter writer)
        {
            WriteEndSections(block.HeaderLevel, writer);

            var state = sectionStates.Peek();
            if (state == SectionState.Content)
            {
                writer.WriteEndElement(); //content
                writer.WriteStartElement("sections");
                sectionStates.Pop();
                sectionStates.Push(SectionState.Sections);
            }

            var title = block.InlineContent.LiteralContent;
            writer.WriteStartElement("section");
            writer.WriteAttributeString("address", title);
            writer.WriteElementString("title", title);
            writer.WriteStartElement("content");

            if (block.Tag == BlockTag.SETextHeader)
                writer.WriteElementString("autoOutline", null);

            sectionStates.Push(SectionState.Content);
        }

        private void WriteEndSections(int level, XmlWriter writer)
        {
            while (sectionStates.Count() >= level)
            {
                var state = sectionStates.Pop();
                writer.WriteEndElement(); //content | sections
                writer.WriteEndElement(); //section
            }
        }

        #endregion Section

        #region Link

        private void WriteLink(Inline inline, XmlWriter writer)
        {
            if (Uri.IsWellFormedUriString(inline.TargetUrl, UriKind.Absolute))
                WriteExternalLink(inline, writer);
            else
                WriteConceptualLink(inline, writer);
        }

        private void WriteConceptualLink(Inline inline, XmlWriter writer)
        {
            string href = inline.TargetUrl.StartsWith("#")
                ? inline.TargetUrl
                : topics[inline.TargetUrl].Id.ToString();
            writer.WriteStartElement("link");
            writer.WriteAttributeString("href", "http://www.w3.org/1999/xlink", href);
            //writer.WriteString(inline.LiteralContent);
            WriteChildInlines(inline, writer);
            writer.WriteEndElement(); //link
        }

        private void WriteExternalLink(Inline inline, XmlWriter writer)
        {
            writer.WriteStartElement("externalLink");
            writer.WriteStartElement("linkUri");
            writer.WriteString(inline.TargetUrl);
            writer.WriteEndElement(); //linkUri
            writer.WriteStartElement("linkText");
            if (!string.IsNullOrEmpty(inline.LiteralContent))
                writer.WriteString(inline.LiteralContent);
            else
                WriteChildInlines(inline, writer);
            writer.WriteEndElement(); //linkText
            writer.WriteEndElement(); //externalLink
        }

        private void WriteRelatedTopics(Block block, XmlWriter writer)
        {
            if (block.ReferenceMap.Count > 0 && !isInSeeAlso)
                writer.WriteStartElement("relatedTopics");
            if (block.ReferenceMap.Count > 0 || isInSeeAlso)
            {
                foreach (var reference in block.ReferenceMap.Values)
                    WriteReferenceLink(reference, writer);
            }
            if (block.ReferenceMap.Count > 0 || isInSeeAlso)
                writer.WriteEndElement();  //relatedTopics
        }

        private void WriteReferenceLink(Reference reference, XmlWriter writer)
        {
            writer.WriteStartElement("externalLink");
            writer.WriteStartElement("linkUri");
            writer.WriteString(reference.Url);
            writer.WriteEndElement(); //linkUri
            var text = string.IsNullOrEmpty(reference.Title)
                ? reference.Label
                : reference.Title;
            writer.WriteElementString("linkText", text);
            writer.WriteEndElement(); //externalLink
        }

        #endregion

        #region Image

        private void WriteImage(Inline inline, XmlWriter writer)
        {
            if (inlineState == InlineState.Start)
                WriteInlineImage(inline, writer);
            else
                WriteBlockImage(inline, writer);
        }

        private void WriteInlineImage(Inline inline, XmlWriter writer)
        {
            writer.WriteStartElement("mediaLinkInline");
            writer.WriteStartElement("image");
            writer.WriteAttributeString("href", "http://www.w3.org/1999/xlink", inline.TargetUrl);
            writer.WriteEndElement(); //image
            writer.WriteEndElement(); //mediaLinkInline
        }

        private void WriteBlockImage(Inline inline, XmlWriter writer)
        {
            writer.WriteStartElement("mediaLink");
            writer.WriteStartElement("image");
            writer.WriteAttributeString("href", "http://www.w3.org/1999/xlink", inline.TargetUrl);
            writer.WriteEndElement(); //image
            if (inline.FirstChild != null)
                WriteCaption(inline, writer);
            writer.WriteEndElement(); //mediaLink
        }

        private void WriteCaption(Inline inline, XmlWriter writer)
        {
            writer.WriteStartElement("caption");
            writer.WriteAttributeString("placement", "after");
            WriteChildInlines(inline, writer);
            writer.WriteEndElement(); //caption
        }

        #endregion Image

        #region List

        private void WriteList(Block block, XmlWriter writer)
        {
            writer.WriteStartElement("list");
            var listClass = GetListClass(block);
            if (listClass != null)
                writer.WriteAttributeString("class", listClass);
            WriteChildBlocks(block, writer);
            writer.WriteEndElement(); //list
        }

        private void WriteListItem(Block block, XmlWriter writer)
        {
            writer.WriteStartElement("listItem");
            WriteChildBlocks(block, writer);
            writer.WriteEndElement(); //listItem
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

        #endregion

        #region Properties

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
