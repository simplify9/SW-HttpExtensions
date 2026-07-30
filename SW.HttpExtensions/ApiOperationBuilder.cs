using SW.PrimitiveTypes;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SW.HttpExtensions
{
    public class ApiOperationBuilder<TApiClientOptions> where TApiClientOptions : ApiClientOptionsBase
    {
        private readonly HttpClient httpClient;
        private readonly RequestContext requestContext;
        private readonly TApiClientOptions options;

        private string path = string.Empty;

        public ApiOperationBuilder(HttpClient httpClient, RequestContext requestContext, TApiClientOptions options)
        {
            this.httpClient = httpClient;
            this.requestContext = requestContext;
            this.options = options;

            // Remove-then-add rather than a bare Add: HttpHeaders.Add APPENDS a second value for an
            // existing name, so building more than one builder over the same HttpClient (which is now
            // once per call — see ApiClientBase.Builder) would otherwise send
            // "sw-correlation-id: abc, abc", growing by one on every call. Remove-then-add is also
            // what keeps the header CURRENT: a plain "add only if missing" guard would pin the first
            // request's correlation id onto every later request that reuses the client.
            if (requestContext.IsValid)
            {
                httpClient.DefaultRequestHeaders.Remove(RequestContext.CorrelationIdHeaderName);
                httpClient.DefaultRequestHeaders.Add(RequestContext.CorrelationIdHeaderName, requestContext.CorrelationId);
            }
        }

        public ApiOperationBuilder<TApiClientOptions> Path(string path)
        {
            this.path = path;
            return this;
        }

        public ApiOperationBuilder<TApiClientOptions> Jwt(string jwt = null)
        {
            var user = requestContext.User;

            if (user != null && jwt == null)
                jwt = options.Token.WriteJwt((ClaimsIdentity)user.Identity);

            if (jwt != null)
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            return this;
        }

        public ApiOperationBuilder<TApiClientOptions> JwtOnBehalf(string jwt = null)
        {
            var user = requestContext.User;
            var userClaimsIdentity = (ClaimsIdentity)user?.Identity;
            userClaimsIdentity?.AddClaim(new Claim(ClaimTypes.Actor, JwtTokenParameters.MicroService));

            if (user != null && jwt == null)
                jwt = options.Token.WriteJwt(userClaimsIdentity);

            if (jwt != null)
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            return this;
        }

        public ApiOperationBuilder<TApiClientOptions> Key(ApiKeyParameters apiKeyParameters = null)
        {
            if (apiKeyParameters == null)
                apiKeyParameters = options.ApiKey;

            if (!httpClient.DefaultRequestHeaders.Contains(apiKeyParameters.Name))
                httpClient.DefaultRequestHeaders.Add(apiKeyParameters.Name, apiKeyParameters.Value);

            return this;
        }

        public ApiOperationBuilder<TApiClientOptions> JwtOrKey(ApiKeyParameters apiKeyParameters = null)
        {
            var user = requestContext.User;
            if (user != null) return Jwt();

            return Key(apiKeyParameters);
        }

        public ApiOperationBuilder<TApiClientOptions> Header(string name, string value)
        {
            httpClient.DefaultRequestHeaders.Add(name, value);
            return this;
        }

        async public Task<int> DeleteAsync(bool throwOnFailure = true)
        {
            try
            {
                var httpResponseMessage = await httpClient.DeleteAsync(path);
                return ProcessResponse(httpResponseMessage, throwOnFailure);
            }
            catch
            {
                if (throwOnFailure) throw;
                return 0;
            }
        }

        async public Task<int> PostAsync(object payload = null, bool throwOnFailure = true)
        {
            try
            {
                var httpResponseMessage = await httpClient.PostAsync(path, httpClient.CreateHttpContent(payload));
                return ProcessResponse(httpResponseMessage, throwOnFailure);
            }
            catch
            {
                if (throwOnFailure) throw;
                return 0;
            }
        }

        private int ProcessResponse(HttpResponseMessage httpResponseMessage, bool throwOnFailure)
        {
            if (throwOnFailure) httpResponseMessage.EnsureSuccessStatusCode();
            return (int)httpResponseMessage.StatusCode;
        }

        public ApiOperationRunnerTyped<TResponse> As<TResponse>(bool throwOnFailure = true)
            => new ApiOperationRunnerTyped<TResponse>(httpClient, path, throwOnFailure);

        public ApiOperationRunnerWrapped<TResponse> AsApiResult<TResponse>()
            => new ApiOperationRunnerWrapped<TResponse>(httpClient, path);

        public ApiOperationRunnerWrapped AsApiResult()
            => new ApiOperationRunnerWrapped(httpClient, path);
    }
}