using System.Collections.Generic;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Binding;
using VirtoCommerce.XCatalog.Core.Binding;

namespace VirtoCommerce.XCatalog.Core.Models
{
    public class ExpCategory : IHasRelevanceScore
    {
        public string Id => Category?.Id;

        [BindIndexField(FieldName = "__object", BinderType = typeof(CategoryBinder))]
        public virtual Category Category { get; set; }

        [BindIndexField(BinderType = typeof(KeyBinder))]
        public virtual string Key { get; set; }

        [BindIndexField(FieldName = SearchModule.Core.ModuleConstants.RelevanceScore, BinderType = typeof(DefaultPropertyIndexBinder))]
        public double? RelevanceScore { get; set; }

        //Level in hierarchy
        public int Level => Category?.Outline?.Split("/").Length ?? 0;

        public IList<ExpCategory> ChildCategories { get; set; }
    }
}
