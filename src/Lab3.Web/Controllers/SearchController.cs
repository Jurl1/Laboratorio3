using Lab3.Web.Data;
using Lab3.Web.Models;
using Lab3.Web.Services.Catalog;
using Lab3.Web.Services.Tmdb;
using Microsoft.AspNetCore.Mvc;

namespace Lab3.Web.Controllers;

public class SearchController(ICatalogService catalog) : Controller
{
    private static readonly string[] Tabs = ["peliculas", "series", "anime"];

    [HttpGet("/buscar")]
    public async Task<IActionResult> Index(string? q, string? tab, int page = 1, CancellationToken ct = default)
    {
        var model = new SearchViewModel
        {
            Query = (q ?? "").Trim(),
            Tab = Tabs.Contains(tab) ? tab! : "peliculas",
            Page = Math.Max(page, 1),
        };
        if (model.Query.Length > 100) model.Query = model.Query[..100];
        if (model.Query.Length == 0) return View(model);

        try
        {
            var res = await catalog.SearchAsync(model.Query, model.Page, ct);
            model.TotalPages = res.TotalPages;
            // Pestañas de PLAN.md §7: el anime va aparte (género 16 + origen JP).
            model.Anime = res.Items.Where(i => i.IsAnime).ToList();
            model.Movies = res.Items.Where(i => !i.IsAnime && i.Type == TmdbType.Movie).ToList();
            model.Series = res.Items.Where(i => !i.IsAnime && i.Type == TmdbType.Tv).ToList();

            // Si no se eligió pestaña, abrir la primera que tenga resultados.
            if (tab is null)
                model.Tab = model.Movies.Count > 0 ? "peliculas" : model.Series.Count > 0 ? "series" : model.Anime.Count > 0 ? "anime" : "peliculas";
        }
        catch (CatalogUnavailableException ex)
        {
            model.CatalogError = ex.Message;
        }

        return View(model);
    }
}
