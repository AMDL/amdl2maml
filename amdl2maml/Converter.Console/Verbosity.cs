using System.ComponentModel;

namespace Amdl.Maml.Converter.Console
{
    /// <summary>
    /// Verbosity.
    /// </summary>
    enum Verbosity
    {
        /// <summary>
        /// No output.
        /// </summary>
        [Description("No output")]
        Silent,

        /// <summary>
        /// Minimal output.
        /// </summary>
        [Description("Minimal output")]
        Minimal,

        /// <summary>
        /// Normal output.
        /// </summary>
        [Description("Normal output")]
        Normal,

        /// <summary>
        /// Detailed output.
        /// </summary>
        [Description("Detailed output")]
        Detailed,

        /// <summary>
        /// Very detailed output.
        /// </summary>
        [Description("Very detailed output")]
        Insane,
    }
}
