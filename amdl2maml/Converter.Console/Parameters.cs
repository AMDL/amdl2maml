using Ditto.CommandLine;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Amdl.Maml.Converter.Console
{
    [DefaultProperty("Help")]
    internal sealed class Parameters
    {
        [Required]
        [Option]
        [Option("s")]
        [DisplayName("sourcePath")]
        [Description("Source folder path")]
        public string SourcePath
        {
            get;
            set;
        }

        [Required]
        [Option]
        [Option("d")]
        [DisplayName("destinationPath")]
        [Description("Destination folder path")]
        public string DestinationPath
        {
            get;
            set;
        }

        [Required]
        [Option]
        [Option("l")]
        [DisplayName("contentLayoutPath")]
        [Description("Content layout file path")]
        public string ContentLayoutPath
        {
            get;
            set;
        }

        [Option]
        [Option("v")]
        [DefaultValue(Verbosity.Normal)]
        [Description("Verbosity")]
        [Category("Output options")]
        public Verbosity Verbosity
        {
            get;
            set;
        }

        [Option]
        [Option("tf")]
        [DefaultValue("yyyy-MM-dd HH:mm:ss.fff")]
        [Description("Time format")]
        [Category("Output options")]
        public string TimeFormat
        {
            get;
            set;
        }

        [Option]
        [Option("df")]
        [DefaultValue("hh:mm:ss.fff")]
        [Description("Duration format")]
        [Category("Output options")]
        public string DurationFormat
        {
            get;
            set;
        }

        [Option]
        [Option("?")]
        [Description("Display this help")]
        public bool Help
        {
            get;
            set;
        }
    }
}
