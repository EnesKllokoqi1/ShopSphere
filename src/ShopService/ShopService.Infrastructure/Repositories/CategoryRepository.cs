using Microsoft.EntityFrameworkCore;
using ShopService.Application.DTOs.CategoryDTOs;
using ShopService.Application.DTOs.ProductDTOs;
using ShopService.Application.Interfaces;
using ShopService.Domain.Entities;
using ShopService.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ShopService.Infrastructure.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly AppDbContext _appDbContext;

        public CategoryRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }
        public async Task<Category?> CreateCategory(Category category)
        {
            if (category is null) return null;

            var name = category.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return null;

            bool exists = await _appDbContext.Categories
                .AnyAsync(e => e.Name.ToLower() == name.ToLower());

            if (exists) return null;

            if (category.ParentCategoryId.HasValue &&
                !await _appDbContext.Categories.AnyAsync(c => c.Id == category.ParentCategoryId))
            {
                return null;
            }

            category.Name = name;

            _appDbContext.Categories.Add(category);
            await _appDbContext.SaveChangesAsync();
            return category;
        }

        public async Task<bool> DeleteCategory(Guid categoryId)
        {
            var category = await _appDbContext.Categories
       .FirstOrDefaultAsync(c => c.Id == categoryId);

            if (category is null) return false;

            bool hasChildren = await _appDbContext.Categories
                .AnyAsync(c => c.ParentCategoryId == categoryId);

            bool hasProducts = await _appDbContext.Products
                .AnyAsync(p => p.CategoryId == categoryId);

            if (hasChildren || hasProducts) return false;

            _appDbContext.Categories.Remove(category);
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<CategoryResponseDTO>> GetAllCategories(int pageNumber = 1, int pageSize = 10)
        {

            return await _appDbContext.Categories
                .AsNoTracking()
                .OrderBy(e => e.CreatedAt)
                .ThenBy(e => e.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToCategoryResponseDTO())
                .ToListAsync();
        }

        private static Expression<Func<Category, CategoryResponseDTO>> MapToCategoryResponseDTO()
        {
            return e => new CategoryResponseDTO
            {
                Id = e.Id,
                Name = e.Name,
                Description = e.Description,
                ParentCategoryId = e.ParentCategoryId,
                DisplayOrder = e.DisplayOrder,
                IsActive = e.IsActive,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                ParentCategoryName = e.ParentCategory != null ? e.ParentCategory.Name : null,
                SubCategories = e.SubCategories.Select(sub => new CategoryResponseDTO
                {
                    Id = sub.Id,
                    Name = sub.Name,
                    Description = sub.Description,
                    ParentCategoryId = sub.ParentCategoryId,
                    DisplayOrder = sub.DisplayOrder,
                    IsActive = sub.IsActive,
                    CreatedAt = sub.CreatedAt,
                    UpdatedAt = sub.UpdatedAt
                })
            };
        }

        public async Task<Category?> GetCategoryById(Guid categoryId)
        {
            return await _appDbContext.Categories
        .Include(c => c.ParentCategory) 
        .Include(c => c.SubCategories)   
        .FirstOrDefaultAsync(c => c.Id == categoryId);
        }

        public async Task<Category?> UpdateCategory(Category updatedCategory, Guid categoryId)
        {
            if (updatedCategory is null) return null;

            var theCategory = await _appDbContext.Categories
                .FirstOrDefaultAsync(c => c.Id == categoryId);

            if (theCategory is null) return null;

            var newName = updatedCategory.Name?.Trim();
            if (string.IsNullOrWhiteSpace(newName)) return null;

            // 1. Can't be its own parent
            if (updatedCategory.ParentCategoryId == categoryId)
                return null;

            // 2. Cycle detection: walk up the proposed parent chain
            var ancestorId = updatedCategory.ParentCategoryId;
            while (ancestorId.HasValue)
            {
                if (ancestorId == categoryId) return null;

                ancestorId = await _appDbContext.Categories
                    .Where(c => c.Id == ancestorId)
                    .Select(c => c.ParentCategoryId)
                    .FirstOrDefaultAsync();
            }
            // 3. Parent must exist if provided
            if (updatedCategory.ParentCategoryId.HasValue &&
                !await _appDbContext.Categories.AnyAsync(c => c.Id == updatedCategory.ParentCategoryId))
            {
                return null;
            }
            // 4. Case-insensitive dup check, excluding self
            if (!string.Equals(theCategory.Name, newName, StringComparison.OrdinalIgnoreCase))
            {
                bool nameExists = await _appDbContext.Categories
                    .AnyAsync(c => c.Id != categoryId && c.Name.ToLower() == newName.ToLower());

                if (nameExists) return null;
            }
            theCategory.Name = newName;
            theCategory.Description = updatedCategory.Description;
            theCategory.ParentCategoryId = updatedCategory.ParentCategoryId;
            theCategory.DisplayOrder = updatedCategory.DisplayOrder;
            theCategory.IsActive = updatedCategory.IsActive;

            await _appDbContext.SaveChangesAsync();
            return theCategory;
        }
    }
}