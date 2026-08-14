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
    public class ProductTypeVariationsBatchingTests : XCatalogMoqHelper
    {
        private static readonly string[] _subFields = ["id", "name"];

        private readonly HashSet<string> _inactiveIds = [];
        private readonly HashSet<string> _unresolvedIds = [];

        private readonly ProductType _productType;
        private readonly IMediator _mediator;

        private readonly List<IList<string>> _sentFieldSets = [];

        public ProductTypeVariationsBatchingTests()
        {
            _dataLoaderContextAccessorMock.Setup(x => x.Context).Returns(new DataLoaderContext());

            _productType = new ProductType(_dataLoaderContextAccessorMock.Object);
            _mediator = CreateRecordingMediator();
        }

        [Fact]
        public async Task ResolveVariationsField_ThreeMastersWithSameSubfields_SendsOneQueryAndSplitsResultsByMaster()
        {
            var masters = new[] { "m1", "m2", "m3" }
                .Select((id, i) => new ExpProduct
                {
                    IndexedProduct = new CatalogProduct { Id = id, IsActive = true },
                    IndexedVariationIds = [$"v{i}a", $"v{i}b"],
                })
                .ToList();

            var results = await ResolveVariationNodesAsync(masters.Select(x => (x, _subFields)).ToArray());

            _sentFieldSets.Should().HaveCount(1);

            for (var i = 0; i < masters.Count; i++)
            {
                results[i].Select(x => x.Id).Should().BeEquivalentTo(masters[i].IndexedVariationIds);
            }
        }

        [Fact]
        public async Task ResolveVariationsField_TwoAliasesWithDifferentSubfields_DoNotShareALoader()
        {
            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["v1", "v2"],
            };

            await ResolveVariationNodesAsync((master, ["id", "name"]), (master, ["id", "code"]));

            _sentFieldSets.Should().HaveCount(2);

            _sentFieldSets
                .Select(x => string.Join(',', x.OrderBy(f => f, StringComparer.Ordinal)))
                .Should().OnlyHaveUniqueItems();
        }

        [Fact]
        public async Task ResolveMasterVariationField_ThreeVariationsWithSameSubfields_SendsOneQuery()
        {
            var variations = new[] { "p1", "p2", "p3" }
                .Select((id, i) => new ExpProduct
                {
                    IndexedProduct = new CatalogProduct { Id = $"v{i}", MainProductId = id, IsActive = true },
                })
                .ToList();

            var results = await ResolveMasterVariationNodesAsync(variations.Select(x => (x, _subFields)).ToArray());

            _sentFieldSets.Should().HaveCount(1);

            results.Select(x => x.Id).Should().Equal("p1", "p2", "p3");
        }

        [Fact]
        public async Task ResolveVariationsAndMasterVariation_WithSameSubfields_SendsOneQuery()
        {
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

            var pendingVariations = await variationsField.Resolver.ResolveAsync(CreateResolveContext(master, _subFields));
            var pendingMasterVariation = await masterVariationField.Resolver.ResolveAsync(CreateResolveContext(variation, _subFields));

            var resolvedVariations = await CompleteNodeAsync<IList<ExpVariation>>(pendingVariations);
            var resolvedMasterVariation = await CompleteNodeAsync<ExpVariation>(pendingMasterVariation);

            _sentFieldSets.Should().HaveCount(1);
            resolvedVariations.Should().ContainSingle();
            resolvedMasterVariation.Should().NotBeNull();
        }

        [Fact]
        public async Task ResolveVariationsField_SelectionIsIdOnly_SendsTheQuery()
        {
            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["v1", "v2"],
            };

            // Answering this from the master's own document is the tempting shortcut, and it drops the
            // startDate/endDate window that the search applies to every other selection - so an id-only
            // selection would return variations outside their validity window that "id name" filters out.
            var results = await ResolveVariationNodesAsync((master, ["id"]));

            _sentFieldSets.Should().HaveCount(1);
            results.Single().Select(x => x.Id).Should().Equal("v1", "v2");
        }

        [Fact]
        public async Task ResolveVariationsField_SelectionOmitsIsActive_RequestsItAnyway()
        {
            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["v1"],
            };

            await ResolveVariationNodesAsync((master, ["id", "name"]));

            _sentFieldSets.Single().Should().Contain("isActive",
                "the projection filters on IsActive, and IncludeFields narrows what the document carries - "
                + "fetched without that field it deserializes as null, every variation is filtered out, and "
                + "the field returns empty for every product with no error and no failing assertion elsewhere");
        }

        [Fact]
        public async Task ResolveVariationsField_IdsExceedTheBatchCap_SplitsIntoTwoLoadProductsQueries()
        {
            var ids = Enumerable.Range(0, 201).Select(i => $"v{i}").ToList();
            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ids,
            };

            var results = await ResolveVariationNodesAsync((master, _subFields));

            _sentFieldSets.Should().HaveCount(2);
            results.Single().Should().HaveCount(201);
        }

        [Fact]
        public async Task ResolveVariationsField_IdSharedByTwoMasters_BothReceiveTheSameLoadedInstance()
        {
            var masterA = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "ma", IsActive = true },
                IndexedVariationIds = ["shared", "onlyA"],
            };
            var masterB = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "mb", IsActive = true },
                IndexedVariationIds = ["shared", "onlyB"],
            };

            var results = await ResolveVariationNodesAsync((masterA, _subFields), (masterB, _subFields));

            _sentFieldSets.Should().HaveCount(1);

            var sharedFromA = results[0].Single(x => x.Id == "shared");
            var sharedFromB = results[1].Single(x => x.Id == "shared");

            sharedFromA.IndexedProduct.Should().BeSameAs(sharedFromB.IndexedProduct);
        }

        [Fact]
        public async Task ResolveVariationsField_AnIdTheLoadDoesNotResolve_IsAbsentFromTheResult()
        {
            _unresolvedIds.Add("gone");

            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["v1", "gone"],
            };

            var results = await ResolveVariationNodesAsync((master, _subFields));

            results.Single().Select(x => x.Id).Should().Equal("v1");
        }

        [Fact]
        public async Task ResolveVariationsField_MixOfActiveAndInactiveVariations_ExcludesInactiveOnes()
        {
            _inactiveIds.Add("inactive");

            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["active1", "inactive"],
            };

            var results = await ResolveVariationNodesAsync((master, _subFields));

            results.Single().Select(x => x.Id).Should().Equal("active1");
        }

        [Fact]
        public async Task ResolveMasterVariationField_InactiveMainProduct_IsStillReturned()
        {
            _inactiveIds.Add("m1");

            var variation = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "v1", MainProductId = "m1", IsActive = true },
            };

            var results = await ResolveMasterVariationNodesAsync((variation, _subFields));

            results.Single().Id.Should().Be("m1");
        }

        [Fact]
        public async Task ResolveMasterVariationField_MainProductIsNotFound_ReturnsNull()
        {
            _unresolvedIds.Add("m1");

            var variation = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "v1", MainProductId = "m1", IsActive = true },
            };

            var results = await ResolveMasterVariationNodesAsync((variation, _subFields));

            results.Single().Should().BeNull();
            _sentFieldSets.Should().HaveCount(1);
        }

        [Fact]
        public async Task ResolveVariationsField_NoVariationIds_SendsNoQuery()
        {
            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = [],
            };

            var results = await ResolveVariationNodesAsync((master, _subFields));

            results.Single().Should().BeEmpty();
            _sentFieldSets.Should().BeEmpty();
        }

        [Fact]
        public async Task ResolveMasterVariationField_NoMainProductId_SendsNoQuery()
        {
            var product = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "p1", IsActive = true },
            };

            var results = await ResolveMasterVariationNodesAsync((product, _subFields));

            results.Single().Should().BeNull();
            _sentFieldSets.Should().BeEmpty();
        }

        private Task<IList<IList<ExpVariation>>> ResolveVariationNodesAsync(params (ExpProduct Source, string[] SubFields)[] nodes)
        {
            return ResolveNodesAsync<IList<ExpVariation>>("variations", nodes);
        }

        private Task<IList<ExpVariation>> ResolveMasterVariationNodesAsync(params (ExpProduct Source, string[] SubFields)[] nodes)
        {
            return ResolveNodesAsync<ExpVariation>("masterVariation", nodes);
        }

        private async Task<IList<T>> ResolveNodesAsync<T>(string fieldName, (ExpProduct Source, string[] SubFields)[] nodes)
        {
            var field = _productType.Fields.First(x => x.Name.EqualsIgnoreCase(fieldName));

            // Every node is resolved before any is completed. Completing one at a time dispatches a batch of one
            // each time and reports one send per node against a correctly batched resolver.
            var pending = new List<object>();
            foreach (var (source, subFields) in nodes)
            {
                pending.Add(await field.Resolver.ResolveAsync(CreateResolveContext(source, subFields)));
            }

            var results = new List<T>();
            foreach (var item in pending)
            {
                results.Add(await CompleteNodeAsync<T>(item));
            }

            return results;
        }

        private static async Task<T> CompleteNodeAsync<T>(object resolved)
        {
            resolved.Should().BeAssignableTo<IDataLoaderResult<T>>();

            return await ((IDataLoaderResult<T>)resolved).GetResultAsync();
        }

        private ResolveFieldContext CreateResolveContext(ExpProduct source, string[] subFields)
        {
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock.Setup(x => x.GetService(typeof(IMediator))).Returns(_mediator);

            return new ResolveFieldContext
            {
                Source = source,
                UserContext = new GraphQLUserContext(null) { { "cultureName", CULTURE_NAME } },
                RequestServices = serviceProviderMock.Object,
                SubFields = subFields.ToDictionary(
                    x => x,
                    x => (new GraphQLField(new GraphQLName(x)), (FieldType)null)),
            };
        }

        private IMediator CreateRecordingMediator()
        {
            var mediatorMock = new Mock<IMediator>();

            mediatorMock
                .Setup(x => x.Send(It.IsAny<LoadProductsQuery>(), It.IsAny<CancellationToken>()))
                .Returns((LoadProductsQuery query, CancellationToken _) =>
                {
                    _sentFieldSets.Add(query.IncludeFields.ToList());

                    var products = query.ObjectIds
                        .Where(id => !_unresolvedIds.Contains(id))
                        .Select(id => new ExpProduct
                        {
                            IndexedProduct = new CatalogProduct { Id = id, IsActive = !_inactiveIds.Contains(id) },
                        })
                        .ToList();

                    return Task.FromResult(new LoadProductResponse(products));
                });

            return mediatorMock.Object;
        }
    }
}
