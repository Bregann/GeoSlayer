using GeoSlayer.Domain.Database.Models;
using GeoSlayer.Domain.Enums;
using GeoSlayer.Domain.Interfaces.Helpers;
using Microsoft.AspNetCore.Http;
using Moq;
using Moq.Protected;
using System.Security.Claims;
using SecurityClaim = System.Security.Claims.Claim;

namespace GeoSlayer.Tests.Infrastructure
{
    /// <summary>
    /// Factory class to create common mocks used across tests
    /// </summary>
    public static class MockFactory
    {
        /// <summary>
        /// Creates a mock IHttpContextAccessor with a user identity
        /// </summary>
        public static Mock<IHttpContextAccessor> CreateHttpContextAccessor(string userId = "1", string firstName = "Test")
        {
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            var claims = new List<SecurityClaim>
            {
                new SecurityClaim(ClaimTypes.NameIdentifier, userId),
                new SecurityClaim(ClaimTypes.Name, firstName)
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            var context = new DefaultHttpContext
            {
                User = claimsPrincipal
            };

            mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(context);

            return mockHttpContextAccessor;
        }

        /// <summary>
        /// Creates a mock IUserContextHelper. Pass a seeded user to have GetUser() return it.
        /// </summary>
        public static Mock<IUserContextHelper> CreateUserContextHelper(string userId = "1", string firstName = "Test", User? user = null)
        {
            var mock = new Mock<IUserContextHelper>();
            mock.Setup(x => x.GetUserId()).Returns(user?.Id ?? userId);
            mock.Setup(x => x.GetUserFirstName()).Returns(user?.FirstName ?? firstName);

            if (user != null)
            {
                mock.Setup(x => x.GetUser()).Returns(user);
            }

            return mock;
        }

        /// <summary>
        /// Creates a mock IEnvironmentalSettingHelper
        /// </summary>
        public static Mock<IEnvironmentalSettingHelper> CreateEnvironmentalSettingHelper()
        {
            var mock = new Mock<IEnvironmentalSettingHelper>();

            // Setup common environmental settings
            mock.Setup(x => x.TryGetEnviromentalSettingValue(It.IsAny<EnvironmentalSettingEnum>()))
                .Returns("test-value");

            mock.Setup(x => x.UpdateEnviromentalSettingValue(It.IsAny<EnvironmentalSettingEnum>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            mock.Setup(x => x.LoadEnvironmentalSettings())
                .Returns(Task.CompletedTask);

            return mock;
        }

        /// <summary>
        /// Creates a mock HttpClient for testing API calls
        /// </summary>
        public static HttpClient CreateMockHttpClient(string responseContent = "{}")
        {
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();

            // Setup default response
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Content = new StringContent(responseContent)
                });

            return new HttpClient(mockHttpMessageHandler.Object) { BaseAddress = new Uri("https://test.local") };
        }
    }
}
