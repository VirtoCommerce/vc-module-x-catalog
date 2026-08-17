using System;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.XCatalog.Data.Services
{
    public class PropertySearchCriteriaBuilder
    {
        private readonly ISearchPhraseParser _phraseParser;
        private readonly PropertySearchCriteria _searchCriteria;


        public PropertySearchCriteriaBuilder(ISearchPhraseParser phraseParser) : this()
        {
            _phraseParser = phraseParser;
        }

        public PropertySearchCriteriaBuilder()
        {
            _searchCriteria = AbstractTypeFactory<PropertySearchCriteria>.TryCreateInstance();
        }

        public virtual PropertySearchCriteria Build()
        {
            return _searchCriteria.Clone() as PropertySearchCriteria;
        }

        public PropertySearchCriteriaBuilder ParseFilters(string filterPhrase)
        {
            if (filterPhrase == null)
            {
                return this;
            }
            if (_phraseParser == null)
            {
                throw new OperationCanceledException("phrase parser must be set");
            }

            var parseResult = _phraseParser.Parse(filterPhrase);
            parseResult.Filters.MapTo(_searchCriteria);

            return this;
        }

        public PropertySearchCriteriaBuilder WithPaging(int skip, int take)
        {
            _searchCriteria.Skip = skip;
            _searchCriteria.Take = take;
            return this;
        }

        public PropertySearchCriteriaBuilder WithCatalogId(string catalogId)
        {
            _searchCriteria.CatalogId = catalogId;
            return this;
        }
    }
}
