using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Celigo.NetSuite.ConnectionGuard.Abstractions;
using Celigo.NetSuite.ConnectionGuard.Client;
using Celigo.NetSuite.ConnectionGuard.Decorators;
using Celigo.ServiceManager.NetSuite.REST;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Celigo.ServiceManager.NetSuite.REST.UnitTests
{
    public class GivenGuardedRestClient
    {
        private const string ConnectionId = "conn-1";
        private static readonly Uri TestUri = new("https://123456.suitetalk.api.netsuite.com/services/rest/record/v1/customer");

        private sealed class StubRestClient : IRestClient
        {
            private readonly HttpResponseMessage _response;

            public int Calls { get; private set; }

            public StubRestClient(HttpResponseMessage response) => _response = response;

            public Task<HttpResponseMessage> Get(string account, Uri requestUri, string token, string tokenSecret)
            {
                Calls++;
                return Task.FromResult(_response);
            }

            public Task<HttpResponseMessage> Post<T>(string account, Uri requestUri, string token, string tokenSecret, T content)
            {
                Calls++;
                return Task.FromResult(_response);
            }

            public Task<HttpResponseMessage> Put<T>(string account, Uri requestUri, string token, string tokenSecret, T content)
            {
                Calls++;
                return Task.FromResult(_response);
            }

            public Task<HttpResponseMessage> Patch<T>(string account, Uri requestUri, string token, string tokenSecret, T content)
            {
                Calls++;
                return Task.FromResult(_response);
            }

            public Task<HttpResponseMessage> Delete(string account, Uri requestUri, string token, string tokenSecret)
            {
                Calls++;
                return Task.FromResult(_response);
            }
        }

        private static NsCallContext Ctx() => new()
        {
            ConnectionId = ConnectionId,
            Account = "123456",
            Surface = NetSuiteSurface.Rest,
            Kind = NetSuiteCallKind.Background
        };

        private static (GuardedRestClient client, IConnectionStatusClient status, StubRestClient inner) Build(
            HttpResponseMessage response, NsCallContext ctx)
        {
            var status = A.Fake<IConnectionStatusClient>();
            A.CallTo(() => status.GetStatusAsync(ConnectionId, A<CancellationToken>._))
                .Returns(new ConnectionStatusDto { ConnectionId = ConnectionId });

            var metrics = A.Fake<IGuardMetrics>();
            var pipeline = new GuardPipeline(
                status, metrics, Options.Create(new ConnectionGuardOptions()), NullLogger<GuardPipeline>.Instance);

            var accessor = new DefaultNsCallContextAccessor();
            if (ctx is not null)
            {
                accessor.Current = ctx;
            }

            var inner = new StubRestClient(response);
            return (new GuardedRestClient(inner, pipeline, accessor), status, inner);
        }

        [Fact]
        public async Task NoContext_PassesThroughUngated()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var (client, status, inner) = Build(response, ctx: null);

            var result = await client.Get("acct", TestUri, "tok", "sec");

            result.Should().BeSameAs(response);
            inner.Calls.Should().Be(1);
            A.CallTo(() => status.GetStatusAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Online_Success_ReturnsResponse_NoMarkOffline()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var (client, status, _) = Build(response, Ctx());

            var result = await client.Get("acct", TestUri, "tok", "sec");

            result.Should().BeSameAs(response);
            A.CallTo(() => status.MarkOfflineAsync(A<NsCallContext>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Online_InvalidLogin_MarksOfflineForRest_AndThrows()
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":{\"code\":\"INVALID_LOGIN\"}}")
            };
            var (client, status, _) = Build(response, Ctx());

            var act = async () => await client.Get("acct", TestUri, "tok", "sec");

            await act.Should().ThrowAsync<ConnectionOfflineException>();
            A.CallTo(() => status.MarkOfflineAsync(
                    A<NsCallContext>.That.Matches(c => c.ConnectionId == ConnectionId && c.Surface == NetSuiteSurface.Rest),
                    "INVALID_LOGIN",
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Online_EmptyBody403_ReturnsResponse_NoMarkOffline()
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("") };
            var (client, status, _) = Build(response, Ctx());

            var result = await client.Get("acct", TestUri, "tok", "sec");

            result.Should().BeSameAs(response);
            A.CallTo(() => status.MarkOfflineAsync(A<NsCallContext>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Online_400InvalidParameter_ReturnsResponse_NoMarkOffline()
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("INVALID_PARAMETER: bad field")
            };
            var (client, status, _) = Build(response, Ctx());

            var result = await client.Get("acct", TestUri, "tok", "sec");

            result.Should().BeSameAs(response);
            A.CallTo(() => status.MarkOfflineAsync(A<NsCallContext>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_Online_Success_ReturnsResponse()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var (client, status, inner) = Build(response, Ctx());

            var result = await client.Post("acct", TestUri, "tok", "sec", new { name = "test" });

            result.Should().BeSameAs(response);
            inner.Calls.Should().Be(1);
        }

        [Fact]
        public async Task Delete_Online_InvalidLogin_Throws()
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":{\"code\":\"INVALID_LOGIN\"}}")
            };
            var (client, status, _) = Build(response, Ctx());

            var act = async () => await client.Delete("acct", TestUri, "tok", "sec");

            await act.Should().ThrowAsync<ConnectionOfflineException>();
        }
    }
}
