using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IGB.Web.ViewModels.Components;

public enum SortDirection
{
    Asc,
    Desc
}

public sealed record TableSort(string Key, SortDirection Direction);

public sealed record TableColumn(
    string Header,
    string? SortKey = null,
    string? CssClass = null,
    string? HeaderAriaLabel = null
);

public sealed record PaginationViewModel(
    int Page,
    int PageSize,
    int TotalCount,
    string Action,
    string Controller,
    object? RouteValues = null
)
{
    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public int From => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int To => Math.Min(Page * PageSize, TotalCount);
}

public sealed record TableShellViewModel(
    string Id,
    IReadOnlyList<TableColumn> Columns,
    Func<IHtmlHelper, IHtmlContent> RenderRows,
    PaginationViewModel Pagination,
    string? Title = null,
    string? SearchQuery = null,
    string SearchParamName = "q",
    string? SearchPlaceholder = "Search...",
    TableSort? Sort = null,
    bool IsLoading = false,
    string? ErrorMessage = null,
    Func<IHtmlHelper, IHtmlContent>? RenderEmptyState = null,
    string? CssClass = null
);


