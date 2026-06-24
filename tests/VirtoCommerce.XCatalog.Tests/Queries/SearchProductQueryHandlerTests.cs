using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using VirtoCommerce.CatalogModule.Core.Search.Sorting;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Data.Queries;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Queries
{
    public class SearchProductQueryHandlerTests
    {
        [Fact]
        public void BuildSortDefinitions_ExcludesHidden_AndProjectsDefaultAndSelected()
        {
            var featured = Ordering("featured", "Featured", isVisible: true, isDefault: true);
            var hidden = Ordering("price-ascending", "Price", isVisible: false);
            var nameAsc = Ordering("name-ascending", "A-Z", isVisible: true);

            var result = new TestHandler().Build([featured, hidden, nameAsc], selected: nameAsc, languageCode: "en-US");

            result.Select(x => x.Id).Should().Equal("featured", "name-ascending"); // hidden one is dropped
            result.Single(x => x.Id == "featured").IsDefault.Should().BeTrue();
            result.Single(x => x.Id == "name-ascending").IsSelected.Should().BeTrue();
            result.Single(x => x.Id == "featured").IsSelected.Should().BeFalse();
        }

        [Fact]
        public void BuildSortDefinitions_ResolvesLocalizedName_WithFallbackToBaseName()
        {
            var localized = Ordering("name-ascending", "A-Z", isVisible: true);
            localized.LocalizedNames = new Dictionary<string, string> { ["de-DE"] = "A bis Z" };
            var plain = Ordering("featured", "Featured", isVisible: true);

            var result = new TestHandler().Build([localized, plain], selected: null, languageCode: "de-DE");

            result.Single(x => x.Id == "name-ascending").Name.Should().Be("A bis Z");
            result.Single(x => x.Id == "featured").Name.Should().Be("Featured"); // no de-DE entry -> base name
        }

        [Fact]
        public void BuildSortDefinitions_RawOrUnknownSort_MarksNothingSelected()
        {
            // selected == null models a raw expression / unknown code passthrough.
            var result = new TestHandler().Build([Ordering("featured", "Featured", isVisible: true, isDefault: true)], selected: null, languageCode: "en-US");

            result.Should().OnlyContain(x => !x.IsSelected);
        }

        [Fact]
        public void BuildSortDefinitions_NullOrderings_ReturnsEmpty()
        {
            new TestHandler().Build(null, selected: null, languageCode: "en-US").Should().BeEmpty();
        }

        private static ProductSearchOrdering Ordering(string code, string name, bool isVisible, bool isDefault = false) =>
            new() { Code = code, Name = name, IsVisible = isVisible, IsDefault = isDefault };

        // BuildSortDefinitions does not use any injected dependency, so the base ctor is satisfied with nulls.
        private sealed class TestHandler : SearchProductQueryHandler
        {
            public TestHandler() : base(null, null, null, null, null, null, null, null) { }

            public IList<ProductSortDefinition> Build(IList<ProductSearchOrdering> orderings, ProductSearchOrdering selected, string languageCode) =>
                BuildSortDefinitions(orderings, selected, languageCode);
        }
    }
}
