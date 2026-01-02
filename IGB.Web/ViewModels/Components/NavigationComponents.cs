namespace IGB.Web.ViewModels.Components;

public sealed record BreadcrumbsViewModel(
    IReadOnlyList<BreadcrumbItem> Items
);

public sealed record TabItem(string Id, string Label, string? Href = null, bool Disabled = false);

public sealed record TabsViewModel(
    string Id,
    IReadOnlyList<TabItem> Tabs,
    string ActiveId,
    string? CssClass = null
);


