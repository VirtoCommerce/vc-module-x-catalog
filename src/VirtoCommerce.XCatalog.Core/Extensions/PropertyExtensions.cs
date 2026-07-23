using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.XCatalog.Core.Extensions
{
    public static class PropertyExtensions
    {
        /// <summary>
        /// Flattens the tree-like structure of Property-PropertyValues into flat list of Properties,
        /// with each Property having a single PropertyValue in its Values collection
        /// </summary>
        public static IList<Property> ExpandByValues(this IEnumerable<Property> properties, string cultureName)
        {
            return properties
                .Where(x => !x.Hidden)
                .SelectMany(property =>
                {
                    var propertyValues = property.Dictionary
                        // Group by Alias for dictionary properties
                        ? property.Values
                            .GroupBy(propertyValue => propertyValue.Alias)
                            .Select(aliasGroup
                                => aliasGroup.FirstOrDefault(propertyValue => propertyValue.LanguageCode.EqualsIgnoreCase(cultureName))
                                // If localization not found build default value
                                ?? aliasGroup.Select(propertyValue =>
                                {
                                    var clonedValue = propertyValue.CloneTyped();
                                    clonedValue.Value = aliasGroup.Key;
                                    return clonedValue;
                                }).First()
                            )
                        : property.Values.Where(x => x.LanguageCode.EqualsIgnoreCase(cultureName) || x.LanguageCode.IsNullOrEmpty());

                    // Wrap each PropertyValue into a Property. Clone a values-free template once
                    // per property instead of full-cloning per value: Property.Clone() deep-clones
                    // ALL values, and the per-value copy immediately discards them — O(values²)
                    // clone waste on the hot resolver path.
                    var template = property.CopyPropertyWithoutValues();

                    return propertyValues
                        .Select(propertyValue => template.CopyWithValue(propertyValue))
                        .DefaultIfEmpty(template);
                })
                .ToList();
        }

        /// <summary>
        /// Sorts properties by Priority attribute, then flattens the key-value tree
        /// </summary>
        public static IList<Property> ExpandOrderedByValues(this IEnumerable<Property> properties, string cultureName)
        {
            if (properties.IsNullOrEmpty())
            {
                return new List<Property>();
            }

            properties = properties
                .OrderByDescending(x => x.IsManageable)
                .ThenBy(x => x.DisplayOrder ?? int.MaxValue)
                .ThenBy(x => x.Name);

            return properties.ExpandByValues(cultureName);
        }

        /// <summary>
        /// Filters and sorts properties by KeyProperty attribute, then flattens the key-value tree
        /// </summary>
        public static IList<Property> ExpandKeyPropertiesByValues(this IEnumerable<Property> properties, string cultureName, int take = 0)
        {
            if (properties.IsNullOrEmpty())
            {
                return new List<Property>();
            }

            properties = properties
                .Where(x => !x.Attributes.IsNullOrEmpty() && x.Attributes.Any(a => a.Name.EqualsIgnoreCase(ModuleConstants.KeyProperty)))
                .OrderBy(x =>
                {
                    var keyPropertyAttr = x.Attributes?.FirstOrDefault(x => x.Name.EqualsIgnoreCase(ModuleConstants.KeyProperty));
                    return keyPropertyAttr?.Value.TryParse(int.MaxValue);
                });

            if (take > 0)
            {
                properties = properties.Take(take);
            }

            return properties.ExpandByValues(cultureName);
        }

        /// <summary>
        /// Names of properties indexed with a per-culture field variant (see CatalogDocumentBuilder.IndexCustomProperty).
        /// </summary>
        public static IEnumerable<string> GetMultilanguagePropertyNames(this IEnumerable<Property> properties)
        {
            return properties.Where(x => x.Multilanguage).Select(x => x.Name);
        }

        public static Property CopyPropertyWithValue(this PropertyValue propertyValue, Property property)
        {
            return property.CopyPropertyWithoutValues().CopyWithValue(propertyValue);
        }

        // The template's Values collection is empty, so cloning it skips the PropertyValue
        // deep-clone entirely; the original value reference is attached, matching the
        // pre-existing aliasing semantics of CopyPropertyWithValue.
        private static Property CopyWithValue(this Property template, PropertyValue propertyValue)
        {
            var clonedProperty = template.CloneTyped();
            clonedProperty.Values = new List<PropertyValue> { propertyValue };
            return clonedProperty;
        }

        private static Property CopyPropertyWithoutValues(this Property property)
        {
            var clonedProperty = property.CloneTyped();
            clonedProperty.Values = Array.Empty<PropertyValue>();
            return clonedProperty;
        }
    }
}
