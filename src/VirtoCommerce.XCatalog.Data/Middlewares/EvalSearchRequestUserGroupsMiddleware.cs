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
using VirtoCommerce.Xapi.Core;
using VirtoCommerce.XCatalog.Data.Index;

namespace VirtoCommerce.XCatalog.Data.Middlewares
{
    public class EvalSearchRequestUserGroupsMiddleware : IAsyncMiddleware<IndexSearchRequestBuilder>
    {
        protected readonly IMemberResolver _memberResolver;
        protected readonly IMemberService _memberService;
        protected readonly IModuleCatalog _moduleCatalog;

        public EvalSearchRequestUserGroupsMiddleware(IMemberResolver memberResolver, IMemberService memberService, IModuleCatalog moduleCatalog)
        {
            _memberResolver = memberResolver;
            _memberService = memberService;
            _moduleCatalog = moduleCatalog;
        }

        public virtual async Task Run(IndexSearchRequestBuilder parameter, Func<IndexSearchRequestBuilder, Task> next)
        {
            if (_moduleCatalog.IsModuleInstalled("VirtoCommerce.CatalogPersonalization"))
            {
                var userGroups = new List<string> { "__any" };

                if (!string.IsNullOrEmpty(parameter?.UserId) && !ModuleConstants.AnonymousUser.UserName.EqualsIgnoreCase(parameter.UserId))
                {
                    var member = await _memberResolver.ResolveMemberByIdAsync(parameter.UserId);

                    if (member is Contact contact)
                    {
                        userGroups.AddRange(GetUserGroupsFromContact(contact));
                    }

                    userGroups.AddRange(await GetUserGroupsFromOrganizartion(parameter.OrganizationId));
                }

                var userGroupsValue = string.Join(',', userGroups);
                parameter?.AddTerms([$"user_groups:{userGroupsValue}"]);
            }

            await next(parameter);
        }

        private static IList<string> GetUserGroupsFromContact(Contact contact)
        {
            var userGroups = new List<string>();

            if (!contact.Groups.IsNullOrEmpty())
            {
                userGroups.AddRange(contact.Groups);
            }

            return userGroups;
        }

        private async Task<IList<string>> GetUserGroupsFromOrganizartion(string organizationId)
        {
            var userGroups = new List<string>();

            if (!organizationId.IsNullOrEmpty())
            {
                var organizations = await _memberService.GetByIdsAsync([organizationId], MemberResponseGroup.WithGroups.ToString());
                userGroups.AddRange(organizations.OfType<Organization>().SelectMany(x => x.Groups));
            }

            return userGroups;
        }
    }
}
