using FluentValidation;

namespace InfoPresupuestal.Api.Features.Products.CreateProduct;

internal sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(ProductConfiguration.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(ProductConfiguration.DescriptionMaxLength);
        RuleFor(c => c.Price).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(c => c.Stock).GreaterThanOrEqualTo(0);
    }
}
