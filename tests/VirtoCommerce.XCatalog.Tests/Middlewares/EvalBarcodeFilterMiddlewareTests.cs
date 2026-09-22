using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using VirtoCommerce.CatalogModule.Core.Search.Barcodes;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.XCatalog.Core.Extensions;
using VirtoCommerce.XCatalog.Data.Index;
using VirtoCommerce.XCatalog.Data.Middlewares;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Middlewares
{
    public class EvalBarcodeFilterMiddlewareTests
    {
        private const string StoreId = "B2B-store";
        private const string BarcodeValue = "0123456789012";

        [Fact]
        public async Task Run_NoBarcodeTerm_LeavesRequestUntouchedAndDoesNotLoadSettings()
        {
            var serviceMock = CreateService("gtin");
            var builder = CreateBuilder(barcodeValue: null);

            var nextCalled = await RunMiddleware(serviceMock.Object, builder);

            nextCalled.Should().BeTrue();
            GetFieldNames(builder).Should().Equal("is", "status");
            GetTermFilter(builder, "is").Values.Should().Equal("product");
            serviceMock.Verify(x => x.GetSettingsAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Run_NoStoreId_LeavesBarcodeTermUntouchedAndDoesNotLoadSettings()
        {
            var serviceMock = CreateService("gtin");
            var builder = CreateBuilder(storeId: null);

            var nextCalled = await RunMiddleware(serviceMock.Object, builder);

            nextCalled.Should().BeTrue();
            GetTermFilter(builder, "barcode").Values.Should().Equal(BarcodeValue);
            serviceMock.Verify(x => x.GetSettingsAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Run_EmptyBarcodeValue_LeavesRequestUntouched()
        {
            var serviceMock = CreateService("gtin");
            var builder = CreateBuilder(barcodeValue: string.Empty);

            var nextCalled = await RunMiddleware(serviceMock.Object, builder);

            nextCalled.Should().BeTrue();
            GetTermFilter(builder, "barcode").Should().NotBeNull();
            GetTermFilter(builder, "gtin").Should().BeNull();
            GetTermFilter(builder, "is").Values.Should().Equal("product");
            builder.UserFilters.OfType<TermFilter>().Should().ContainSingle(x => x.FieldName == "barcode");
            builder.GeneratedFilters.Should().BeEmpty();
        }

        [Fact]
        public async Task Run_NoConfiguredFields_LeavesBarcodeTermUntouched()
        {
            var serviceMock = CreateService();
            var builder = CreateBuilder();

            var nextCalled = await RunMiddleware(serviceMock.Object, builder);

            nextCalled.Should().BeTrue();
            GetTermFilter(builder, "barcode").Values.Should().Equal(BarcodeValue);
            GetTermFilter(builder, "is").Values.Should().Equal("product");
            builder.UserFilters.OfType<TermFilter>().Should().ContainSingle(x => x.FieldName == "barcode");
            builder.GeneratedFilters.Should().BeEmpty();
        }

        [Fact]
        public async Task Run_SingleConfiguredField_ReplacesBarcodeTermWithFieldTerm()
        {
            var serviceMock = CreateService("gtin");
            var builder = CreateBuilder();

            var nextCalled = await RunMiddleware(serviceMock.Object, builder);

            nextCalled.Should().BeTrue();
            GetTermFilter(builder, "barcode").Should().BeNull();
            GetTermFilter(builder, "gtin").Values.Should().Equal(BarcodeValue);
            GetFilter(builder).ChildFilters.OfType<OrFilter>().Should().BeEmpty();
        }

        [Fact]
        public async Task Run_SeveralConfiguredFields_ReplacesBarcodeTermWithOrFilter()
        {
            var serviceMock = CreateService("gtin", "manufacturerPartNumber");
            var builder = CreateBuilder();

            var nextCalled = await RunMiddleware(serviceMock.Object, builder);

            nextCalled.Should().BeTrue();
            GetTermFilter(builder, "barcode").Should().BeNull();

            var orFilter = GetFilter(builder).ChildFilters.OfType<OrFilter>().Single();
            orFilter.ChildFilters.OfType<TermFilter>().Select(x => x.FieldName).Should().Equal("gtin", "manufacturerPartNumber");
            orFilter.ChildFilters.OfType<TermFilter>().Should().OnlyContain(x => x.Values.Single() == BarcodeValue);
        }

        [Fact]
        public async Task Run_SingleConfiguredField_ReportsExpansionAsGeneratedFilter()
        {
            var serviceMock = CreateService("gtin");
            var builder = CreateBuilder();

            await RunMiddleware(serviceMock.Object, builder);

            builder.UserFilters.Should().BeEmpty();
            builder.GeneratedFilters.Should().ContainSingle()
                .Which.Should().BeOfType<TermFilter>()
                .Which.FieldName.Should().Be("gtin");
            GetFilter(builder).ChildFilters.Should().Contain(builder.GeneratedFilters.Single());
            builder.Filter.Flatten().OfType<TermFilter>().Should().ContainSingle(x => x.FieldName == "gtin");
        }

        [Fact]
        public async Task Run_SeveralConfiguredFields_ReportsEachFieldAsGeneratedFilter()
        {
            var serviceMock = CreateService("gtin", "manufacturerPartNumber");
            var builder = CreateBuilder();

            await RunMiddleware(serviceMock.Object, builder);

            builder.UserFilters.Should().BeEmpty();
            builder.GeneratedFilters.OfType<TermFilter>().Select(x => x.FieldName).Should().Equal("gtin", "manufacturerPartNumber");

            // The same term filter instances the OrFilter of the request carries.
            GetFilter(builder).ChildFilters.OfType<OrFilter>().Single().ChildFilters.Should().Equal(builder.GeneratedFilters);
            builder.Filter.Flatten().OfType<TermFilter>().Should().ContainSingle(x => x.FieldName == "gtin");
            builder.Filter.Flatten().OfType<TermFilter>().Should().ContainSingle(x => x.FieldName == "manufacturerPartNumber");
        }

        [Fact]
        public async Task Run_BarcodeTermExpanded_IncludesVariations()
        {
            var serviceMock = CreateService("gtin");
            var builder = CreateBuilder();

            await RunMiddleware(serviceMock.Object, builder);

            GetTermFilter(builder, "is").Values.Should().Equal("product", "variation");
        }

        [Fact]
        public async Task Run_UserSuppliedProductScope_IsNotWidened()
        {
            var serviceMock = CreateService("gtin");
            var builder = CreateBuilder(documentTypeFromUser: true);

            await RunMiddleware(serviceMock.Object, builder);

            GetTermFilter(builder, "gtin").Values.Should().Equal(BarcodeValue);
            GetTermFilter(builder, "is").Values.Should().Equal("product");
            builder.UserFilters.OfType<TermFilter>().Should().ContainSingle(x => x.FieldName == "is");
            builder.UserFilters.OfType<TermFilter>().Should().NotContain(x => x.FieldName == "barcode");
        }

        [Fact]
        public async Task Run_ExplicitDocumentTypeScope_IsNotWidened()
        {
            var serviceMock = CreateService("gtin");
            var builder = CreateBuilder(documentTypes: ["variation"]);

            await RunMiddleware(serviceMock.Object, builder);

            GetTermFilter(builder, "is").Values.Should().Equal("variation");
        }

        [Fact]
        public async Task Run_SeveralConfiguredFields_PatchesAggregationFilters()
        {
            var serviceMock = CreateService("gtin", "manufacturerPartNumber");
            var builder = CreateBuilder();
            var aggregationFilter = CloneRequestFilter(builder);
            builder.Aggregations.Add(new TermAggregationRequest { FieldName = "color", Filter = aggregationFilter });

            await RunMiddleware(serviceMock.Object, builder);

            aggregationFilter.ChildFilters.OfType<TermFilter>().Should().NotContain(x => x.FieldName == "barcode");
            aggregationFilter.ChildFilters.OfType<OrFilter>().Single()
                .ChildFilters.OfType<TermFilter>().Select(x => x.FieldName).Should().Equal("gtin", "manufacturerPartNumber");
            aggregationFilter.ChildFilters.OfType<TermFilter>().Single(x => x.FieldName == "is").Values.Should().Equal("product", "variation");
        }

        [Fact]
        public async Task Run_NestedAggregationFilter_IsPatched()
        {
            var serviceMock = CreateService("gtin");
            var builder = CreateBuilder();
            var nestedFilter = CloneRequestFilter(builder);
            var aggregationFilter = new AndFilter
            {
                ChildFilters = [new TermFilter { FieldName = "color", Values = ["red"] }, nestedFilter],
            };
            builder.Aggregations.Add(new TermAggregationRequest { FieldName = "color", Filter = aggregationFilter });

            await RunMiddleware(serviceMock.Object, builder);

            nestedFilter.ChildFilters.OfType<TermFilter>().Should().NotContain(x => x.FieldName == "barcode");
            nestedFilter.ChildFilters.OfType<TermFilter>().Single(x => x.FieldName == "gtin").Values.Should().Equal(BarcodeValue);
            nestedFilter.ChildFilters.OfType<TermFilter>().Single(x => x.FieldName == "is").Values.Should().Equal("product", "variation");
        }

        private static Mock<IBarcodeSearchConfigurationService> CreateService(params string[] fields)
        {
            var serviceMock = new Mock<IBarcodeSearchConfigurationService>();
            serviceMock
                .Setup(x => x.GetSettingsAsync(StoreId))
                .ReturnsAsync(new BarcodeSearchSettings { ScannerEnabled = true, Fields = fields });

            return serviceMock;
        }

        private static IndexSearchRequestBuilder CreateBuilder(
            string storeId = StoreId,
            string barcodeValue = BarcodeValue,
            IList<string> documentTypes = null,
            bool documentTypeFromUser = false)
        {
            var builder = new IndexSearchRequestBuilder().WithStoreId(storeId);

            // Filters the caller sent in the filter phrase, which ParseFilters also tracks in UserFilters.
            var userFilters = new List<IFilter>();

            if (barcodeValue != null)
            {
                userFilters.Add(new TermFilter { FieldName = "barcode", Values = [barcodeValue] });
            }

            if (documentTypeFromUser)
            {
                userFilters.Add(new TermFilter { FieldName = "is", Values = documentTypes ?? ["product"] });
            }

            if (userFilters.Count > 0)
            {
                ParseUserFilters(builder, userFilters);
            }

            // The terms SearchProductQueryHandler.AddDefaultTerms adds after the user filters.
            builder.AddTermFilter("is", documentTypes ?? ["product"], skipIfExists: true);
            builder.AddTermFilter("status", "visible", skipIfExists: true);

            return builder;
        }

        private static void ParseUserFilters(IndexSearchRequestBuilder builder, IList<IFilter> filters)
        {
            var phraseParserMock = new Mock<ISearchPhraseParser>();
            phraseParserMock
                .Setup(x => x.Parse(It.IsAny<string>()))
                .Returns(new SearchPhraseParseResult { Filters = filters });

            builder.ParseFilters(phraseParserMock.Object, "filter");
        }

        // The per-aggregation copy of the request filter that ApplyMultiSelectFacetSearch makes before the pipeline runs.
        private static AndFilter CloneRequestFilter(IndexSearchRequestBuilder builder)
        {
            return GetFilter(builder).CloneTyped();
        }

        private static async Task<bool> RunMiddleware(IBarcodeSearchConfigurationService barcodeSearchConfigurationService, IndexSearchRequestBuilder builder)
        {
            var nextCalled = false;
            var middleware = new EvalBarcodeFilterMiddleware(barcodeSearchConfigurationService);

            await middleware.Run(builder, _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });

            return nextCalled;
        }

        private static AndFilter GetFilter(IndexSearchRequestBuilder builder)
        {
            return (AndFilter)builder.Filter;
        }

        private static IList<string> GetFieldNames(IndexSearchRequestBuilder builder)
        {
            return GetFilter(builder).ChildFilters.OfType<TermFilter>().Select(x => x.FieldName).ToList();
        }

        private static TermFilter GetTermFilter(IndexSearchRequestBuilder builder, string fieldName)
        {
            return GetFilter(builder).ChildFilters.OfType<TermFilter>().FirstOrDefault(x => x.FieldName == fieldName);
        }
    }
}
