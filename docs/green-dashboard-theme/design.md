# Green Dashboard Theme and Sidebar-Ready Layout

> **Status:** Approved for implementation

## 1. Executive summary

The current dashboard is functional but visually generic, with mixed blue, green, amber, red, slate, and gray accents. Employees need a cleaner internal tool that feels consistent with the requested white and `#119754` brand direction. This change will introduce a small accessible color system, a branded top bar, more polished cards and tables, and a layout that can accept a chatbot sidebar later without showing an empty placeholder today. The main tradeoff is that `#119754` does not provide enough contrast with white for normal-size text, so a darker related green will be used wherever white text must meet accessibility requirements.

## 2. Context and scope

The authenticated page currently places the employee heading, clock card, and time-log table inside one centered slate background. Blue is used for section labels and login controls, while clock actions each introduce another unrelated color. The requested theme should make white the dominant surface and green the consistent brand accent without changing clock, authentication, pagination, or time-log behavior.

This design covers the login page, authenticated top bar, clock card, time-log table, responsive page spacing, focus states, and a future sidebar layout hook. It does not add chatbot content, conversation state, AI calls, or new backend behavior.

## 3. System context

The change stays inside the Angular presentation layer and preserves the API and authentication boundaries described in [the time clock dashboard design](../time-clock-dashboard/design.md).

```text
DashboardPage
|-- Branded top bar
`-- dashboard-layout
    |-- dashboard-main
    |   |-- ClockCard
    |   `-- TimeLogTable
    `-- dashboard-sidebar (added only when the chatbot is implemented)
```

The global stylesheet owns shared color tokens and page defaults. Each component owns its local spacing and states. The future chatbot sidebar will be a sibling of `dashboard-main`, not a child of the clock or table components.

## 4. Proposed design

### How it works

An employee opens the application and sees a white login surface with green branding, green focus treatment, and one strong primary action. After login, a compact branded top bar shows the portal identity, employee name and number, and a high-contrast logout control. The main white page contains a visually stronger clock card followed by a refined history table. Status, loading, error, and action behavior remain unchanged.

The page uses one-column layout today. When a chatbot component is added later, inserting an element with the `dashboard-sidebar` class will create a two-column desktop layout automatically. On smaller screens the sidebar will stack below the main content. No empty sidebar or "coming soon" panel is rendered in this change.

### Components and responsibilities

The global stylesheet owns `--brand`, `--brand-dark`, `--brand-soft`, surface, border, and focus tokens. It does not own component layout or application state.

`DashboardPage` owns the top bar, maximum page width, responsive main and optional-sidebar grid, and overall section spacing. It does not own clock or table internals.

`ClockCard` owns timer emphasis, status badges, summary tiles, action-button hierarchy, and its existing loading and error states. It does not decide which actions are valid.

`TimeLogTable` owns the history card header, refresh and pagination treatments, table header, row hover, expanded-break styling, and status badges. It does not change time-log values or table semantics.

`LoginPanel` owns the branded sign-in card, field focus states, validation presentation, and submit button. It does not change authentication or session handling.

### Decisions

The exact `#119754` color will be the brand accent and decorative top-bar highlight. White text on `#119754` has about 3.76:1 contrast, which is not sufficient for normal text. A darker `#08713e` green will therefore be used for top-bar and primary-button backgrounds with white text, while `#119754` remains visible in borders, focus rings, icons, large decorative elements, and soft green surfaces.

White will remain the dominant page and card surface. A very light green-gray background may separate the content area from cards, but it must read as white rather than as a colored dashboard.

The future sidebar will be prepared through layout classes instead of a placeholder component. This avoids unused screen space and avoids implying that the chatbot already exists.

## 5. Invariants and requirements

### Invariants

1. Styling must not change authentication, clock actions, live timers, time-log values, pagination, or API calls.
2. Every existing interactive control remains keyboard reachable and has a visible focus state.
3. Normal-size text and essential control boundaries meet WCAG AA contrast targets.
4. The page renders without horizontal overflow at 390 CSS pixels, except for the intentionally scrollable time-log table.
5. No chatbot panel, message, button, or AI behavior appears before the chatbot feature is implemented.
6. Adding one `dashboard-sidebar` sibling later is sufficient to enable the prepared desktop layout.

