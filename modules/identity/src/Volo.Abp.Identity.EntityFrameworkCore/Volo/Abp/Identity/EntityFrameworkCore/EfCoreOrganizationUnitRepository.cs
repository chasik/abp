﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Dynamic.Core;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Volo.Abp.Identity.EntityFrameworkCore
{
    public class EfCoreOrganizationUnitRepository
        : EfCoreRepository<IIdentityDbContext, OrganizationUnit, Guid>,
            IOrganizationUnitRepository
    {
        public EfCoreOrganizationUnitRepository(
            IDbContextProvider<IIdentityDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public virtual async Task<List<OrganizationUnit>> GetChildrenAsync(
            Guid? parentId,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            return await DbSet
                .IncludeDetails(includeDetails)
                .Where(x => x.ParentId == parentId)
                .ToListAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<List<OrganizationUnit>> GetAllChildrenWithParentCodeAsync(
            string code,
            Guid? parentId,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            return await DbSet
                .IncludeDetails(includeDetails)
                .Where(ou => ou.Code.StartsWith(code) && ou.Id != parentId.Value)
                .ToListAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<List<OrganizationUnit>> GetListAsync(
            Guid? parentId,
            string sorting = null,
            int maxResultCount = int.MaxValue,
            int skipCount = 0,
            string filter = null,
            bool includeDetails = true,
            CancellationToken cancellationToken = default)
        {
            return await DbSet
                .IncludeDetails(includeDetails)
                .Where(ou=>ou.ParentId==parentId)
                .WhereIf(!filter.IsNullOrWhiteSpace(),
                    ou => ou.DisplayName.Contains(filter) ||
                          ou.Code.Contains(filter))
                .OrderBy(sorting ?? nameof(OrganizationUnit.DisplayName))
                .PageBy(skipCount, maxResultCount)
                .ToListAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<List<OrganizationUnit>> GetListAsync(
            IEnumerable<Guid> ids,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            return await DbSet
                .IncludeDetails(includeDetails)
                .Where(t => ids.Contains(t.Id))
                .ToListAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<OrganizationUnit> GetAsync(
            string displayName,
            bool includeDetails = true,
            CancellationToken cancellationToken = default)
        {
            return await DbSet
                .IncludeDetails(includeDetails)
                .FirstOrDefaultAsync(
                    ou => ou.DisplayName == displayName,
                    GetCancellationToken(cancellationToken)
                );
        }

        public virtual async Task<List<IdentityRole>> GetRolesAsync(
            OrganizationUnit organizationUnit,
            string sorting = null,
            int maxResultCount = int.MaxValue,
            int skipCount = 0,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            var query = from organizationRole in DbContext.Set<OrganizationUnitRole>()
                join role in DbContext.Roles.IncludeDetails(includeDetails) on organizationRole.RoleId equals role.Id
                where organizationRole.OrganizationUnitId == organizationUnit.Id
                select role;
            query = query
                .OrderBy(sorting ?? nameof(IdentityRole.Name))
                .PageBy(skipCount, maxResultCount);

            return await query.ToListAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<int> GetRolesCountAsync(
            OrganizationUnit organizationUnit,
            CancellationToken cancellationToken = default)
        {
            var query = from organizationRole in DbContext.Set<OrganizationUnitRole>()
                join role in DbContext.Roles on organizationRole.RoleId equals role.Id
                where organizationRole.OrganizationUnitId == organizationUnit.Id
                select role;

            return await query.CountAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<List<IdentityUser>> GetMembersAsync(
            OrganizationUnit organizationUnit,
            string sorting = null,
            int maxResultCount = int.MaxValue,
            int skipCount = 0,
            string filter = null,
            bool includeDetails = false,
            CancellationToken cancellationToken = default
        )
        {
            var query = CreateGetMembersFilteredQuery(organizationUnit, filter);

            return await query.IncludeDetails(includeDetails).OrderBy(sorting ?? nameof(IdentityUser.UserName))
                .PageBy(skipCount, maxResultCount)
                .ToListAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<int> GetMembersCountAsync(
            OrganizationUnit organizationUnit,
            string filter = null,
            CancellationToken cancellationToken = default)
        {
            var query = CreateGetMembersFilteredQuery(organizationUnit, filter);

            return await query.CountAsync(GetCancellationToken(cancellationToken));
        }

        public override IQueryable<OrganizationUnit> WithDetails()
        {
            return GetQueryable().IncludeDetails();
        }

        public virtual Task RemoveAllRolesAsync(
            OrganizationUnit organizationUnit,
            CancellationToken cancellationToken = default)
        {
            organizationUnit.Roles.Clear();
            return Task.CompletedTask;
        }

        public virtual async Task RemoveAllMembersAsync(
            OrganizationUnit organizationUnit,
            CancellationToken cancellationToken = default)
        {
            var ouMembersQuery = await DbContext.Set<IdentityUserOrganizationUnit>()
                .Where(q => q.OrganizationUnitId == organizationUnit.Id)
                .ToListAsync(GetCancellationToken(cancellationToken));

            DbContext.Set<IdentityUserOrganizationUnit>().RemoveRange(ouMembersQuery);
        }

        public virtual async Task<long> GetLongCountAsync(Guid? parentId, string filter = null,
            CancellationToken cancellationToken = default)
        {
            return await DbSet
                .Where(ou=>ou.ParentId==parentId)
                .WhereIf(!filter.IsNullOrWhiteSpace(), ou =>
                    ou.DisplayName.Contains(filter) ||
                    ou.Code.Contains(filter))
                .LongCountAsync(GetCancellationToken(cancellationToken));
        }

        protected virtual IQueryable<IdentityUser> CreateGetMembersFilteredQuery(OrganizationUnit organizationUnit,
            string filter = null)
        {
            var query = from userOu in DbContext.Set<IdentityUserOrganizationUnit>()
                join user in DbContext.Users on userOu.UserId equals user.Id
                where userOu.OrganizationUnitId == organizationUnit.Id
                select user;

            if (!filter.IsNullOrWhiteSpace())
            {
                query = query.Where(u =>
                    u.UserName.Contains(filter) ||
                    u.Email.Contains(filter) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(filter))
                );
            }

            return query;
        }
    }
}