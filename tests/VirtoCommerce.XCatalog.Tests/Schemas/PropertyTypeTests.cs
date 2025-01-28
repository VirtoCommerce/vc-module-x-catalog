using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using GraphQL;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.XCatalog.Tests.Helpers;
using Xunit;
using PropertyType = VirtoCommerce.XCatalog.Core.Schemas.PropertyType;

namespace VirtoCommerce.XCatalog.Tests.Schemas
{
    public class PropertyTypeTests : XCatalogMoqHelper
    {
        private readonly PropertyType _propertyType = new(null, null);

        [Fact]
        public async Task PropertyType_Properties_ShouldFilterPropertiesByCultureName()
        {
            // Arrange
            var label = _fixture.Create<string>();

            var product = new Property
            {
                Name = _fixture.Create<string>(),
                DisplayNames = new List<PropertyDisplayName>
                {
                    new PropertyDisplayName
                    {
                        LanguageCode = CULTURE_NAME,
                        Name = label
                    },
                    new PropertyDisplayName
                    {
                        LanguageCode = "de-De",
                        Name = _fixture.Create<string>()
                    },
                }
            };

            var resolveContext = new ResolveFieldContext
            {
                Source = product,
                UserContext = new Dictionary<string, object>
                {
                    { "cultureName", CULTURE_NAME }
                }
            };

            // Act
            var result = await _propertyType.Fields.FirstOrDefault(x => x.Name.EqualsInvariant("label")).Resolver.ResolveAsync(resolveContext);

            // Assert
            result.Should().BeOfType<string>();
            ((string)result).Should().Be(label);
        }

        [Fact]
        public async Task PropertyType_Properties_CultureNameNotPassed_ShouldReturnSourceName()
        {
            // Arrange
            var label = _fixture.Create<string>();

            var product = new Property
            {
                Name = label,
                DisplayNames = new List<PropertyDisplayName>
                {
                    new PropertyDisplayName
                    {
                        LanguageCode = CULTURE_NAME,
                        Name = _fixture.Create<string>()
                    },
                }
            };

            var resolveContext = new ResolveFieldContext
            {
                Source = product,
                UserContext = new Dictionary<string, object>()
            };

            // Act
            var result = await _propertyType.Fields.FirstOrDefault(x => x.Name.EqualsInvariant("label")).Resolver.ResolveAsync(resolveContext);

            // Assert
            result.Should().BeOfType<string>();
            ((string)result).Should().Be(label);
        }
    }
}
