using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IGB.Web.ViewModels.Components;

public sealed record CardViewModel(
    string? Title = null,
    string? Subtitle = null,
    bool IsLoading = false,
    string? ErrorMessage = null,
    Func<IHtmlHelper, IHtmlContent>? Body = null,
    Func<IHtmlHelper, IHtmlContent>? Footer = null,
    string? CssClass = null
);

public sealed record BadgeViewModel(
    string Text,
    string Variant = "secondary", // primary|secondary|success|warning|danger|info
    string? CssClass = null
);

public sealed record AvatarViewModel(
    string? ImageUrl = null,
    string? Initials = null,
    string Alt = "Avatar",
    int SizePx = 32,
    string? CssClass = null
);

public sealed record StatusIndicatorViewModel(
    string Text,
    string Variant = "info", // success|warning|danger|info|secondary
    string? CssClass = null
);

public sealed record ProgressBarViewModel(
    int Value, // 0..100
    string Variant = "primary",
    string? Label = null,
    bool ShowLabel = false,
    string? CssClass = null
);


