namespace IGB.Web.ViewModels;

public sealed record BreadcrumbItem(string Text, string? Url = null);

public sealed record PageHeaderViewModel(
    string Title,
    IReadOnlyList<BreadcrumbItem>? Breadcrumbs = null,
    string? Subtitle = null,
    bool Centered = false
);


