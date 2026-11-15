using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShopService.Application.DTOs.CartDTOs;
using ShopService.Application.Interfaces;
using System.Security.Claims;

namespace ShopService.API.Controllers
{
    [Route("api/cart")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }

        private Guid? GetCurrentUserId() =>
            Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<CartResponseDTO?>> CreateCart([FromBody] CreateCartDTO createCartDTO)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var response = await _cartService.CreateCartAsync(createCartDTO, userId.Value);
            if (response is null)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Timestamp = DateTime.UtcNow,
                    Message = "Cart could not be created. You may already have a cart, " +
                              "a product does not exist, or the requested quantity exceeds the available stock."
                });
            }

            return Ok(new
            {
                Status = "Success",
                Timestamp = DateTime.UtcNow,
                Message = "Cart has been created successfully.",
                UsersCart = response
            });
        }

        [Authorize]
        [HttpDelete("{cartId:guid}")]
        public async Task<ActionResult> DeleteCart([FromRoute] Guid cartId)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var result = await _cartService.DeleteCart(cartId, userId.Value);
            if (!result)
            {
                return NotFound(new
                {
                    Status = "Error",
                    Timestamp = DateTime.UtcNow,
                    Message = "Cart not found."
                });
            }

            return NoContent();
        }

        [Authorize]
        [HttpGet("items/{itemId:guid}")]
        public async Task<ActionResult<CartItemResponseDTO?>> GetCartItemById([FromRoute] Guid itemId)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var cartItem = await _cartService.GetCartItemByIdAsync(itemId, userId.Value);
            if (cartItem is null)
            {
                return NotFound(new
                {
                    Status = "Error",
                    Timestamp = DateTime.UtcNow,
                    Message = "Cart item not found."
                });
            }

            return Ok(new
            {
                Status = "Success",
                Timestamp = DateTime.UtcNow,
                Message = "Cart item has been found.",
                CartItem = cartItem
            });
        }

        [Authorize]
        [HttpPut("items/{itemId:guid}")]
        public async Task<ActionResult<CartItemResponseDTO?>> UpdateCartItem(
            [FromBody] UpdateCartItemDTO updatedCartItemDTO,
            [FromRoute] Guid itemId)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var cartItem = await _cartService.UpdateCartItem(updatedCartItemDTO, itemId, userId.Value);
            if (cartItem is null)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Timestamp = DateTime.UtcNow,
                    Message = "Cart item could not be updated. It may not exist, " +
                              "or the quantity is invalid or exceeds the available stock."
                });
            }

            return Ok(new
            {
                Status = "Success",
                Timestamp = DateTime.UtcNow,
                Message = "Cart item quantity has been updated successfully.",
                UpdatedCartItem = cartItem
            });
        }

        [Authorize]
        [HttpDelete("items/{itemId:guid}")]
        public async Task<ActionResult> RemoveCartItemFromCart([FromRoute] Guid itemId)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var removed = await _cartService.RemoveCartItemFromCart(itemId, userId.Value);
            if (!removed)
            {
                return NotFound(new
                {
                    Status = "Error",
                    Timestamp = DateTime.UtcNow,
                    Message = "Cart item not found."
                });
            }

            return NoContent();
        }

        [Authorize]
        [HttpPost("items")]
        public async Task<ActionResult<CartItemResponseDTO?>> AddCartItem([FromBody] AddCartItemDTO addCartItemDTO)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var cartItem = await _cartService.AddCartItemToCart(addCartItemDTO, userId.Value);
            if (cartItem is null)
            {
                return BadRequest(new
                {
                    Status = "Error",
                    Timestamp = DateTime.UtcNow,
                    Message = "Item could not be added to the cart. You may not have a cart yet, " +
                              "the product does not exist, or the quantity exceeds the available stock."
                });
            }

            return Ok(new
            {
                Status = "Success",
                Timestamp = DateTime.UtcNow,
                Message = "Item has been added to the cart.",
                CartItem = cartItem
            });
        }

        [Authorize]
        [HttpGet("items")]
        public async Task<ActionResult<IEnumerable<CartItemResponseDTO>?>> GetAllCartItems(
            [FromQuery] int pageNumber,
            [FromQuery] int pageSize)
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var cartItems = await _cartService.GetAllCartItems(userId.Value, pageNumber, pageSize);

            return Ok(new
            {
                Status = "Success",
                Timestamp = DateTime.UtcNow,
                Message = "Cart items have been fetched successfully.",
                CartItems = cartItems
            });
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<CartResponseDTO?>> GetMyCart()
        {
            var userId = GetCurrentUserId();
            if (userId is null) return Unauthorized();

            var myCart = await _cartService.GetUsersCart(userId.Value);
            if (myCart is null)
            {
                return NotFound(new
                {
                    Status = "Error",
                    Timestamp = DateTime.UtcNow,
                    Message = "You do not have a cart yet."
                });
            }

            return Ok(new
            {
                Status = "Success",
                Timestamp = DateTime.UtcNow,
                Message = "Your cart has been fetched successfully.",
                MyCart = myCart
            });
        }
    }
}
