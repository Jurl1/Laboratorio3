using System.Diagnostics;
using Lab3.Web.Data;
using Lab3.Web.Infrastructure;
using Lab3.Web.Models;
using Lab3.Web.Services;
using Lab3.Web.Services.Catalog;
using Lab3.Web.Services.Tmdb;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lab3.Web.Controllers;

public class HomeController(ICatalogService catalog, AppDbContext db) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = new HomeViewModel();
        try
        {
            model.Lists = await catalog.GetHomeAsync(ct);
        }
        catch (CatalogUnavailableException ex)
        {
            model.CatalogError = ex.Message;
        }

        model.LatestReviews = await db.Reviews
            .OrderByDescending(r => r.CreatedAt)
            .Take(8)
            .ToItems(User.UserId())
            .ToListAsync(ct);

        return View(model);
    }

    [HttpGet("/acerca")]
    public IActionResult About() => View();
}

[Route("error")]
[IgnoreAntiforgeryToken]
public class ErrorController : Controller
{
    [Route("{code:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Show(int code)
    {
        Response.StatusCode = code is >= 400 and <= 599 ? code : 500;
        ViewData["Code"] = Response.StatusCode;
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View("~/Views/Shared/Error.cshtml");
    }
}