### Requirements

- Define shared green theme tokens once and remove blue accents from dashboard and login surfaces.
- Add a top bar with a darker accessible green background and a visible `#119754` accent.
- Use consistent rounded corners, borders, shadows, spacing, and hover states across cards.
- Give the live `HH:MM:SS` values stronger visual hierarchy with stable tabular numerals.
- Preserve amber for break warnings and red for destructive clock-out or errors, while green remains the primary brand and success color.
- Improve table scanning with a soft green header, restrained row hover, clearer expand controls, and aligned footer controls.
- Keep controls usable at mobile, tablet, and wide desktop widths.

## 6. Interfaces and data

No API, TypeScript data model, route, event, or stored value changes. The only new interface is a presentational CSS contract:

- `dashboard-layout` is the responsive grid container.
- `dashboard-main` is the current clock and history column.
- `dashboard-sidebar` is the optional future chatbot column.

### Naming and identity

The layout class names are static source names and are not persisted. The employee name and number continue to come from the authenticated session. If those values are unavailable, existing Angular rendering behavior remains unchanged.

## 7. Failure behavior and lifecycle

Theme variables load with the global stylesheet. If an individual decorative rule is unavailable, semantic HTML and existing Tailwind utilities still leave the page usable. No retry, storage, timer, request, or shutdown behavior changes.

At wide widths, a future sidebar becomes sticky within the viewport but must remain independently scrollable if its content exceeds the available height. At narrower widths it returns to normal document flow below the main content.

## 8. Security, privacy, and operations

Authorization, JWT handling, employee identity, and API boundaries do not change. The theme adds no external fonts, images, scripts, tracking, storage, network requests, or runtime dependencies. Bundle-size impact should be limited to generated CSS for the new utility classes and a small amount of component CSS.

## 9. Acceptance criteria

- The authenticated page has a polished green top bar and primarily white theme using `#119754` as a visible brand accent.
- Login, clock card, and time-log table use one consistent visual language with no remaining blue brand accents.
- Primary actions, focus rings, status indicators, errors, warnings, and destructive actions remain visually distinguishable.
- Desktop and 390-pixel views have no new clipping or page-level horizontal overflow.
- The time-log table keeps horizontal scrolling and table semantics on narrow screens.
- No chatbot placeholder is visible.
- Adding a `dashboard-sidebar` sibling activates a two-column wide-screen layout without restructuring the existing main column.
- Existing frontend tests and production build continue to pass.

## 10. Test approach

Run the existing Angular component tests to prove behavior did not change, then run the production build and formatting checks. Use a real browser with authenticated data at a wide desktop viewport and at 390 pixels. Verify the top bar, both clock states available during the test, timer legibility, action focus and hover treatment, table expansion, pagination, scroll containment, login styling, console output, and absence of an empty sidebar.

## 11. Risks and tradeoffs

- **Brand contrast:** Exact `#119754` cannot carry normal white text at AA contrast. Use `#08713e` for text-bearing green surfaces and keep `#119754` as the visible brand accent.
- **Too much green:** Repeating strong green on every control can reduce hierarchy. Limit solid green to the top bar, primary actions, and active or success states.
- **Future sidebar assumptions:** Chatbot width may change after product discovery. Start with a 22 to 24 rem optional column and keep the class contract easy to adjust.
- **Large table on mobile:** The table will still require horizontal scrolling. Preserve that behavior and make the surrounding card fit the viewport.

## 12. Open questions

- The chatbot's final width and whether it can collapse are not yet defined. This does not block the theme because the optional sidebar contract can be adjusted when chatbot behavior is designed.
- No logo asset has been provided. This does not block the theme; use a simple text brand and CSS-only accent rather than inventing a permanent logo.

## 13. Out of scope

- Chatbot UI, messages, prompts, actions, state, AI integration, or backend endpoints.
- Changes to authentication, clock behavior, timers, time logs, pagination, or API contracts.
- A custom font, external icon library, new image assets, dark mode, or user-selectable themes.
