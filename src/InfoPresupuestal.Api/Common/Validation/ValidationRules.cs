using FluentValidation;
using InfoPresupuestal.Api.Common.MultiTenancy;

namespace InfoPresupuestal.Api.Common.Validation;

public static class ValidationRules
{
    /// <summary>Mínimo 8 caracteres con mayúscula, minúscula y número.</summary>
    public static IRuleBuilderOptions<T, string> ContrasenaSegura<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(8).MaximumLength(100)
            .Matches("[A-Z]").WithMessage("La contraseña debe tener al menos una mayúscula.")
            .Matches("[a-z]").WithMessage("La contraseña debe tener al menos una minúscula.")
            .Matches("[0-9]").WithMessage("La contraseña debe tener al menos un número.");

    /// <summary>Formato de tenant: minúsculas, dígitos y guiones (igual que tenant_id en la base).</summary>
    public static IRuleBuilderOptions<T, string> TenantId<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Must(v => TenantResolutionFilter.TenantIdFormat().IsMatch(v ?? string.Empty))
            .WithMessage("Solo minúsculas, números y guiones, empezando por letra o número (máx. 63).");
}
