using Microsoft.VisualStudio.TestTools.UnitTesting;
using SW.PrimitiveTypes;

namespace SW.HttpExtensions.UnitTests
{
    [TestClass]
    public class ApiResultExtensionsTests
    {
        private class Refusal
        {
            public string ErrorCode { get; set; }
            public string ErrorMessage { get; set; }
        }

        [TestMethod]
        public void ReadsAStructuredBodyOffAFailedResult()
        {
            var result = new ApiResult<object>
            {
                Success = false,
                StatusCode = 400,
                Body = "{\"errorCode\":\"InsufficientQuantity\",\"errorMessage\":\"only 3 left\"}"
            };

            Assert.IsTrue(result.TryReadBody<Refusal>(out var refusal));
            Assert.AreEqual("InsufficientQuantity", refusal.ErrorCode);
            Assert.AreEqual("only 3 left", refusal.ErrorMessage);
        }

        /// <summary>Property casing differs between the wire (camel) and C# (pascal).</summary>
        [TestMethod]
        public void MatchesPropertiesRegardlessOfCasing()
        {
            var result = new ApiResult { Body = "{\"ErrorCode\":\"Pascal\"}" };
            Assert.IsTrue(result.TryReadBody<Refusal>(out var refusal));
            Assert.AreEqual("Pascal", refusal.ErrorCode);
        }

        /// <summary>
        /// A failure body is often not the shape the caller expects — a proxy's HTML error page, a
        /// gateway timeout, a bare exception string. None of those may throw at the call site.
        /// </summary>
        [TestMethod]
        public void ReturnsFalseRatherThanThrowingOnABodyThatIsNotJson()
        {
            var result = new ApiResult { StatusCode = 502, Body = "<html><body>Bad Gateway</body></html>" };
            Assert.IsFalse(result.TryReadBody<Refusal>(out var refusal));
            Assert.IsNull(refusal);
        }

        [TestMethod]
        public void ReturnsFalseOnJsonOfTheWrongShape()
        {
            var result = new ApiResult { Body = "[1,2,3]" };
            Assert.IsFalse(result.TryReadBody<Refusal>(out _));
        }

        [TestMethod]
        public void ReturnsFalseOnAnEmptyOrAbsentBody()
        {
            Assert.IsFalse(new ApiResult { Body = null }.TryReadBody<Refusal>(out _));
            Assert.IsFalse(new ApiResult { Body = "" }.TryReadBody<Refusal>(out _));
            Assert.IsFalse(new ApiResult { Body = "   " }.TryReadBody<Refusal>(out _));
        }

        /// <summary>Literal JSON null deserializes to null — absent, not a value.</summary>
        [TestMethod]
        public void ReturnsFalseOnLiteralJsonNull()
        {
            Assert.IsFalse(new ApiResult { Body = "null" }.TryReadBody<Refusal>(out _));
        }

        [TestMethod]
        public void ToleratesANullResult()
        {
            ApiResult result = null;
            Assert.IsFalse(result.TryReadBody<Refusal>(out _));
            Assert.IsNull(result.ReadBodyOrDefault<Refusal>());
        }

        [TestMethod]
        public void ReadBodyOrDefaultReturnsTheBodyOrTheDefault()
        {
            Assert.AreEqual("X",
                new ApiResult { Body = "{\"errorCode\":\"X\"}" }.ReadBodyOrDefault<Refusal>().ErrorCode);
            Assert.IsNull(new ApiResult { Body = "nonsense" }.ReadBodyOrDefault<Refusal>());
        }
    }
}
