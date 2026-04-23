using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PromoRadar.Web.ViewModels;

public class CreateTrackedProductViewModel
{
    [Required(ErrorMessage = "Informe o nome do produto que deseja monitorar.")]
    [StringLength(180, ErrorMessage = "O nome deve ter no máximo {1} caracteres.")]
    [Display(Name = "Nome do produto")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione uma categoria.")]
    [StringLength(80, ErrorMessage = "A categoria deve ter no máximo {1} caracteres.")]
    [Display(Name = "Categoria")]
    public string Category { get; set; } = string.Empty;

    [StringLength(80, ErrorMessage = "A marca deve ter no máximo {1} caracteres.")]
    [Display(Name = "Marca")]
    public string? Brand { get; set; }

    [StringLength(500, ErrorMessage = "A URL deve ter no máximo {1} caracteres.")]
    [Display(Name = "URL do produto")]
    public string? Url { get; set; }

    [Range(0.01, 9999999, ErrorMessage = "Informe um preço alvo válido.")]
    [Display(Name = "Preço alvo")]
    public decimal? TargetPrice { get; set; }

    [Display(Name = "Imagem do produto")]
    public IFormFile? ImageFile { get; set; }

    public IReadOnlyList<string> CategoryOptions { get; set; } = [];
}
