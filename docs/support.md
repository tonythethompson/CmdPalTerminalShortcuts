---
layout: default
title: Support
description: Contact, bug reports, and feature requests for Quick Shell.
---

# Support

## Email

For questions that are not a good fit for a public GitHub issue, email:

**[{{ site.author.email }}](mailto:{{ site.author.email }})**

Please include your Windows version, PowerToys version, and Quick Shell version if you know them.

## GitHub Issues (bugs and feature requests)

The fastest way to report bugs or suggest features is a GitHub issue:

**[github.com/tonythethompson/QuickShell/issues](https://github.com/tonythethompson/QuickShell/issues)**

### Report a bug

1. Check [existing issues](https://github.com/tonythethompson/QuickShell/issues) first
2. Click **New issue**
3. Choose **Bug report** if a template is available, or use a plain issue
4. Include:
   - What you expected to happen
   - What actually happened
   - Steps to reproduce
   - Windows version (for example Windows 11 24H2)
   - PowerToys version
   - How you installed Quick Shell (Store, WinGet, or GitHub release)
   - Any error messages or screenshots

### Request a feature

1. Open a **New issue**
2. Describe the problem you are trying to solve and how you would use the feature
3. Mention if you would be willing to test a preview build

### Privacy-related requests

For privacy questions, you can email [{{ site.author.email }}](mailto:{{ site.author.email }}) or open an issue. See the [Privacy policy]({{ '/privacy/' | relative_url }}) for what data Quick Shell stores.

## Troubleshooting

**Extension does not appear after install**

Run **Reload Command Palette Extension** in Command Palette. Confirm Command Palette is enabled in PowerToys settings.

**Shortcuts missing after an update**

Check for a backup at `%LOCALAPPDATA%\QuickShell\shortcuts.json.bak`.

**Terminal list is empty or outdated**

Open **Quick Shell settings** → **Refresh terminal list**, or use the refresh button in the shortcut editor.

**Duplicate Quick Shell entries in Settings → Apps**

Uninstall older copies and keep a single install (Store, WinGet, or local MSIX — not multiple).
