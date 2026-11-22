# Goldfish UI Style Guide

The two admin apps (`src/Goldfish.Web` and `src/Goldfish.PricingAdmin`) follow
the visual style of [usi.com](https://www.usi.com). The values below were read
from that site's stylesheets (`/dist/usi/main.*.min.css`, `/dist/core/main.*.min.css`)
on 2026-11-22. They are defined once as CSS custom properties at the top of
`src/Goldfish.Web/wwwroot/css/site.css` and
`src/Goldfish.PricingAdmin/wwwroot/app.css`; use the variables, not the raw hex
values.

## Color

| Token | Value | Use |
|---|---|---|
| `--brand-blue` | `#00529b` | Primary actions, links, brand name, focus ring |
| `--brand-blue-dark` | `#074c81` | Hover for links and primary buttons |
| `--brand-blue-deep` | `#003a6e` | Bottom edge of primary buttons |
| `--brand-orange` | `#ee8a1d` | Header rule, warning banner edge, quote rule (decoration only) |
| `--brand-orange-light` | `#f09635` | Reserved for orange hover states |
| `--brand-orange-dark` | `#aa6315` | Reserved for orange button edges |
| `--fg` | `#333333` | Body text and headings |
| `--muted` | `#666666` | Secondary text, table headers |
| `--line` / `--line-strong` | `#dedede` / `#cccccc` | Borders and rules |
| `--bg` | `#f7f7f7` | Page background, neutral button hover |
| `--bad` | `#bd2130` | Errors |

**Accessibility rule.** usi.com puts white text on the orange (`#ee8a1d`). That
pair has a contrast ratio of about 2.5:1, which fails WCAG 2.2 AA (4.5:1 for
text, 3:1 for UI parts). In these apps, orange is used only for decoration and
never behind text or as the only signal of state. Primary buttons use white on
`--brand-blue` (about 7.8:1). If an orange button is ever needed, use `--fg`
text on it (about 5.0:1), not white.

## Typography

*   Body: Figtree 400/600, falling back to Helvetica, Arial, sans-serif
    (`--font-body`). Base size 15px, line height 1.5.
*   Headings: Roboto 500/700, same fallbacks (`--font-heading`). `h1` 1.75rem,
    `h2` 1.375rem (usi.com's `h2` size; its 2.25rem `h1` is too large for
    dense admin pages).
*   Fonts load from Google Fonts in each app's layout. To avoid that external
    request, self-host the two families under `wwwroot` and remove the
    `fonts.googleapis.com` links.

## Components

*   **Buttons:** 5px corners (`--radius`), 8px by 18px padding, and a 3px
    bottom edge in a darker shade, as on usi.com. Default buttons are white
    with a gray edge; `.primary` is brand blue with a deep-blue edge.
*   **Header:** white, a 3px orange bottom rule and a soft shadow; the app name
    is in Roboto, brand blue.
*   **Cards, banners, tables:** white cards with `--line` borders and 5px
    corners. Banners carry a 4px colored left edge (blue notice, orange
    warning, red error). Table headers have a 2px rule.
*   **Tags and status pills:** fully rounded (`border-radius: 50px`).
*   **Focus:** every link and control shows a 3px brand-blue outline with a
    2px offset on keyboard focus.

## Not copied

The USI logo, imagery and page layout are not used. This guide covers color,
type and component shape only.
