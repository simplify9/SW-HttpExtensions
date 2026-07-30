using SW.PrimitiveTypes;
using System.Net.Http;


namespace SW.HttpExtensions
{
    public abstract class ApiClientBase<TApiClientOptions> where TApiClientOptions : ApiClientOptionsBase
    {
        private readonly RequestContext requestContext;

        protected ApiClientBase(HttpClient httpClient, RequestContext requestContext, TApiClientOptions options)
        {
            HttpClient = httpClient;
            Options = options;
            this.requestContext = requestContext;
        }

        protected HttpClient HttpClient { get; }
        protected TApiClientOptions Options { get; }

        /// <summary>
        /// A FRESH builder per access, because <see cref="ApiOperationBuilder{TApiClientOptions}"/>
        /// carries mutable per-call state — most importantly the path.
        ///
        /// <para>
        /// This used to be one instance built in the constructor and shared by every method on the
        /// client, which made the client only accidentally safe. The usual
        /// <c>Builder.Path(x).AsApiResult&lt;T&gt;().GetAsync()</c> shape happens to hold up, because
        /// <c>AsApiResult</c> copies the path into a fresh runner synchronously — before the first
        /// await — so even <c>Task.WhenAll(client.A(), client.B())</c> has A's path captured before B
        /// starts. Two things did NOT hold up:
        /// </para>
        /// <list type="bullet">
        /// <item>Genuinely parallel use of one client instance (<c>Task.Run</c>, <c>Parallel.ForEach</c>,
        /// a background fan-out). Two threads racing <c>Path(...)</c> can send one operation to the
        /// other's URL, silently — the window is small, which makes it rare and awful to diagnose.</item>
        /// <item>Holding a configured builder before dispatching it — <c>var b = Builder.Path(x);</c>
        /// then using <c>b</c> after any other call on the same client. That one isn't a race at all;
        /// it's deterministically wrong, because <c>b</c> IS the other call's builder.</item>
        /// </list>
        /// </summary>
        protected ApiOperationBuilder<TApiClientOptions> Builder =>
            new ApiOperationBuilder<TApiClientOptions>(HttpClient, requestContext, Options);

        //protected void AddApiKey()
        //{
        //    operationBuilder.Jwt().Body("").Url("").MustSucceed().  
        //    //HttpClient.DefaultRequestHeaders.Add(Options.ApiKey.Name, Options.ApiKey.Value);
        //}

        //protected void AddJwt()
        //{
        //    var user = requestContext.User;

        //    var jwt = Options.Token.WriteJwt((ClaimsIdentity)user.Identity);

        //    if (jwt != null)
        //        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        //}

        //async public Task<HttpResponseMessage> PostAsync(string url, object payload, ApiHeaderOptions httpOperationHeaderOptions = ApiHeaderOptions.None)
        //{
        //    PrepareHeaderOptions(httpOperationHeaderOptions);
        //    return await HttpClient.PostAsync(url, payload);
        //}

        //async public Task<TResult> PostAsync<TResult>(string url, object payload, ApiHeaderOptions httpOperationHeaderOptions = ApiHeaderOptions.None)
        //{
        //    PrepareHeaderOptions(httpOperationHeaderOptions);
        //    return await HttpClient.PostAsync<TResult>(url, payload);
        //}

        //async public Task<TResult> GetAsync<TResult>(string url, ApiHeaderOptions httpOperationHeaderOptions = ApiHeaderOptions.None)
        //{
        //    PrepareHeaderOptions(httpOperationHeaderOptions);
        //    return await HttpClient.GetAsync<TResult>(url);
        //}

        //async public Task DeleteAsync<TResult>(string url, ApiHeaderOptions httpOperationHeaderOptions = ApiHeaderOptions.None)
        //{
        //    PrepareHeaderOptions(httpOperationHeaderOptions);
        //    var httpResponseMessage = await HttpClient.DeleteAsync(url);
        //    httpResponseMessage.EnsureSuccessStatusCode();
        //    return;
        //}

        //private void PrepareHeaderOptions(ApiHeaderOptions httpOperationHeaderOptions)
        //{
        //    switch (httpOperationHeaderOptions)
        //    {
        //        case ApiHeaderOptions.AddJwt:
        //            AddJwt();
        //            break;
        //        case ApiHeaderOptions.AddApiKey:
        //            AddApiKey();
        //            break;
        //        default:
        //            break;
        //    }
        //}
    }
}
