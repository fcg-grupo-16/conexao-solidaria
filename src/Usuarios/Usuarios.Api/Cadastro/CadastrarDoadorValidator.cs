using FluentValidation;
using Usuarios.Domain;

namespace Usuarios.Api.Cadastro;

/// <summary>Regras de formato do cadastro. A unicidade de e-mail e CPF é checada no banco.</summary>
public sealed class CadastrarDoadorValidator : AbstractValidator<CadastrarDoadorRequest>
{
    /// <summary>Tamanho mínimo da senha.</summary>
    public const int TamanhoMinimoSenha = 8;

    /// <summary>Tamanho máximo da senha.</summary>
    public const int TamanhoMaximoSenha = 128;

    /// <summary>Define as regras.</summary>
    public CadastrarDoadorValidator()
    {
        RuleFor(r => r.NomeCompleto)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe o nome completo.")
            .MaximumLength(Usuario.TamanhoMaximoNome).WithMessage($"O nome deve ter no máximo {Usuario.TamanhoMaximoNome} caracteres.")
            .Must(nome => nome.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
            .WithMessage("Informe nome e sobrenome.");

        RuleFor(r => r.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe o e-mail.")
            .MaximumLength(Usuario.TamanhoMaximoEmail).WithMessage($"O e-mail deve ter no máximo {Usuario.TamanhoMaximoEmail} caracteres.")
            .EmailAddress().WithMessage("E-mail em formato inválido.");

        RuleFor(r => r.Cpf)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe o CPF.")
            .Must(Cpf.EhValido).WithMessage("CPF inválido. Use 000.000.000-00 ou apenas os 11 dígitos.");

        RuleFor(r => r.Senha)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Informe a senha.")
            .MinimumLength(TamanhoMinimoSenha).WithMessage($"A senha deve ter pelo menos {TamanhoMinimoSenha} caracteres.")
            .MaximumLength(TamanhoMaximoSenha).WithMessage($"A senha deve ter no máximo {TamanhoMaximoSenha} caracteres.")
            .Must(senha => senha.Any(char.IsLetter) && senha.Any(char.IsDigit))
            .WithMessage("A senha deve conter letras e números.");
    }
}
