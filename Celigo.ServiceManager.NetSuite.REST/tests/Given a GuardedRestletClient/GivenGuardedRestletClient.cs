using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Celigo.NetSuite.ConnectionGuard.Abstractions;
using Celigo.NetSuite.ConnectionGuard.Client;
using Celigo.NetSuite.ConnectionGuard.Decorators;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Celigo.ServiceManager.NetSuite.REST.UnitTests
{
    public class GivenGuardedRestletClient
    {
        private const string ConnectionId = "conn-1";

        private sealed class StubRestletClient : IRestletClient
        {
            private readonly HttpResponseMessage _response;

            public int Calls { get; private set; }

            public StubRestletClient(HttpResponseMessage response) => _response = response;

            public Task<HttpResponseMessage> Get(in string account, in string token, in string tokenSecret, IReadOnlyDictionary<string, string> queryParams = null)
            {
                Calls++;
                return Task.FromResult(_response);
            }

            public Task<HttpResponseMessage> Delete(in string account, in string token, in string tokenSecret, IReadOnlyDictionary<string, string> queryParams = null)
            {
                Calls++;
                return Task.FromResult(_response);
            }

            public Task<HttpResponseMessage> Post<T>(in string account, in string token, in string tokenSecret, in T message, IReadOnlyDictionary<string, string> queryParams = null)
            {
                Calls++;
                return Task.FromResult(_response);
            }

            public Task<HttpResponseMessage> Put<T>(in string account, in string token, in string tokenSecret, in T message, IReadOnlyDictionary<string, string> queryParams = null)
            {
                Calls++;
                return Task.FromResult(_response);
            }

            public Task<HttpResponseMessage> Get(in Passport passport, IReadOnlyDictionary<string, string> queryParams = null) => Task.FromResult(_response);

            public Task<HttpResponseMessage> Post<T>(in Passport passport, in T message, IReadOnlyDictionary<string, string> queryParams = null) => Task.FromResult(_response);

            [Obsolete]
            public Task<HttpResponseMessage> Get(in string account, in string token, in string tokenSecret, (string key, string value) queryParam, params (string key, string value)[] queryParams) => Task.FromResult(_response);

            [Obsolete]
            public Task<HttpResponseMessage> Get(in Passport passport, (string key, string value) queryParam, params (string key, string value)[] queryParams) => Task.FromResult(_response);

            [Obsolete]
            public Task<HttpResponseMessage> Post<T>(in string account, in string token, in string tokenSecret, in T message, (string key, string value) queryParam, params (string key, string value)[] queryParams) => Task.FromResult(_response);

            [Obsolete]
            public Task<HttpResponseMessage> Post<T>(in Passport passport, in T message, (string key, string value) queryParam, params (string key, string value)[] queryParams) => Task.FromResult(_response);
        }

        private static NsCallContext Ctx() => new()
        {
            ConnectionId = ConnectionId,
            Account = "123456",
            Surface = NetSuiteSurface.Rest,
            Kind = NetSuiteCallKind.Background
        };

        private static (GuardedRestletClient client, IConnectionStatusClient status, StubRestletClient inner) Build(
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

            var inner = new StubRestletClient(response);
            return (new GuardedRestletClient(inner, pipeline, accessor), status, inner);
        }

        [Fact]
        public async Task NoContext_PassesThroughUngated()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var (client, status, inner) = Build(response, ctx: null);

            var result = await client.Get("acct", "tok", "sec");

            result.Should().BeSameAs(response);
            inner.Calls.Should().Be(1);
            A.CallTo(() => status.GetStatusAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Online_Success_ReturnsResponse_NoMarkOffline()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var (client, status, _) = Build(response, Ctx());

            var result = await client.Get("acct", "tok", "sec");

            result.Should().BeSameAs(response);
            A.CallTo(() => status.MarkOfflineAsync(A<NsCallContext>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Online_InvalidLoginAttempt_MarksOfflineForRestlet_AndThrows()
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("{\"error\":\"INVALID_LOGIN_ATTEMPT\"}")
            };
            var (client, status, _) = Build(response, Ctx());

            var act = async () => await client.Get("acct", "tok", "sec");

            await act.Should().ThrowAsync<ConnectionOfflineException>();
            A.CallTo(() => status.MarkOfflineAsync(
                    A<NsCallContext>.That.Matches(c => c.ConnectionId == ConnectionId && c.Surface == NetSuiteSurface.Restlet),
                    "INVALID_LOGIN_ATTEMPT",
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Online_EmptyBody403Block_ReturnsResponse_NoMarkOffline()
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("") };
            var (client, status, _) = Build(response, Ctx());

            var result = await client.Get("acct", "tok", "sec");

            result.Should().BeSameAs(response);
            A.CallTo(() => status.MarkOfflineAsync(A<NsCallContext>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }
    }
}
