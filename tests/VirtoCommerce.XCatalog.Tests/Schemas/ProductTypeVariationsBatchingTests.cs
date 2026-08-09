using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GraphQL;
using GraphQL.DataLoader;
using GraphQL.Types;
using GraphQLParser.AST;
using MediatR;
using Moq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Queries;
using VirtoCommerce.XCatalog.Core.Schemas;
using VirtoCommerce.XCatalog.Tests.Helpers;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Schemas
{
    /// <summary>
    /// The subject is the number of <see cref="LoadProductsQuery"/> sends a page of masters produces, not the
    /// variations that come back: the returned variations are correct both before and after batching, so only
    /// the send count scales with the bug.
    /// </summary>
    public class ProductTypeVariationsBatchingTests : XCatalogMoqHelper
    {
        [Fact]
        public async Task ResolveVariationsField_ThreeMastersOnOnePage_SendsOneLoadProductsQuery()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets);

            var masters = new[] { "m1", "m2", "m3" }
                .Select((id, i) => new ExpProduct
                {
                    IndexedProduct = new CatalogProduct { Id = id, IsActive = true },
                    IndexedVariationIds = [$"v{i}a", $"v{i}b"],
                })
                .ToList();

            // "name" alongside "id" keeps the selection deliberately not id-only, so this exercises batching
            // rather than any selection that can be answered from the master's own document.
            var results = await ResolveVariationNodesAsync(mediator, masters.Select(x => (x, new[] { "id", "name" })).ToArray());

            sentFieldSets.Should().HaveCount(1);
            results.SelectMany(x => x).Should().HaveCount(6);
        }

        [Fact]
        public async Task ResolveVariationsField_TwoAliasesWithDifferentSubfields_DoNotShareALoader()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets);

            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["v1", "v2"],
            };

            // Neither alias is id-only: a later gate short-circuits an id-only selection before the loader is
            // reached, which would make this test fail for a reason that is not the key.
            await ResolveVariationNodesAsync(mediator, (master, ["id", "name"]), (master, ["id", "code"]));

            sentFieldSets.Should().HaveCount(2);

            // Compare the CONTENT of the two field sets, not the two list objects: OnlyHaveUniqueItems over
            // IList<string> compares references, which differ by construction, so it would pass against a
            // loader key that ignores the field set entirely.
            sentFieldSets
                .Select(x => string.Join(',', x.OrderBy(f => f, StringComparer.Ordinal)))
                .Should().OnlyHaveUniqueItems();
        }

        /// <summary>
        /// Drives the registered <c>variations</c> field over several sibling nodes sharing one
        /// <see cref="DataLoaderContext"/>, the way one request does.
        /// </summary>
        private async Task<IList<IList<ExpVariation>>> ResolveVariationNodesAsync(
            IMediator mediator,
            params (ExpProduct Master, string[] SubFields)[] nodes)
        {
            // A mock IDataLoaderContextAccessor returns a null Context, and the loader would never be reached.
            _dataLoaderContextAccessorMock.Setup(x => x.Context).Returns(new DataLoaderContext());

            var productType = new ProductType(_dataLoaderContextAccessorMock.Object);
            var variationsField = productType.Fields.First(x => x.Name.EqualsIgnoreCase("variations"));

            // Every node must be resolved - and its loader result queued - before the first one is completed.
            // This mirrors the execution strategy, which runs ExecuteNodeAsync across sibling nodes and only
            // then completes the pending data-loader nodes. Resolving and completing one node at a time
            // dispatches a batch of one each time and reports one send per node against a correctly batched
            // implementation - a failure that is not the bug.
            var pending = new List<object>();
            foreach (var (master, subFields) in nodes)
            {
                pending.Add(await variationsField.Resolver.ResolveAsync(CreateResolveContext(master, mediator, subFields)));
            }

            var results = new List<IList<ExpVariation>>();
            foreach (var item in pending)
            {
                results.Add(await CompleteNodeAsync(item));
            }

            return results;
        }

        /// <summary>
        /// Completes one resolved node the way the execution strategy does - by the resolved value's runtime
        /// type. A resolver that loaded eagerly hands back the finished sequence instead of a pending result.
        /// </summary>
        private static async Task<IList<ExpVariation>> CompleteNodeAsync(object resolved)
        {
            var value = resolved is IDataLoaderResult loaderResult
                ? await loaderResult.GetResultAsync()
                : resolved;

            return ((IEnumerable<ExpVariation>)value).ToList();
        }

        private static IResolveFieldContext CreateResolveContext(ExpProduct source, IMediator mediator, string[] subFields)
        {
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock.Setup(x => x.GetService(typeof(IMediator))).Returns(mediator);

            return new ResolveFieldContext
            {
                Source = source,
                // GetCatalogQuery reaches the current principal through a GraphQLUserContext cast, which a plain
                // dictionary fails.
                UserContext = new GraphQLUserContext(null) { { "cultureName", CULTURE_NAME } },
                // The resolver reaches the mediator through context.GetMediator(), which requires RequestServices.
                RequestServices = serviceProviderMock.Object,
                SubFields = subFields.ToDictionary(
                    x => x,
                    x => (new GraphQLField(new GraphQLName(x)), (FieldType)null)),
            };
        }

        private static IMediator CreateRecordingMediator(IList<IList<string>> sentFieldSets)
        {
            var mediatorMock = new Mock<IMediator>();

            mediatorMock
                .Setup(x => x.Send(It.IsAny<LoadProductsQuery>(), It.IsAny<CancellationToken>()))
                .Returns((LoadProductsQuery query, CancellationToken _) =>
                {
                    sentFieldSets.Add(query.IncludeFields.ToList());

                    var products = query.ObjectIds
                        .Select(id => new ExpProduct { IndexedProduct = new CatalogProduct { Id = id, IsActive = true } })
                        .ToList();

                    return Task.FromResult(new LoadProductResponse(products));
                });

            return mediatorMock.Object;
        }
    }
}
