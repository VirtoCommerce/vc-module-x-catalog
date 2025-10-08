using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PipelineNet.Middleware;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Extensions;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core;
using VirtoCommerce.XCatalog.Data.Index;

namespace VirtoCommerce.XCatalog.Data.Middlewares
{
    public class EvalSearchRequestUserGroupsMiddleware : IAsyncMiddleware<IndexSearchRequestBuilder>
    {
        private readonly IMemberResolver _memberResolver;
        private readonly IMemberService _memberService;
        private readonly IModuleCatalog _moduleCatalog;

        public EvalSearchRequestUserGroupsMiddleware(IMemberResolver memberResolver, IMemberService memberService, IModuleCatalog moduleCatalog)
        {
            _memberResolver = memberResolver;
            _memberService = memberService;
            _moduleCatalog = moduleCatalog;
        }

        public virtual async Task Run(IndexSearchRequestBuilder parameter, Func<IndexSearchRequestBuilder, Task> next)
        {
            if (parameter != null && _moduleCatalog.IsModuleInstalled("VirtoCommerce.CatalogPersonalization"))
            {
                var userGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "__any" };

                if (!string.IsNullOrEmpty(parameter.UserId) && !ModuleConstants.AnonymousUser.UserName.EqualsIgnoreCase(parameter.UserId))
                {
                    var member = await _memberResolver.ResolveMemberByIdAsync(parameter.UserId);

                    if (member is Contact contact)
                    {
                        GetUserGroupsFromContact(contact, userGroups);
                    }

                    await GetUserGroupsFromOrganization(parameter.OrganizationId, userGroups);
                }

                parameter.AddTermFilter("user_groups", userGroups);
                UpdateAggregations(parameter, userGroups);
            }

            await next(parameter);
        }

        private static void GetUserGroupsFromContact(Contact contact, HashSet<string> userGroups)
        {
            if (contact.Groups.IsNullOrEmpty())
            {
                return;
            }

            userGroups.AddRange(contact.Groups);
        }

        private async Task GetUserGroupsFromOrganization(string organizationId, HashSet<string> userGroups)
        {
            if (organizationId.IsNullOrEmpty())
            {
                return;
            }

            var organizations = await _memberService.GetByIdsAsync([organizationId], nameof(MemberResponseGroup.WithGroups));
            userGroups.AddRange(organizations.OfType<Organization>().SelectMany(x => x.Groups));
        }

        private static void UpdateAggregations(IndexSearchRequestBuilder parameter, HashSet<string> userGroups)
        {
            if (parameter == null)
            {
                return;
            }

            foreach (var aggregation in parameter.Aggregations)
            {
                if (aggregation.Filter is not AndFilter aggregationFilter)
                {
                    continue;
                }

                var userGroupsFilter = aggregationFilter.ChildFilters.OfType<TermFilter>().FirstOrDefault(x => x.FieldName.EqualsIgnoreCase("user_groups"));
                if (userGroupsFilter != null)
                {
                    userGroupsFilter.Values = userGroups.ToList();
                }
                else
                {
                    aggregationFilter.ChildFilters.Add(new TermFilter { FieldName = "user_groups", Values = userGroups.ToList() });
                }
            }
        }
    }
}
