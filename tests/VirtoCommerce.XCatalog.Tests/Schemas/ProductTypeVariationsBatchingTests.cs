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
        // "name" is not one of the fields the master's document answers, so this selection is the one that
        // reaches the loader. Tests about batching use it; tests about the gate spell their selection out.
        private static readonly string[] _selectionRequiringLoad = ["id", "name"];

        private readonly ProductType _productType;

        public ProductTypeVariationsBatchingTests()
        {
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

            var results = await ResolveVariationNodesAsync(mediator, masters.Select(x => (x, _selectionRequiringLoad)).ToArray());

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

            await ResolveVariationNodesAsync(mediator, (master, ["id", "name"]), (master, ["id", "code"]));

            sentFieldSets.Should().HaveCount(2);

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
                mediator, variations.Select(x => (x, _selectionRequiringLoad)).ToArray());

            sentFieldSets.Should().HaveCount(1);

            results.Select(x => x.Id).Should().Equal("p1", "p2", "p3");
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

            var pendingVariations = await variationsField.Resolver.ResolveAsync(CreateResolveContext(master, mediator, _selectionRequiringLoad));
            var pendingMasterVariation = await masterVariationField.Resolver.ResolveAsync(CreateResolveContext(variation, mediator, _selectionRequiringLoad));

            var resolvedVariations = await CompleteNodeAsync(pendingVariations);
            var resolvedMasterVariation = await CompleteMasterVariationNodeAsync(pendingMasterVariation);

            sentFieldSets.Should().HaveCount(1);
            resolvedVariations.Should().ContainSingle();
            resolvedMasterVariation.Should().NotBeNull();
        }

        [Fact]
        public async Task ResolveVariationsField_SelectionIsIdOnly_SendsNoLoadProductsQuery()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets);

            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["v1", "v2"],
            };

            var results = await ResolveVariationNodesAsync(mediator, (master, new[] { "id" }));

            sentFieldSets.Should().BeEmpty();
            results.Single().Select(x => x.Id).Should().Equal("v1", "v2");
        }

        [Fact]
        public async Task ResolveVariationsField_SelectionNeedsALoadedField_StillSendsTheQuery()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets);

            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["v1", "v2"],
            };

            await ResolveVariationNodesAsync(mediator, (master, _selectionRequiringLoad));

            sentFieldSets.Should().HaveCount(1);
        }

        [Fact]
        public async Task ResolveVariationsField_SelectionIsIdAndTypeName_SendsNoLoadProductsQuery()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets);

            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["v1", "v2"],
            };

            // This, not the bare id-only case, is what an id-only source query looks like on the wire: a client
            // that normalises a cache adds __typename to every selection set.
            var results = await ResolveVariationNodesAsync(mediator, (master, new[] { "id", "__typename" }));

            sentFieldSets.Should().BeEmpty();
            results.Single().Select(x => x.Id).Should().Equal("v1", "v2");
        }

        [Fact]
        public async Task ResolveVariationsField_SelectionIsTypeNameBesideALoadedField_StillSendsTheQuery()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets);

            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["v1", "v2"],
            };

            await ResolveVariationNodesAsync(mediator, (master, new[] { "id", "name", "__typename" }));

            sentFieldSets.Should().HaveCount(1);
        }

        [Fact]
        public async Task ResolveVariationsField_IdsExceedTheBatchCap_SplitsIntoTwoLoadProductsQueries()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets);

            var ids = Enumerable.Range(0, 201).Select(i => $"v{i}").ToList();
            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ids,
            };

            var results = await ResolveVariationNodesAsync(mediator, (master, _selectionRequiringLoad));

            sentFieldSets.Should().HaveCount(2);
            results.Single().Should().HaveCount(201);
        }

        [Fact]
        public async Task ResolveVariationsField_IdSharedByTwoMasters_BothReceiveTheSameLoadedInstance()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets);

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

            var results = await ResolveVariationNodesAsync(
                mediator, (masterA, _selectionRequiringLoad), (masterB, _selectionRequiringLoad));

            sentFieldSets.Should().HaveCount(1);

            var sharedFromA = results[0].Single(x => x.Id == "shared");
            var sharedFromB = results[1].Single(x => x.Id == "shared");

            sharedFromA.IndexedProduct.Should().BeSameAs(sharedFromB.IndexedProduct);
        }

        [Fact]
        public async Task ResolveVariationsField_AnIdTheLoadDoesNotResolve_IsAbsentFromTheResult()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets, unresolvedIds: new HashSet<string> { "gone" });

            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["v1", "gone"],
            };

            var results = await ResolveVariationNodesAsync(mediator, (master, _selectionRequiringLoad));

            results.Single().Select(x => x.Id).Should().Equal("v1");
        }

        [Fact]
        public async Task ResolveVariationsField_MixOfActiveAndInactiveVariations_ExcludesInactiveOnes()
        {
            var sentFieldSets = new List<IList<string>>();
            var mediator = CreateRecordingMediator(sentFieldSets, isActive: id => id != "inactive");

            var master = new ExpProduct
            {
                IndexedProduct = new CatalogProduct { Id = "m1", IsActive = true },
                IndexedVariationIds = ["active1", "inactive"],
            };

            var results = await ResolveVariationNodesAsync(mediator, (master, _selectionRequiringLoad));

            results.Single().Select(x => x.Id).Should().Equal("active1");
        }

        [Fact]
        public async Task ResolveVariationsField_ThreeMastersOnOnePage_EachReceivesExactlyItsOwnVariationIds()
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

            var results = await ResolveVariationNodesAsync(mediator, masters.Select(x => (x, _selectionRequiringLoad)).ToArray());

            for (var i = 0; i < masters.Count; i++)
            {
                results[i].Select(x => x.Id).Should().BeEquivalentTo(masters[i].IndexedVariationIds);
            }
        }

        private async Task<IList<IList<ExpVariation>>> ResolveVariationNodesAsync(
            IMediator mediator,
            params (ExpProduct Master, string[] SubFields)[] nodes)
        {
            var variationsField = _productType.Fields.First(x => x.Name.EqualsIgnoreCase("variations"));

            // Every node is resolved before any is completed. Completing one at a time dispatches a batch of one
            // each time and reports one send per node against a correctly batched resolver.
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

        private async Task<IList<ExpVariation>> ResolveMasterVariationNodesAsync(
            IMediator mediator,
            params (ExpProduct Variation, string[] SubFields)[] nodes)
        {
            var masterVariationField = _productType.Fields.First(x => x.Name.EqualsIgnoreCase("masterVariation"));

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

        private static async Task<IList<ExpVariation>> CompleteNodeAsync(object resolved)
        {
            var value = resolved is IDataLoaderResult loaderResult
                ? await loaderResult.GetResultAsync()
                : resolved;

            return ((IEnumerable<ExpVariation>)value).ToList();
        }

        private static async Task<ExpVariation> CompleteMasterVariationNodeAsync(object resolved)
        {
            var value = resolved is IDataLoaderResult loaderResult
                ? await loaderResult.GetResultAsync()
                : resolved;

            return (ExpVariation)value;
        }

        private static ResolveFieldContext CreateResolveContext(ExpProduct source, IMediator mediator, string[] subFields)
        {
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock.Setup(x => x.GetService(typeof(IMediator))).Returns(mediator);

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

        private static IMediator CreateRecordingMediator(List<IList<string>> sentFieldSets)
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

        private static IMediator CreateRecordingMediator(List<IList<string>> sentFieldSets, Func<string, bool> isActive)
        {
            var mediatorMock = new Mock<IMediator>();

            mediatorMock
                .Setup(x => x.Send(It.IsAny<LoadProductsQuery>(), It.IsAny<CancellationToken>()))
                .Returns((LoadProductsQuery query, CancellationToken _) =>
                {
                    sentFieldSets.Add(query.IncludeFields.ToList());

                    var products = query.ObjectIds
                        .Select(id => new ExpProduct { IndexedProduct = new CatalogProduct { Id = id, IsActive = isActive(id) } })
                        .ToList();

                    return Task.FromResult(new LoadProductResponse(products));
                });

            return mediatorMock.Object;
        }

        private static IMediator CreateRecordingMediator(List<IList<string>> sentFieldSets, HashSet<string> unresolvedIds)
        {
            var mediatorMock = new Mock<IMediator>();

            mediatorMock
                .Setup(x => x.Send(It.IsAny<LoadProductsQuery>(), It.IsAny<CancellationToken>()))
                .Returns((LoadProductsQuery query, CancellationToken _) =>
                {
                    sentFieldSets.Add(query.IncludeFields.ToList());

                    var products = query.ObjectIds
                        .Where(id => !unresolvedIds.Contains(id))
                        .Select(id => new ExpProduct { IndexedProduct = new CatalogProduct { Id = id, IsActive = true } })
                        .ToList();

                    return Task.FromResult(new LoadProductResponse(products));
                });

            return mediatorMock.Object;
        }
    }
}
