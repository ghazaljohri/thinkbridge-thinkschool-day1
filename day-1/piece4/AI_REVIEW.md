# AI-Assisted Review

## AI Review of OrderService

I asked ChatGPT to review `OrderService.cs` and propose a refactor using the Strategy Pattern.

### AI Recommendation

The AI determined that the Strategy Pattern is not justified at this stage because there is currently only one order-creation strategy.

The current service already:
- validates the customer name and order total
- creates a pending order
- saves through `IOrderRepository`
- logs the created order
- delegates retrieval to the repository

Introducing a strategy interface and implementation would add abstraction and dependency-injection complexity without providing a meaningful benefit.

### Decision

I agreed with the AI recommendation and did not apply the Strategy Pattern.

This keeps the existing behavior and tests unchanged while avoiding unnecessary abstraction.

### When Strategy Would Become Justified

The Strategy Pattern could become useful if order-creation rules started varying based on factors such as:
- customer tier
- region
- order type
- fulfillment channel
- promotion policy

At that point, different creation policies could be extracted into separate strategies.

### Conclusion

The AI was used as a review and design-assistance tool rather than blindly accepting its proposed refactoring. The recommendation was evaluated against the current requirements, and the existing simpler implementation was retained.
