using Microsoft.EntityFrameworkCore;
using ShopService.Application.DTOs.ProductDTOs;
using ShopService.Application.Interfaces;
using ShopService.Domain.Entities;
using ShopService.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ShopService.Application.Service
{
    public class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _appDbContext;

        public ProductRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<bool> AdjustStockAsync(Guid productId, int quantityChange)
        {
            if (quantityChange == 0) return true;

            var now = DateTime.UtcNow;

            var affected = await _appDbContext.Products
                .Where(p => p.Id == productId && p.StockQuantity + quantityChange >= 0)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.StockQuantity, p => p.StockQuantity + quantityChange)
                    .SetProperty(p => p.UpdatedAt, now));

            return affected > 0;
        }

        public async Task<Product?> CreateProduct(Product product)
        {
            if (product is null) return null;

            var name = product.Name?.Trim();
            var sku = product.Sku?.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sku)) return null;

            var slug = string.IsNullOrEmpty(product.Slug)
                ? GenerateSlug(name)
                : product.Slug.Trim();

            if (string.IsNullOrEmpty(slug)) return null;  

            bool exists = await _appDbContext.Products
                .AnyAsync(p => p.Sku.ToLower() == sku.ToLower()
                            || p.Slug.ToLower() == slug.ToLower()
                            || p.Name.ToLower() == name.ToLower());

            if (exists) return null;

            product.Name = name;
            product.Sku = sku;
            product.Slug = slug;

            await _appDbContext.Products.AddAsync(product);
            await _appDbContext.SaveChangesAsync();
            return product;
        }

        public async Task<bool> DeleteProduct(Guid guid)
        {
            var product = await _appDbContext.Products
        .FirstOrDefaultAsync(p => p.Id == guid);

            if (product is null) return false;

            bool hasOrderItems = await _appDbContext.OrderItems
                .AnyAsync(o => o.ProductId == guid);

            bool hasCartItems = await _appDbContext.CartItems
                .AnyAsync(c => c.ProductId == guid);

            if (hasOrderItems || hasCartItems) return false;

            _appDbContext.Products.Remove(product);
            await _appDbContext.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<ProductResponseDTO>> GetAllProducts(int pageNumber = 1, int pageSize = 10)
        {
            return await _appDbContext.Products
                .AsNoTracking()
                  .OrderBy(e => e.CreatedAt)
                  .ThenBy(e => e.Id)
                  .Skip((pageNumber - 1) * pageSize)
                  .Take(pageSize)
                  .Select(MapToProductResponseDTO())
                  .ToListAsync();
        }

        public async Task<IEnumerable<ProductResponseDTO>> GetFeaturedProducts(int pageNumber = 1, int pageSize = 10)
        {
            return await _appDbContext.Products
                 .AsNoTracking()
                 .Where(e => e.IsFeatured)
                 .OrderBy(e => e.CreatedAt)
                 .ThenBy(e => e.Id)
                 .Skip((pageNumber - 1) * pageSize)
                 .Take(pageSize)
                 .Select(MapToProductResponseDTO())
                 .ToListAsync();
        }

        public async Task<IEnumerable<ProductResponseDTO>> GetLowStockProductsAsync(int pageNumber = 1, int pageSize = 10)
        {
            return await _appDbContext.Products
     .AsNoTracking()
     .Where(e => e.StockQuantity <= (e.LowStockThreshold ?? 10))
     .OrderBy(e => e.CreatedAt)
     .ThenBy(e => e.Id)
     .Skip((pageNumber - 1) * pageSize)
     .Take(pageSize)
     .Select(MapToProductResponseDTO())
     .ToListAsync();
        }

        public async Task<Product?> GetProductById(Guid productId)
        {
            return await _appDbContext.Products
     .FirstOrDefaultAsync(e => e.Id == productId);
        }

        public async Task<IEnumerable<ProductCategoryResponseDTO>> GetProductCategories(int pageNumber = 1, int pageSize = 10)
        {
            return await _appDbContext.Categories
                 .AsNoTracking()
                .OrderBy(e => e.CreatedAt)
                .ThenBy(e => e.Id)
                  .Skip((pageNumber - 1) * pageSize)
                  .Take(pageSize)
                .Select(c => new ProductCategoryResponseDTO
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    ParentCategoryId = c.ParentCategoryId,
                    DisplayOrder = c.DisplayOrder,
                    IsActive = c.IsActive,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    Products = c.Products.Select(p => new ProductResponseDTO
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Slug = p.Slug,
                        Description = p.Description,
                        ShortDescription = p.ShortDescription,
                        Sku = p.Sku,
                        Price = p.Price,
                        CompareAtPrice = p.CompareAtPrice,
                        StockQuantity = p.StockQuantity,
                        LowStockThreshold = p.LowStockThreshold,
                        IsActive = p.IsActive,
                        IsFeatured = p.IsFeatured,
                        Brand = p.Brand,
                        Weight = p.Weight,
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                })
                .ToListAsync();
        }

        public async Task<IEnumerable<ProductReviewResponseDTO>> GetProductReviews(Guid productId, int pageNumber = 1, int pageSize = 10)
        {
            return await _appDbContext.Reviews
                  .AsNoTracking()
                .Where(e => e.ProductId == productId)
               .OrderBy(e => e.CreatedAt)
               .ThenBy(e => e.Id)
                  .Skip((pageNumber - 1) * pageSize)
                  .Take(pageSize)
                .Select(e => new ProductReviewResponseDTO
                {
                    ProductId = e.ProductId,
                    ProductName = e.Product.Name,
                    ProductSku = e.Product.Sku,
                    ReviewId = e.Id,
                    Slug = e.Product.Slug,
                    Rating = e.Rating,
                    Comment = e.Comment,
                    ReviewCreatedAt = e.CreatedAt,
                }).ToListAsync();
        }

        public async Task<Product?> UpdateProduct(Product updatedProduct, Guid productId)
        {

            if (updatedProduct is null) return null;

            var product = await _appDbContext.Products
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product is null) return null;

            var newName = updatedProduct.Name?.Trim();
            var newSku = updatedProduct.Sku?.Trim();
            if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(newSku)) return null;

            if (updatedProduct.CategoryId.HasValue &&
                !await _appDbContext.Categories.AnyAsync(c => c.Id == updatedProduct.CategoryId))
            {
                return null;
            }
            var newSlug = product.Name != newName ? GenerateSlug(newName) : product.Slug;

            bool exists = await _appDbContext.Products
                .AnyAsync(p => p.Id != productId &&
                              (p.Sku.ToLower() == newSku.ToLower()
                            || p.Slug.ToLower() == newSlug.ToLower()
                            || p.Name.ToLower() == newName.ToLower()));

            if (exists) return null;

            product.Name = newName;
            product.Sku = newSku;
            product.Slug = newSlug;
            product.Description = updatedProduct.Description;
            product.ShortDescription = updatedProduct.ShortDescription;
            product.Price = updatedProduct.Price;
            product.CompareAtPrice = updatedProduct.CompareAtPrice;
            product.StockQuantity = updatedProduct.StockQuantity;
            product.LowStockThreshold = updatedProduct.LowStockThreshold;
            product.IsActive = updatedProduct.IsActive;
            product.IsFeatured = updatedProduct.IsFeatured;
            product.Brand = updatedProduct.Brand;
            product.Weight = updatedProduct.Weight;
            product.CategoryId = updatedProduct.CategoryId;

            await _appDbContext.SaveChangesAsync();
            return product;
        }

        private string GenerateSlug(string name)
        {
            var slug = name.Trim().ToLowerInvariant()
           .Replace("&", " and ")
            .Replace(" ", "-");
            slug = Regex.Replace(slug, "[^a-z0-9-]", "");  
            slug = Regex.Replace(slug, "-+", "-");          
            slug = slug.Trim('-');                          
            return slug;
        }

        private static Expression<Func<Product, ProductResponseDTO>> MapToProductResponseDTO()
        {
            return e => new ProductResponseDTO
            {
                Id = e.Id,
                Name = e.Name,
                Slug = e.Slug,
                Description = e.Description,
                ShortDescription = e.ShortDescription,
                Sku = e.Sku,
                Price = e.Price,
                CompareAtPrice = e.CompareAtPrice,
                StockQuantity = e.StockQuantity,
                LowStockThreshold = e.LowStockThreshold,
                IsActive = e.IsActive,
                IsFeatured = e.IsFeatured,
                Brand = e.Brand,
                Weight = e.Weight,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            };
        }
    }
}