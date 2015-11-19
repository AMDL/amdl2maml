using CommonMark;
using CommonMark.Syntax;
using System.IO;

namespace Amdl.Maml.Converter
{
    class TopicParser
    {
        private static CommonMarkSettings settings;

        static TopicParser()
        {
            settings = CommonMarkSettings.Default.Clone();
            settings.AdditionalFeatures = CommonMarkAdditionalFeatures.None
                | CommonMarkAdditionalFeatures.StrikethroughTilde
                | CommonMarkAdditionalFeatures.SubscriptTilde
                | CommonMarkAdditionalFeatures.SuperscriptCaret;
        }

        public static TopicParserResult Parse(TextReader reader)
        {
            var root = CommonMarkConverter.Parse(reader, settings);
            return new TopicParserResult(root);
        }
    }
}
