using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Logging;
using Moq;

namespace MiniValidation.AspNetCore.Tests
{
    public class ValidatedOfT
    {
        [Theory]
        [InlineData("{\"Name\":\"Test Value\"}")]
        [InlineData("{\"name\":\"Test Value\"}")]
        public async Task BindAsync_Returns_Valid_Object_For_Valid_Json(string jsonBody)
        {
            var (httpContext, _, _, _) = CreateMockHttpContext(jsonBody);
            var parameterInfo = new Mock<ParameterInfo>();

            var result = await Validated<TestType>.BindAsync(httpContext.Object, parameterInfo.Object);

            Assert.NotNull(result.Value);
            if (result.Value == null) throw new InvalidOperationException("Result should not be null here.");

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("{\"Name\":\"\"}", 1)]
        [InlineData("{\"WrongName\":\"A value\"}", 1)]
        [InlineData("{\"Name\":null}", 1)]
        [InlineData("{}", 1)]
        public async Task BindAsync_Returns_Invalid_Object_For_Invalid_Json(string jsonBody, int expectedErrorCount)
        {
            var (httpContext, _, _, _) = CreateMockHttpContext(jsonBody);
            var parameterInfo = new Mock<ParameterInfo>();

            var result = await Validated<TestType>.BindAsync(httpContext.Object, parameterInfo.Object);

            Assert.NotNull(result.Value);
            if (result.Value == null) throw new InvalidOperationException("Result should not be null here.");

            Assert.False(result.IsValid);
            Assert.Equal(expectedErrorCount, result.Errors.Count);
        }

        [Fact]
        public async Task BindAsync_Returns_Null_For_Null_Request_Body()
        {
            var (httpContext, _, _, _) = CreateMockHttpContext("null");
            var parameterInfo = new Mock<ParameterInfo>();

            var result = await Validated<TestType>.BindAsync(httpContext.Object, parameterInfo.Object);

            Assert.Null(result.Value);
        }

        [Fact]
        public async Task BindAsync_Returns_Null_For_Request_With_No_Body()
        {
            var (httpContext, _, httpRequest, _) = CreateMockHttpContext();
            httpRequest.SetupGet(x => x.ContentType).Returns("application/json");
            var parameterInfo = new Mock<ParameterInfo>();

            var result = await Validated<TestType>.BindAsync(httpContext.Object, parameterInfo.Object);

            Assert.Null(result.Value);
        }

        [Fact]
        public async Task BindAsync_Uses_Binding_Logic_Of_Wrapped_Type()
        {
            var (httpContext, _, httpRequest, _) = CreateMockHttpContext("{}");
            httpRequest.SetupGet(x => x.ContentType).Returns("application/json");
            var parameterInfo = new Mock<ParameterInfo>();

            var result = await Validated<TestBindableType>.BindAsync(httpContext.Object, parameterInfo.Object);

            Assert.NotNull(result.Value);
            Assert.True(httpContext.Object.Items[nameof(TestBindableType)] switch { true => true, _ => false });
        }

        [Fact]
        public async Task BindAsync_Returns_415_For_Non_Json_Request()
        {
            var (httpContext, _, httpRequest, services) = CreateMockHttpContext("some text");
            httpRequest.SetupGet(x => x.ContentType).Returns("text/plain");
            services.Setup(x => x.GetService(It.Is<Type>(t => t == typeof(ILoggerFactory)))).Returns(new LoggerFactory());
            var parameterInfo = new Mock<ParameterInfo>();

            var result = await Validated<TestType>.BindAsync(httpContext.Object, parameterInfo.Object);

            Assert.Equal(StatusCodes.Status415UnsupportedMediaType, result.DefaultBindingResultStatusCode);
            Assert.Equal(1, result.Errors.Count);
        }

        [Fact]
        public async Task BindAsync_Returns_400_For_Empty_Json_Request()
        {
            var (httpContext, _, httpRequest, services) = CreateMockHttpContext("");
            httpRequest.SetupGet(x => x.Body).Returns(new MemoryStream());
            services.Setup(x => x.GetService(It.Is<Type>(t => t == typeof(ILoggerFactory)))).Returns(new LoggerFactory());

            var parameterInfo = new Mock<ParameterInfo>();

            var result = await Validated<TestType>.BindAsync(httpContext.Object, parameterInfo.Object);

            Assert.Equal(StatusCodes.Status400BadRequest, result.DefaultBindingResultStatusCode);
            Assert.Equal(1, result.Errors.Count);
        }

        private static (Mock<HttpContext>, Mock<IFeatureCollection>, Mock<HttpRequest>, Mock<IServiceProvider>) CreateMockHttpContext(string? requestBody = null)
        {
            var httpContext = new Mock<HttpContext>();
            var features = new Mock<IFeatureCollection>();
            var httpRequest = new Mock<HttpRequest>();
            var httpResponse = new Mock<HttpResponse>();
            var serviceProvider = new Mock<IServiceProvider>();
            var items = new Dictionary<object, object?>();

            httpRequest.SetupGet(x => x.Method).Returns("POST");
            httpRequest.SetupGet(x => x.HttpContext).Returns(httpContext.Object);
            httpResponse.SetupProperty(x => x.StatusCode);
            httpContext.SetupGet(x => x.Items).Returns(items);
            httpContext.SetupGet(x => x.Features).Returns(features.Object);
            httpContext.SetupGet(x => x.Request).Returns(httpRequest.Object);
            httpContext.SetupGet(x => x.Response).Returns(httpResponse.Object);
            httpContext.SetupGet(x => x.RequestAborted).Returns(CancellationToken.None);
            httpContext.SetupGet(x => x.RequestServices).Returns(serviceProvider.Object);

            if (requestBody != null)
            {
                var bodyDetectionFeature = new Mock<IHttpRequestBodyDetectionFeature>();
                var bodyBytes = Encoding.UTF8.GetBytes(requestBody);
                bodyDetectionFeature.SetupGet(x => x.CanHaveBody).Returns(true);
                features.Setup(x => x.Get<IHttpRequestBodyDetectionFeature>()).Returns(bodyDetectionFeature.Object);
                httpRequest.SetupGet(x => x.ContentType).Returns("application/json");
                httpRequest.SetupGet(x => x.ContentLength).Returns(bodyBytes.Length);
                httpRequest.SetupGet(x => x.Body).Returns(new MemoryStream(bodyBytes));
            }

            return (httpContext, features, httpRequest, serviceProvider);
        }

        private class TestType
        {
            [Required]
            public string? Name {  get; set; }
        }

        private class TestBindableType
        {
            [Required]
            public string? Name { get; set; }

            public static async ValueTask<TestBindableType?> BindAsync(HttpContext context, ParameterInfo parameter)
            {
                context.Items[nameof(TestBindableType)] = true;
                return await context.Request.ReadFromJsonAsync<TestBindableType>();
            }
        }

        private class AppendToStringJsonConverter : JsonConverter<string>
        {
            private readonly string _suffix;

            public AppendToStringJsonConverter(string suffix)
            {
                _suffix = suffix;
            }

            public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return reader.GetString() + _suffix;
            }

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value, options);
            }
        }
    }
}