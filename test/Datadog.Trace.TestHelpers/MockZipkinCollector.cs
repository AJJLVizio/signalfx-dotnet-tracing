// Modifed by SignalFx
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignalFx.Tracing;
using SignalFx.Tracing.ExtensionMethods;

namespace Datadog.Trace.TestHelpers
{
    // Modeled from MockTracerAgent
    public class MockZipkinCollector : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Thread _listenerThread;
        private readonly CancellationTokenSource _listenerCts = new CancellationTokenSource();

        public MockZipkinCollector(int port = 9080, int retries = 5)
        {
            // try up to 5 consecutive ports before giving up
            while (true)
            {
                // seems like we can't reuse a listener if it fails to start,
                // so create a new listener each time we retry
                var listener = new HttpListener();

                try
                {
                    listener.Start();
                    listener.Prefixes.Add($"http://localhost:{port}/");

                    // successfully listening
                    Port = port;
                    _listener = listener;

                    _listenerThread = new Thread(HandleHttpRequests);
                    _listenerThread.Start();

                    return;
                }
                catch (HttpListenerException) when (retries > 0)
                {
                    // only catch the exception if there are retries left
                    port++;
                    retries--;
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
            }
        }

        public event EventHandler<EventArgs<HttpListenerContext>> RequestReceived;

        public event EventHandler<EventArgs<IList<IMockSpan>>> RequestDeserialized;

        /// <summary>
        /// Gets or sets a value indicating whether to skip serialization of traces.
        /// </summary>
        public bool ShouldDeserializeTraces { get; set; } = true;

        /// <summary>
        /// Gets the TCP port that this Agent is listening on.
        /// Can be different from <see cref="MockZipkinCollector(int, int)"/>'s <c>initialPort</c>
        /// parameter if listening on that port fails.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Gets the filters used to filter out spans we don't want to look at for a test.
        /// </summary>
        public List<Func<IMockSpan, bool>> SpanFilters { get; private set; } = new List<Func<IMockSpan, bool>>();

        public IImmutableList<IMockSpan> Spans { get; private set; } = ImmutableList<IMockSpan>.Empty;

        public IImmutableList<NameValueCollection> RequestHeaders { get; private set; } = ImmutableList<NameValueCollection>.Empty;

        /// <summary>
        /// Wait for the given number of spans to appear.
        /// </summary>
        /// <param name="count">The expected number of spans.</param>
        /// <param name="timeoutInMilliseconds">The timeout</param>
        /// <param name="operationName">The integration we're testing</param>
        /// <param name="minDateTime">Minimum time to check for spans from</param>
        /// <param name="returnAllOperations">When true, returns every span regardless of operation name</param>
        /// <param name="operationNameContainsAny">Set of words that need to be present of selected span names</param>
        /// <returns>The list of spans.</returns>
        public IImmutableList<IMockSpan> WaitForSpans(
            int count,
            int timeoutInMilliseconds = 20000,
            string operationName = null,
            DateTimeOffset? minDateTime = null,
            bool returnAllOperations = false,
            string[] operationNameContainsAny = null)
        {
            var deadline = DateTime.Now.AddMilliseconds(timeoutInMilliseconds);
            var minimumOffset = (minDateTime ?? DateTimeOffset.MinValue).ToUnixTimeMicroseconds();

            IImmutableList<IMockSpan> relevantSpans = ImmutableList<IMockSpan>.Empty;

            operationNameContainsAny ??= new string[0];
            while (DateTime.Now < deadline)
            {
                relevantSpans =
                    Spans
                       .Where(s => SpanFilters.All(shouldReturn => shouldReturn(s)))
                       .Where(s => s.Start > minimumOffset)
                       .ToImmutableList();

                // Warning: the `ToList()` below is required or relevantSpans fails (at least for net461). Likely due to the fact that relevantSpans is immutable.
                if (relevantSpans.ToList().Count(s => operationNameContainsAny.Any(contains => s.Name.Contains(contains)) || operationName == null || s.Name == operationName) >= count)
                {
                    break;
                }

                Thread.Sleep(500);
            }

            if (!returnAllOperations)
            {
                relevantSpans =
                    relevantSpans
                       .Where(s => operationNameContainsAny.Any(contains => s.Name.Contains(contains))
                            || (operationNameContainsAny.Length == 0 && operationName == null)
                            || s.Name == operationName)
                       .ToImmutableList();
            }

            return relevantSpans;
        }

        public void Dispose()
        {
            lock (_listener)
            {
                _listenerCts.Cancel();
                _listener.Stop();
            }
        }

        protected virtual void OnRequestReceived(HttpListenerContext context)
        {
            RequestReceived?.Invoke(this, new EventArgs<HttpListenerContext>(context));
        }

        protected virtual void OnRequestDeserialized(IList<IMockSpan> trace)
        {
            RequestDeserialized?.Invoke(this, new EventArgs<IList<IMockSpan>>(trace));
        }

        private void AssertHeader(
            NameValueCollection headers,
            string headerKey,
            Func<string, bool> assertion)
        {
            var header = headers.Get(headerKey);

            if (string.IsNullOrEmpty(header))
            {
                throw new Exception($"Every submission to the agent should have a {headerKey} header.");
            }

            if (!assertion(header))
            {
                throw new Exception($"Failed assertion for {headerKey} on {header}");
            }
        }

        private void HandleHttpRequests()
        {
            while (true)
            {
                try
                {
                    var getCtxTask = Task.Run(() => _listener.GetContext());
                    getCtxTask.Wait(_listenerCts.Token);

                    lock (_listener)
                    {
                        if (!_listener.IsListening)
                        {
                            return;
                        }

                        var ctx = getCtxTask.Result;
                        OnRequestReceived(ctx);

                        if (ShouldDeserializeTraces)
                        {
                            using (var reader = new StreamReader(ctx.Request.InputStream))
                            {
                                var zspans = JsonConvert.DeserializeObject<List<Span>>(reader.ReadToEnd());
                                if (zspans != null)
                                {
                                    IList<IMockSpan> spans = (IList<IMockSpan>)zspans.ConvertAll(x => (IMockSpan)x);
                                    OnRequestDeserialized(spans);

                                    Spans = Spans.AddRange(spans);
                                }

                                RequestHeaders = RequestHeaders.Add(new NameValueCollection(ctx.Request.Headers));
                            }
                        }

                        ctx.Response.ContentType = "application/json";
                        var buffer = Encoding.UTF8.GetBytes("{}");
                        ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
                        ctx.Response.Close();
                    }
                }
                catch (Exception ex) when (ex is HttpListenerException || ex is OperationCanceledException || ex is AggregateException)
                {
                    lock (_listener)
                    {
                        if (!_listener.IsListening)
                        {
                            return;
                        }
                    }

                    throw;
                }
            }
        }

        [DebuggerDisplay("TraceId={TraceId}, SpanId={SpanId}, Service={Service}, Name={Name}, Resource={Resource}")]
        public class Span : IMockSpan
        {
            [JsonExtensionData]
            private IDictionary<string, JToken> _zipkinData;

            public Span()
            {
                _zipkinData = new Dictionary<string, JToken>();
            }

            public TraceId TraceId
            {
                get => TraceId.TryParse(_zipkinData["traceId"].ToString(), out var traceId) ? traceId : TraceId.Zero;
            }

            public ulong SpanId
            {
                get => Convert.ToUInt64(_zipkinData["id"].ToString(), 16);
            }

            public string Name { get; set; }

            public string Resource { get; set; }

            public string Service
            {
                get => _zipkinData["localEndpoint"]["serviceName"].ToString();
            }

            public string Type { get; set; }

            public long Start
            {
                get => Convert.ToInt64(_zipkinData["timestamp"].ToString());
            }

            public long Duration { get; set; }

            public ulong? ParentId
            {
                get
                {
                    ((IDictionary)_zipkinData).TryGetValue<string>("parentId", out string parentId);
                    return parentId == null ? null : (ulong?)Convert.ToUInt64(parentId.ToString(), 16);
                }
            }

            public byte Error { get; set; }

            public Dictionary<string, string> Tags { get; set; }

            public Dictionary<DateTimeOffset, Dictionary<string, string>> Logs
            {
                get
                {
                    var annotations = _zipkinData["annotations"].ToObject<List<Dictionary<string, object>>>();
                    var logs = new Dictionary<DateTimeOffset, Dictionary<string, string>>();
                    foreach (var item in annotations)
                    {
                        DateTimeOffset timestamp = TimeHelpers.UnixMicrosecondsToDateTimeOffset((long)item["timestamp"]);
                        Dictionary<string, string> fields = JsonConvert.DeserializeObject<Dictionary<string, string>>(item["value"].ToString());
                        logs[timestamp] = fields;
                    }

                    return logs;
                }
            }

            public Dictionary<string, double> Metrics { get; set; }

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"TraceId: {TraceId}");
                sb.AppendLine($"ParentId: {ParentId}");
                sb.AppendLine($"SpanId: {SpanId}");
                sb.AppendLine($"Service: {Service}");
                sb.AppendLine($"Name: {Name}");
                sb.AppendLine($"Resource: {Resource}");
                sb.AppendLine($"Type: {Type}");
                sb.AppendLine($"Start: {Start}");
                sb.AppendLine($"Duration: {Duration}");
                sb.AppendLine($"Error: {Error}");
                sb.AppendLine("Tags:");

                if (Tags?.Count > 0)
                {
                    foreach (var kv in Tags)
                    {
                        sb.Append($"\t{kv.Key}:{kv.Value}\n");
                    }
                }

                sb.AppendLine("Logs:");
                foreach (var e in Logs)
                {
                    sb.Append($"\t{e.Key}:\n");
                    foreach (var kv in e.Value)
                    {
                        sb.Append($"\t\t{kv.Key}:{kv.Value}\n");
                    }
                }

                return sb.ToString();
            }

            [OnDeserialized]
            private void OnDeserialized(StreamingContext context)
            {
                var resourceNameTag = (string)DictionaryExtensions.GetValueOrDefault(Tags, "resource.name");
                // If resource.name tag not set, it matches the operation name
                Resource = string.IsNullOrEmpty(resourceNameTag) ? Name : resourceNameTag;
                Type = (string)DictionaryExtensions.GetValueOrDefault(Tags, "span.type");
                var error = DictionaryExtensions.GetValueOrDefault(Tags, "error") ?? "false";
                Error = (byte)(error.ToLowerInvariant().Equals("true") ? 1 : 0);
                var spanKind = (string)DictionaryExtensions.GetValueOrDefault(_zipkinData, "kind");
                if (spanKind != null)
                {
                    Tags["span.kind"] = spanKind.ToLowerInvariant();
                }
            }
        }
    }
}
