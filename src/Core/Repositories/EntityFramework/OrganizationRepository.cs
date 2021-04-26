﻿using System;
using System.Threading.Tasks;
using TableModel = Bit.Core.Models.Table;
using DataModel = Bit.Core.Models.Data;
using EFModel = Bit.Core.Models.EntityFramework;
using System.Linq;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Bit.Core.Models.Table;
using System.Text.Json;

namespace Bit.Core.Repositories.EntityFramework
{
    public class OrganizationRepository : Repository<TableModel.Organization, EFModel.Organization, Guid>, IOrganizationRepository
    {
        public OrganizationRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Organizations)
        { }

        public async Task<Organization> GetByIdentifierAsync(string identifier)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var organization = await GetDbSet(dbContext).Where(e => e.Identifier == identifier)
                    .FirstOrDefaultAsync();
                return organization;
            }
        }

        public async Task<ICollection<TableModel.Organization>> GetManyByEnabledAsync()
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var organizations = await GetDbSet(dbContext).Where(e => e.Enabled).ToListAsync();
                return Mapper.Map<List<TableModel.Organization>>(organizations);
            }
        }

        public async Task<ICollection<TableModel.Organization>> GetManyByUserIdAsync(Guid userId)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var organizations = await GetDbSet(dbContext)
                    .Select(e => e.OrganizationUsers
                        .Where(ou => ou.UserId == userId)
                        .Select(ou => ou.Organization))
                    .ToListAsync();
                return Mapper.Map<List<TableModel.Organization>>(organizations);
            }
        }

        public async Task<ICollection<TableModel.Organization>> SearchAsync(string name, string userEmail,
            bool? paid, int skip, int take)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var organizations = await GetDbSet(dbContext)
                    .Where(e => name == null || e.Name.Contains(name))
                    .Where(e => userEmail == null || e.OrganizationUsers.Any(u => u.Email == userEmail))
                    .Where(e => paid == null || 
                            (paid == true && !string.IsNullOrWhiteSpace(e.GatewaySubscriptionId)) ||
                            (paid == false && e.GatewaySubscriptionId == null))
                    .OrderBy(e => e.CreationDate)
                    .Skip(skip).Take(take)
                    .ToListAsync();
                return Mapper.Map<List<TableModel.Organization>>(organizations);
            }
        }

        public async Task UpdateStorageAsync(Guid id)
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                var ciphers = await dbContext.Ciphers
                    .Where(e => e.UserId == null && e.OrganizationId == id).ToListAsync();
                var storage = ciphers.Sum(e => JsonDocument.Parse(e.Attachments)?.RootElement.EnumerateArray()
                    .Sum(p => p.GetProperty("Size").GetInt64()) ?? 0);
                var organization = new EFModel.Organization
                {
                    Id = id,
                    RevisionDate = DateTime.UtcNow,
                    Storage = storage,
                };
                var set = GetDbSet(dbContext);
                set.Attach(organization);
                var entry = dbContext.Entry(organization);
                entry.Property(e => e.RevisionDate).IsModified = true;
                entry.Property(e => e.Storage).IsModified = true;
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task<ICollection<DataModel.OrganizationAbility>> GetManyAbilitiesAsync()
        {
            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var dbContext = GetDatabaseContext(scope);
                return await GetDbSet(dbContext)
                .Select(e => new DataModel.OrganizationAbility
                {
                    Enabled = e.Enabled,
                    Id = e.Id,
                    Use2fa = e.Use2fa,
                    UseEvents = e.UseEvents,
                    UsersGetPremium = e.UsersGetPremium,
                    Using2fa = e.Use2fa && e.TwoFactorProviders != null,
                    UseSso = e.UseSso,
                }).ToListAsync();
            }
        }
    }
}
