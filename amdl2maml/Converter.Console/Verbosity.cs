using System.ComponentModel;

namespace Amdl.Maml.Converter.Console
{
    enum Verbosity
    {
        [Description("No output")]
        Silent,

        [Description("Minimal output")]
        Minimal,

        [Description("Normal output")]
        Normal,

        [Description("Detailed output")]
        Detailed,

        [Description("Very detailed output")]
        Insane,
    }
}
