using Microsoft.VisualStudio.TestTools.UnitTesting;
using SW.PrimitiveTypes;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace SW.HttpExtensions.UnitTests
{
    [TestClass]
    public class ApiClientBaseTests
    {
        private class TestOptions : ApiClientOptionsBase
        {
            public override string ConfigurationSection => "Test";
        }

        /// <summary>Records every path it is asked for, and answers 200 with an empty object.</summary>
        private class RecordingHandler : HttpMessageHandler
        {
            private readonly List<string> paths = new List<string>();
            private readonly int delayMs;

            public RecordingHandler(int delayMs = 0) => this.delayMs = delayMs;

            public IReadOnlyList<string> Paths
            {
                get { lock (paths) return paths.ToList(); }
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                lock (paths) paths.Add(request.RequestUri.PathAndQuery);
                if (delayMs > 0) await Task.Delay(delayMs, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
                };
            }
        }

        /// <summary>Two operations on one client, exactly as a generated SDK writes them.</summary>
        private class TestClient : ApiClientBase<TestOptions>
        {
            public TestClient(HttpClient httpClient, RequestContext requestContext, TestOptions options)
                : base(httpClient, requestContext, options) { }

            public Task<ApiResult<object>> Alpha() =>
                Builder.Path("alpha").AsApiResult<object>().GetAsync();

            public Task<ApiResult<object>> Beta() =>
                Builder.Path("beta").AsApiResult<object>().GetAsync();

            public ApiOperationBuilder<TestOptions> ExposedBuilder => Builder;

            /// <summary>
            /// Prepares both operations before dispatching either — the shape that breaks
            /// deterministically when every method shares one builder instance.
            /// </summary>
            public async Task TwoPreparedOperations()
            {
                var first = Builder.Path("alpha");
                var second = Builder.Path("beta");
                await first.AsApiResult<object>().GetAsync();
                await second.AsApiResult<object>().GetAsync();
            }
        }

        private static TestClient CreateClient(HttpMessageHandler handler, RequestContext requestContext = null)
        {
            var httpClient = new HttpClient(handler) { BaseAddress = new System.Uri("http://localhost/") };
            return new TestClient(httpClient, requestContext ?? new RequestContext(), new TestOptions());
        }

        [TestMethod]
        public void BuilderIsNotSharedBetweenAccesses()
        {
            var client = CreateClient(new RecordingHandler());
            Assert.AreNotSame(client.ExposedBuilder, client.ExposedBuilder);
        }

        /// <summary>
        /// The bug a per-access builder exists to prevent, in its deterministic form: with one shared
        /// builder the second Path(...) overwrites the first, so BOTH requests go to "beta" and the
        /// first operation silently runs as the second.
        /// </summary>
        [TestMethod]
        public async Task OperationsPreparedBeforeDispatchDoNotCrossPaths()
        {
            var handler = new RecordingHandler();
            var client = CreateClient(handler);

            await client.TwoPreparedOperations();

            CollectionAssert.AreEqual(
                new[] { "/alpha", "/beta" },
                handler.Paths.ToList(),
                $"expected one request per operation, got: {string.Join(", ", handler.Paths)}");
        }

        /// <summary>
        /// Guards the case that was ALREADY safe, so it stays that way: awaiting two async operations
        /// together is fine even with a shared builder, because AsApiResult copies the path into a
        /// fresh runner synchronously — before the first await — so Alpha's path is captured before
        /// Beta starts. Worth pinning down; it's the reason the old shared builder survived in
        /// production for as long as it did.
        /// </summary>
        [TestMethod]
        public async Task AwaitingTwoOperationsTogetherKeepsTheirOwnPaths()
        {
            var handler = new RecordingHandler(delayMs: 30);
            var client = CreateClient(handler);

            await Task.WhenAll(client.Alpha(), client.Beta());

            CollectionAssert.AreEquivalent(
                new[] { "/alpha", "/beta" },
                handler.Paths.ToList(),
                $"expected one request per operation, got: {string.Join(", ", handler.Paths)}");
        }

        [TestMethod]
        public async Task SequentialCallsOnOneClientEachKeepTheirOwnPath()
        {
            var handler = new RecordingHandler();
            var client = CreateClient(handler);

            await client.Alpha();
            await client.Beta();

            CollectionAssert.AreEqual(new[] { "/alpha", "/beta" }, handler.Paths.ToList());
        }

        /// <summary>
        /// A builder per call means the constructor's correlation-id header runs per call too, and
        /// HttpHeaders.Add appends rather than replaces — so without the remove-then-add this sends a
        /// header that grows a duplicate value on every request.
        /// </summary>
        [TestMethod]
        public async Task CorrelationIdIsSentOnceNoMatterHowManyCalls()
        {
            var requestContext = new RequestContext();
            requestContext.Set(new ClaimsPrincipal(new ClaimsIdentity("testauth")));

            var handler = new RecordingHandler();
            var httpClient = new HttpClient(handler) { BaseAddress = new System.Uri("http://localhost/") };
            var client = new TestClient(httpClient, requestContext, new TestOptions());

            await client.Alpha();
            await client.Beta();
            await client.Alpha();

            Assert.IsTrue(
                httpClient.DefaultRequestHeaders.TryGetValues(RequestContext.CorrelationIdHeaderName, out var values),
                "correlation id header was not set at all");
            Assert.AreEqual(1, values.Count(), $"correlation id was sent {values.Count()} times");
        }
    }
}
