using System;
using Celigo.NetSuite.ConnectionGuard.Abstractions;
using Celigo.NetSuite.ConnectionGuard.Decorators;

namespace Celigo.ServiceManager.NetSuite.REST
{
    public sealed class GuardedRestletClientFactory : IRestletClientFactory
    {
        private readonly IRestletClientFactory _inner;
        private readonly GuardPipeline _guardPipeline;
        private readonly INsCallContextAccessor _contextAccessor;

        public GuardedRestletClientFactory(
            IRestletClientFactory inner,
            GuardPipeline guardPipeline,
            INsCallContextAccessor contextAccessor)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _guardPipeline = guardPipeline ?? throw new ArgumentNullException(nameof(guardPipeline));
            _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        }

        public IRestletClient CreateClient(string restletName)
        {
            var innerClient = _inner.CreateClient(restletName);
            return new GuardedRestletClient(innerClient, _guardPipeline, _contextAccessor);
        }
    }
}
