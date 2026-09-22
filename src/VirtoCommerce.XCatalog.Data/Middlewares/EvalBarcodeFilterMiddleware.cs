using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.CatalogModule.Core.Search.Barcodes;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Extensions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.XCatalog.Data.Index;

namespace VirtoCommerce.XCatalog.Data.Middlewares
{
    // Expands the virtual "barcode" filter term (sent by the storefront for a scanned code) into exact term filters
    // over the barcode fields the store has configured in Catalog.Search.BarcodeSearchFields, OR-ed when there are
    // several. With no configured field the term is left untouched and the scanned value keeps its full-text behavior.
    public class EvalBarcodeFilterMiddleware : IAsyncMiddleware<IndexSearchRequestBuilder>
    {
        private const string BarcodeFieldName = "barcode";
        private const string DocumentTypeFieldName = "is";
        private const string ProductDocumentType = "product";
        private const string VariationDocumentType = "variation";

        private readonly IBarcodeSearchConfigurationService _barcodeSearchConfigurationService;

        public EvalBarcodeFilterMiddleware(IBarcodeSearchConfigurationService barcodeSearchConfigurationService)
        {
            _barcodeSearchConfigurationService = barcodeSearchConfigurationService;
        }

        public virtual async Task Run(IndexSearchRequestBuilder parameter, Func<IndexSearchRequestBuilder, Task> next)
        {
            if (!string.IsNullOrEmpty(parameter.StoreId) &&
                parameter.Filter is AndFilter andFilter && !andFilter.ChildFilters.IsNullOrEmpty())
            {
                var barcodeFilter = FindBarcodeFilter(andFilter);
                var values = barcodeFilter?.Values?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

                if (values?.Count > 0)
                {
                    var settings = await _barcodeSearchConfigurationService.GetSettingsAsync(parameter.StoreId);

                    if (!settings.Fields.IsNullOrEmpty())
                    {
                        ExpandBarcodeFilter(parameter, andFilter, barcodeFilter, settings.Fields, values);
                    }
                }
            }

            await next(parameter);
        }

        protected virtual void ExpandBarcodeFilter(IndexSearchRequestBuilder parameter, AndFilter andFilter, TermFilter barcodeFilter, IList<string> fields, IList<string> values)
        {
            // The configured names are index field names, so they are used verbatim.
            var fieldFilters = fields.Select(x => new TermFilter { FieldName = x, Values = values.ToList() }).ToList();
            var expansion = fieldFilters.Or();

            var includeVariations = HasDefaultProductScope(andFilter, parameter.UserFilters);

            andFilter.ChildFilters.Remove(barcodeFilter);
            andFilter.ChildFilters.Add(expansion);

            if (includeVariations)
            {
                IncludeVariations(andFilter);
            }

            // The response reports the request's own filters: the virtual term is no longer one of them and its
            // expansion was not written by the caller, so it moves from the user list to the generated one (the
            // storefront renders user filters as removable chips).
            parameter.UserFilters.Remove(barcodeFilter);
            parameter.GeneratedFilters.AddRange(fieldFilters);

            UpdateAggregations(parameter, expansion, includeVariations);
        }

        // Only the first barcode term is expanded, like the isPurchased precedent; a second one stays a literal field.
        private static TermFilter FindBarcodeFilter(AndFilter filter)
        {
            return filter.ChildFilters.OfType<TermFilter>().FirstOrDefault(x => x.FieldName.EqualsIgnoreCase(BarcodeFieldName));
        }

        private static TermFilter FindDocumentTypeFilter(AndFilter filter)
        {
            return filter.ChildFilters.OfType<TermFilter>().FirstOrDefault(x => x.FieldName.EqualsIgnoreCase(DocumentTypeFieldName));
        }

        // The scope is the default "is:product" the query handler adds only when the caller did not send one of
        // their own; an explicitly requested scope is theirs to keep.
        protected virtual bool HasDefaultProductScope(AndFilter filter, IList<IFilter> userFilters)
        {
            var documentTypeFilter = FindDocumentTypeFilter(filter);

            return HasProductScope(documentTypeFilter) && !userFilters.Contains(documentTypeFilter);
        }

        private static bool HasProductScope(TermFilter documentTypeFilter)
        {
            return documentTypeFilter?.Values?.Count == 1 && documentTypeFilter.Values[0].EqualsIgnoreCase(ProductDocumentType);
        }

        // A barcode may be stored on a variation, so the default scope is widened to variations as well.
        protected virtual void IncludeVariations(AndFilter filter)
        {
            var documentTypeFilter = FindDocumentTypeFilter(filter);

            if (HasProductScope(documentTypeFilter))
            {
                documentTypeFilter.Values = [ProductDocumentType, VariationDocumentType];
            }
        }

        private void UpdateAggregations(IndexSearchRequestBuilder parameter, IFilter expansion, bool includeVariations)
        {
            foreach (var aggregation in parameter.Aggregations)
            {
                if (aggregation.Filter is AndFilter aggregationFilter)
                {
                    ExpandBarcodeFilter(aggregationFilter, expansion, includeVariations);
                }
            }
        }

        // The per-aggregation copies of the request filter, made by ApplyMultiSelectFacetSearch before this pipeline
        // runs, carry the barcode term too - either as a direct child or nested in the copied AndFilter. Each copy
        // that carries it gets its own copy of the expansion.
        private void ExpandBarcodeFilter(AndFilter filter, IFilter expansion, bool includeVariations)
        {
            var barcodeFilter = FindBarcodeFilter(filter);
            if (barcodeFilter != null)
            {
                filter.ChildFilters.Remove(barcodeFilter);
                filter.ChildFilters.Add(expansion.CloneTyped());
            }

            if (includeVariations)
            {
                IncludeVariations(filter);
            }

            foreach (var childFilter in filter.ChildFilters.OfType<AndFilter>())
            {
                ExpandBarcodeFilter(childFilter, expansion, includeVariations);
            }
        }
    }
}
