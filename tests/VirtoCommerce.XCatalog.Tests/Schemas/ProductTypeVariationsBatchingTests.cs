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
        private readonly ProductType _productType;

        public ProductTypeVariationsBatchingTests()
        {
            // A mock IDataLoaderContextAccessor returns a null Context, and the loader would never be reached.
            // One context per test, shared by every node, is what a single request gives the resolvers.
            _dataLoaderContextAccessorMock.Setup(x => x.Context).Returns(new DataLoaderContext());

            _productType = new ProductType(_dataLoaderContextAccessorMock.Object);
        }

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

        [Fact]
        public async Task ResolveMasterVariationField_ThreeVariationsOnOnePage_SendsOneLoadProductsQuery()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets);

            var variations = new[] { "p1", "p2", "p3" }
                .Select((id, i) => new ExpProduct
                {
                    IndexedProduct = new CatalogProduct { Id = $"v{i}", MainProductId = id, IsActive = true },
                })
                .ToList();

            var results = await ResolveMasterVariationNodesAsync(
                mediator, variations.Select(x => (x, new[] { "id", "name" })).ToArray());

            sentFieldSets.Should().HaveCount(1);
            results.Should().HaveCount(3);
        }

        [Fact]
        public async Task ResolveVariationsAndMasterVariationFields_OnOnePage_SendOneLoadProductsQuery()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets);

            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["v1"],
            };

            var variation = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "v9", MainProductId = "m2", IsActive = true },
            };

            var variationsField = _productType.Fields.First(x => x.Name.EqualsIgnoreCase("variations"));
            var masterVariationField = _productType.Fields.First(x => x.Name.EqualsIgnoreCase("masterVariation"));

            // Both nodes resolved and queued before either is completed, as in the single-field helpers.
            var pendingVariations = await variationsField.Resolver.ResolveAsync(CreateResolveContext(master, mediator, ["id", "name"]));
            var pendingMasterVariation = await masterVariationField.Resolver.ResolveAsync(CreateResolveContext(variation, mediator, ["id", "name"]));

            var resolvedVariations = await CompleteNodeAsync(pendingVariations);
            var resolvedMasterVariation = await CompleteMasterVariationNodeAsync(pendingMasterVariation);

            // The two fields build their loader key separately. Should those two compositions ever drift apart,
            // each field registers its own loader and the page silently pays a second search - nothing else here
            // would fail.
            sentFieldSets.Should().HaveCount(1);
            resolvedVariations.Should().ContainSingle();
            resolvedMasterVariation.Should().NotBeNull();
        }

        /// <summary>
        /// Drives the registered <c>variations</c> field over several sibling nodes sharing one
        /// <see cref="DataLoaderContext"/>, the way one request does.
        /// </summary>
        private async Task<IList<IList<ExpVariation>>> ResolveVariationNodesAsync(
            IMediator mediator,
            params (ExpProduct Master, string[] SubFields)[] nodes)
        {
            var variationsField = _productType.Fields.First(x => x.Name.EqualsIgnoreCase("variations"));

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
        /// Drives the registered <c>masterVariation</c> field over several sibling nodes sharing one
        /// <see cref="DataLoaderContext"/> - the single-result counterpart of <see cref="ResolveVariationNodesAsync"/>.
        /// </summary>
        private async Task<IList<ExpVariation>> ResolveMasterVariationNodesAsync(
            IMediator mediator,
            params (ExpProduct Variation, string[] SubFields)[] nodes)
        {
            var masterVariationField = _productType.Fields.First(x => x.Name.EqualsIgnoreCase("masterVariation"));

            // Same ordering requirement as ResolveVariationNodesAsync: every node resolved and queued before
            // the first one is completed.
            var pending = new List<object>();
            foreach (var (variation, subFields) in nodes)
            {
                pending.Add(await masterVariationField.Resolver.ResolveAsync(CreateResolveContext(variation, mediator, subFields)));
            }

            var results = new List<ExpVariation>();
            foreach (var item in pending)
            {
                results.Add(await CompleteMasterVariationNodeAsync(item));
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

        /// <summary>
        /// <see cref="CompleteNodeAsync"/> for <c>masterVariation</c>, whose resolved value is a single
        /// <see cref="ExpVariation"/> rather than a list.
        /// </summary>
        private static async Task<ExpVariation> CompleteMasterVariationNodeAsync(object resolved)
        {
            var value = resolved is IDataLoaderResult loaderResult
                ? await loaderResult.GetResultAsync()
                : resolved;

            return (ExpVariation)value;
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
