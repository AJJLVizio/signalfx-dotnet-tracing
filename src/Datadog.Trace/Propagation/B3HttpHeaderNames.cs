// Modified by SignalFx

namespace SignalFx.Tracing.Propagation
{
    internal static class B3HttpHeaderNames
    {
        /// <summary>
        /// ID of a distributed trace.
        /// </summary>
        public const string B3TraceId = "x-b3-traceid";

        /// <summary>
        /// ID of the propagating span.
        /// </summary>
        public const string B3SpanId = "x-b3-spanid";

        /// <summary>
        /// ID of the propagating span's parent.
        /// </summary>
        public const string B3ParentId = "x-b3-parentspanid";

        /// <summary>
        /// Setting used to determine whether a trace should be sampled or not.
        /// </summary>
        public const string B3Sampled = "x-b3-sampled";

        /// <summary>
        /// Setting used to determine whether a trace should be considered for debugging.
        /// </summary>
        public const string B3Flags = "x-b3-flags";
    }
}
