# Contract: Fluent UI Blazor Integration

**Feature**: `007-reconciliation-approval-workflow`  
**Project**: `BillDrift.Web`  
**Skill reference**: `.cursor/skills/fluentui-blazor-usage/SKILL.md`  
**Date**: 2026-07-02

## Purpose

First BillDrift UI feature. Refactors the Bootstrap skeleton starter into **Microsoft.FluentUI.AspNetCore.Components v5** and implements the approval queue operator experience.

---

## Package & Registration

**csproj**:

```xml
<PackageReference Include="Microsoft.FluentUI.AspNetCore.Components" Version="5.*" />
```

Use latest **v5** package version compatible with `net10.0` at implementation time (verify on NuGet; do not use v4 package line).

**Program.cs**:

```csharp
builder.Services.AddFluentUIComponents();
builder.Services.AddHttpClient<IApprovalApiClient, ApprovalApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://api");
});
```

**App host** (`Components/App.razor` or layout root):

```razor
<FluentProviders />
@* Routes / Router *@
```

**index / host HTML** — add:

```html
<link href="_content/Microsoft.FluentUI.AspNetCore.Components/css/default-fuib.css" rel="stylesheet" />
```

Remove Bootstrap CSS from layout when Fluent layout replaces sidebar (Bootstrap files may remain in wwwroot until cleanup task).

---

## Layout Refactor

Replace `Components/Layout/MainLayout.razor` + `NavMenu.razor`:

| Remove (Bootstrap/v4) | Add (Fluent v5) |
|-----------------------|-----------------|
| `.sidebar` div + NavMenu | `FluentLayout` + `FluentLayoutItem Area=Menu` |
| Bootstrap nav links | `FluentNav`, `FluentNavItem`, `FluentNavCategory` |
| `FluentNavMenu` / `FluentNavLink` | **Do not use** — v4 removed |

**Navigation items (v1)**:

- Home → `/`
- Reconciliation → `/reconciliation` (placeholder)
- **Approvals** → `/approvals/{runId}` (primary feature route)
- Settings → placeholder

Use `FluentLayoutHamburger` for responsive menu.

---

## Approval Pages

### `Pages/Approvals/ApprovalQueuePage.razor`

Route: `/approvals/{RunId:guid}`

**Components**:
- `FluentTabs` — "Subscription" | "Catalogue" | "Investigation" (FR-022 filter)
- `FluentDataGrid<ApprovalProposalViewModel>` — customer grouping with expandable rows OR per-customer sections
- `FluentBadge` — state (`Pending`, `Approved`, `Rejected`, `Stale`) and eligibility
- `FluentButton Appearance=Primary` — Approve (disabled when `!CanApprove`)
- `FluentButton` — Reject → opens dialog
- `FluentMessageBar Intent=Error` — API errors (constitution III)

**Prior vs proposed**: Two-column `FluentStack` with labelled values from `PriorValues` / `ProposedValues` dictionaries.

### `Components/Approval/RejectProposalDialog.razor`

- `FluentTextArea` for required rejection reason
- `IDialogService.ShowDialogAsync<RejectProposalDialog>`

### `Components/Approval/BulkApproveDialog.razor`

- Shows summary count and action types from preview endpoint
- Confirm / Cancel

### `Components/Approval/ExportChangesetPanel.razor`

- Export button → calls API → shows download link + entry count
- Empty state when no approved items

---

## Theming

v5 uses CSS custom properties — **no** `<FluentDesignTheme>`.

Optional theme toggle via `Blazor.theme.switchTheme` JS interop per skill.

Default: system/light acceptable for v1.

---

## Error & Loading States

| State | Pattern |
|-------|---------|
| Loading queue | `FluentProgressBar` or skeleton |
| Empty queue | `FluentMessageBar` informational |
| API failure | `FluentMessageBar Intent=Error` with retry |
| Permission denied | Disable approve/export buttons; message explains read-only |

---

## Accessibility

- Use `Label` parameters on form controls
- Dialog modal focus trap via Fluent dialog provider
- Data grid row actions keyboard accessible

---

## Out of Scope (UI)

- Full reconciliation exception browser (005 UI — future)
- Stripe apply UI (future feature)
- Authentication UI (placeholder header operator id)

---

## Verification

1. App starts via Aspire; Web loads without Bootstrap layout regressions on approval route
2. Fluent providers render (no console missing provider errors)
3. Approval grid loads from API mock/fixture run
4. Approve/reject dialogs function in Interactive Server mode
