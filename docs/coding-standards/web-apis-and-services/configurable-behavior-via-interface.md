---
title: "Make Behavior Safely Configurable via Interface + DI, Not Inline Conditionals"
---

# Make Behavior Safely Configurable via Interface + DI, Not Inline Conditionals


## The Standard

When a piece of behavior needs to vary by configuration (e.g. how an address is formatted per customer/tenant), extract it behind a small interface, implement the current behavior as one implementation of that interface, and wire the concrete implementation (and its configuration values) at composition-root time (`Program.cs`) via DI — reading the actual settings from `IConfiguration`. Consuming code (Razor Page models, services) must depend only on the interface, never on the configuration key or the concrete implementation.

## Why

Before this change, `CompaniesModel` had no way to vary how an address is displayed without editing the page itself. The final version introduces `IAddressLabel` with a single method `For(AddressViewModel? address)`, and one implementation, `SearchAndReplaceAddressLabel(string format)`, that interprets a placeholder-based format string (`"@city, @country"`). `Program.cs` builds that implementation once, reading the format from `Customizations:Companies:AddressFormat` in configuration, and registers it as the `IAddressLabel` singleton. `CompaniesModel` takes `IAddressLabel` as a constructor dependency and never sees the configuration key or the format-string parsing — the configurability is fully isolated behind the interface, so it can be changed per-environment/per-tenant, swapped for a different implementation, or unit-tested independently of ASP.NET Core.

## Before (Anti-pattern)

```csharp
public class CompaniesModel : PageModel
{
    public List<CompanyViewModel> Companies { get; set; } = new();

    public async Task OnGetAsync()
    {
        Companies = (await _companiesQuery.GetAllAsync()).ToList();
        // address formatting, if needed, would end up hardcoded here or duplicated per page
    }
}
```

## After (Standard)

```csharp
public interface IAddressLabel
{
    string For(AddressViewModel? address);
}

public class SearchAndReplaceAddressLabel(string format) : IAddressLabel
{
    public string For(AddressViewModel? address) => address is null ? "-" : ApplyTo(address);
    // ... placeholder substitution using `format` ...
}

// Program.cs (composition root) — the only place that reads configuration for this behavior
builder.Services.AddSingleton<IAddressLabel>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var addressFormat = configuration.GetValue<string>("Customizations:Companies:AddressFormat") ?? string.Empty;
    return new SearchAndReplaceAddressLabel(addressFormat);
});

public class CompaniesModel(CompaniesQuery companiesQuery, IUnitOfWork unitOfWork, IAddressLabel addressLabel) : PageModel
{
    public async Task OnGetAsync() =>
        Companies = (await companiesQuery.GetAllAsync())
            .Select(company => (company, address: addressLabel.For(company.Addresses.FirstOrDefault())))
            .ToList();
}
```

## Rules for LLMs / Agents

- When behavior must vary by configuration, define a narrow interface expressing the behavior (one or two methods), not a bag of config values passed around.
- Implement the interface with a class whose constructor takes the already-resolved configuration value(s) as plain parameters (e.g. `string format`) — the implementation must not read `IConfiguration` itself.
- Read configuration values and construct the implementation only in the composition root (`Program.cs` / DI registration), using `IConfiguration`/`IOptions<T>` there and nowhere else.
- Inject the interface into consumers via constructor DI; consumers must not new-up the implementation or reference the configuration key.
- Keep the configuration-driven parser (e.g. format-string substitution) as a pure function of its inputs so it is independently unit-testable without DI or configuration.

## When NOT to apply

If the behavior never varies (no configuration, no per-tenant/per-environment difference, and no anticipated need to swap implementations), introducing an interface and DI registration is unnecessary ceremony — a plain method/class is fine.
