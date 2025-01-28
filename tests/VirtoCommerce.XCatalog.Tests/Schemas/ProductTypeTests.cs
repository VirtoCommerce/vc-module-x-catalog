using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using GraphQL;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.XCatalog.Core.Models;
using VirtoCommerce.XCatalog.Core.Schemas;
using VirtoCommerce.XCatalog.Tests.Helpers;
using Xunit;

namespace VirtoCommerce.XCatalog.Tests.Schemas
{
    public class ProductTypeTests : XCatalogMoqHelper
    {
        private readonly ProductType _productType;

        public ProductTypeTests()
        {
            _productType = new ProductType(_mediatorMock.Object, _dataLoaderContextAccessorMock.Object);
        }

        #region Properties

        [Fact]
        public async Task ProductType_Properties_ShouldResolve()
        {
            // Arrange
            var propValues = _fixture
                .Build<PropertyValue>()
                .With(x => x.LanguageCode, CULTURE_NAME)
                .With(x => x.Property, default(Property))
                .CreateMany()
                .ToList();

            var product = new ExpProduct
            {
                IndexedProduct = new CatalogProduct
                {
                    Properties = new List<Property>
                    {
                        new Property
                        {
                            Values = propValues
                        }
                    }
                }
            };
            var resolveContext = new ResolveFieldContext()
            {
                Source = product,
                UserContext = new Dictionary<string, object>
                {
                    { "cultureName", CULTURE_NAME }
                }
            };

            // Act
            var result = await _productType.Fields.FirstOrDefault(x => x.Name.EqualsInvariant("properties")).Resolver.ResolveAsync(resolveContext);

            // Assert
            result.Should().BeOfType<List<Property>>();
            ((List<Property>)result).Count.Should().Be(propValues.Count);
        }

        [Fact]
        public async Task ProductType_Properties_ShouldReturnPropertyWithCurrentCultureName()
        {
            // Arrange
            var propValues = _fixture
                .Build<PropertyValue>()
                .With(x => x.LanguageCode, CULTURE_NAME)
                .With(x => x.Property, default(Property))
                .With(x => x.Alias, "i_grouped")
                .CreateMany(2)
                .ToList();

            propValues.First().LanguageCode = "de-De";

            var product = new ExpProduct
            {
                IndexedProduct = new CatalogProduct
                {
                    Properties = new List<Property>
                    {
                        new Property
                        {
                            Values = propValues
                        }
                    }
                }
            };
            var resolveContext = new ResolveFieldContext()
            {
                Source = product,
                UserContext = new Dictionary<string, object>
                {
                    { "cultureName", CULTURE_NAME }
                }
            };

            // Act
            var result = await _productType.Fields.FirstOrDefault(x => x.Name.EqualsInvariant("properties")).Resolver.ResolveAsync(resolveContext);

            // Assert
            result.Should().BeOfType<List<Property>>();
            ((List<Property>)result).Count.Should().Be(1);
        }

        [Fact]
        public async Task ProductType_Properties_SelectedLanguageNotFound_ShouldReturnEmptyList()
        {
            // Arrange
            var propValue = _fixture
                .Build<PropertyValue>()
                .With(x => x.LanguageCode, "de-De")
                .With(x => x.Property, default(Property))
                .Create();

            var product = new ExpProduct
            {
                IndexedProduct = new CatalogProduct
                {
                    Properties = new List<Property>
                    {
                        new Property
                        {
                            Values = new List<PropertyValue>
                            {
                                propValue
                            }
                        }
                    }
                }
            };
            var resolveContext = new ResolveFieldContext()
            {
                Source = product,
                UserContext = new Dictionary<string, object>
                {
                    { "cultureName", CULTURE_NAME }
                }
            };

            // Act
            var result = await _productType.Fields.FirstOrDefault(x => x.Name.EqualsInvariant("properties")).Resolver.ResolveAsync(resolveContext);

            // Assert
            result.Should().BeOfType<List<Property>>();
            ((List<Property>)result).Count.Should().Be(1);
            ((List<Property>)result).First().Values.Should().BeEmpty();
        }

        [Fact]
        public async Task ProductType_Properties_NoLocalization_ShouldGetDefaultValueForDictionary()
        {
            // Arrange
            var alias = "i_grouped";
            var propValues = _fixture
                .Build<PropertyValue>()
                .With(x => x.LanguageCode, "de-De")
                .With(x => x.Property, default(Property))
                .With(x => x.Alias, alias)
                .CreateMany()
                .ToList();

            var product = new ExpProduct
            {
                IndexedProduct = new CatalogProduct
                {
                    Properties = new List<Property>
                    {
                        new Property
                        {
                            Values = propValues,
                            Dictionary = true
                        }
                    }
                }
            };
            var resolveContext = new ResolveFieldContext()
            {
                Source = product,
                UserContext = new Dictionary<string, object>
                {
                    { "cultureName", CULTURE_NAME }
                }
            };

            // Act
            var result = await _productType.Fields.FirstOrDefault(x => x.Name.EqualsInvariant("properties")).Resolver.ResolveAsync(resolveContext);

            // Assert
            result.Should().BeOfType<List<Property>>();
            ((List<Property>)result).Count.Should().Be(1);
            ((List<Property>)result).Any(p => p.Values.Any(pv => pv.Value.ToString().EqualsInvariant(alias))).Should().BeTrue();
        }

        #endregion Properties
    }
}
