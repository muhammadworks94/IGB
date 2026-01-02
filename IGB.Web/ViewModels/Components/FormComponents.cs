namespace IGB.Web.ViewModels.Components;

public sealed record SelectOption(string Value, string Label, bool Disabled = false);

public sealed record RadioOption(string Value, string Label, string? Description = null, bool Disabled = false);

public sealed record InputFieldViewModel(
    string Name,
    string Label,
    string Type = "text", // text|email|password|number|tel
    string? Value = null,
    string? Placeholder = null,
    bool Required = false,
    bool Disabled = false,
    string? HelpText = null,
    string? ErrorMessage = null,
    string? Autocomplete = null,
    string? CssClass = null
);

public sealed record TextareaFieldViewModel(
    string Name,
    string Label,
    string? Value = null,
    string? Placeholder = null,
    int Rows = 4,
    bool Required = false,
    bool Disabled = false,
    string? HelpText = null,
    string? ErrorMessage = null,
    string? CssClass = null
);

public sealed record SelectFieldViewModel(
    string Name,
    string Label,
    IReadOnlyList<SelectOption> Options,
    string? Value = null,
    string? Placeholder = null,
    bool Required = false,
    bool Disabled = false,
    string? HelpText = null,
    string? ErrorMessage = null,
    string? CssClass = null
);

public sealed record CheckboxFieldViewModel(
    string Name,
    string Label,
    bool Checked = false,
    bool Disabled = false,
    string? HelpText = null,
    string? ErrorMessage = null,
    string? CssClass = null
);

public sealed record RadioGroupViewModel(
    string Name,
    string Label,
    IReadOnlyList<RadioOption> Options,
    string? Value = null,
    bool Required = false,
    bool Disabled = false,
    string? HelpText = null,
    string? ErrorMessage = null,
    string? CssClass = null
);

public sealed record DateTimeFieldViewModel(
    string Name,
    string Label,
    string Mode = "date", // date|time|datetime-local
    string? Value = null,
    bool Required = false,
    bool Disabled = false,
    string? HelpText = null,
    string? ErrorMessage = null,
    string? CssClass = null
);

public sealed record FileUploadImageViewModel(
    string Name,
    string Label,
    string? CurrentImageUrl = null,
    string Accept = "image/*",
    bool Disabled = false,
    string? HelpText = null,
    string? ErrorMessage = null,
    string? CssClass = null
);

public sealed record PhoneFieldViewModel(
    string CountryCodeName,
    string PhoneName,
    string Label,
    IReadOnlyList<SelectOption> CountryCodes,
    string? CountryCodeValue = null,
    string? PhoneValue = null,
    string? Placeholder = null,
    bool Required = false,
    bool Disabled = false,
    string? HelpText = null,
    string? ErrorMessage = null,
    string? CssClass = null
);


