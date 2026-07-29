using Newtonsoft.Json;
using SW.PrimitiveTypes;

namespace SW.HttpExtensions
{
    /// <summary>
    /// Reads a structured body off an <see cref="ApiResult"/> — in practice, off a FAILED one.
    ///
    /// <para>
    /// <see cref="ApiResult{TResponse}.Response"/> is only ever populated for a 2xx (see
    /// <see cref="ApiOperationRunnerWrapped{TResponse}"/>); a non-2xx leaves the payload sitting in
    /// <see cref="ApiResult.Body"/> as an unparsed string. So an API that answers an expected,
    /// non-exceptional refusal with a 4xx and a typed body — "out of stock", "already cancelled", a
    /// validation list — gives the caller a status code and nothing it can act on, unless every call
    /// site hand-rolls its own <see cref="JsonConvert"/> call.
    /// </para>
    /// <para>
    /// The observable consequence is services answering <c>200 OK</c> with an error field instead,
    /// purely because that's the only shape a caller can actually read. That works, but it hides a
    /// failure from every generic status-code check (logging, retries, dashboards) and silently
    /// degrades if anyone later corrects the status to a 4xx.
    /// </para>
    /// </summary>
    public static class ApiResultExtensions
    {
        /// <summary>
        /// Deserializes <see cref="ApiResult.Body"/> into <typeparamref name="TBody"/>.
        ///
        /// Never throws: a null result, an empty body, or a body that isn't JSON of that shape all
        /// return <c>false</c> with <paramref name="body"/> left at its default. That matters because
        /// a failure body is frequently NOT the shape you expect — an unhandled server exception, an
        /// HTML error page from a proxy, or a gateway timeout — and none of those should turn a failed
        /// call into a thrown exception at the call site.
        ///
        /// Note this reads <see cref="ApiResult.Body"/>, not <see cref="ApiResult{TResponse}.Response"/>:
        /// on a successful typed call the body is not retained, so this is for the failure path.
        /// </summary>
        public static bool TryReadBody<TBody>(this ApiResult apiResult, out TBody body)
        {
            body = default;

            if (string.IsNullOrWhiteSpace(apiResult?.Body))
                return false;

            try
            {
                var deserialized = JsonConvert.DeserializeObject<TBody>(apiResult.Body);
                if (deserialized == null)
                    return false;

                body = deserialized;
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// <see cref="TryReadBody{TBody}"/> for when the absent case needs no distinct handling —
        /// returns the default when the body is missing or isn't <typeparamref name="TBody"/>.
        /// </summary>
        public static TBody ReadBodyOrDefault<TBody>(this ApiResult apiResult)
        {
            apiResult.TryReadBody<TBody>(out var body);
            return body;
        }
    }
}
