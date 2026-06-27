---
layout: default
title: Get started
description: Create shortcuts, use home keywords, and manage Quick Shell from Command Palette.
---

# Get started

Open Command Palette, search **Quick Shell**, and press **Enter**.

## Create a shortcut

1. In Quick Shell, choose **Create shortcut** (or press **Ctrl+N**)
2. Enter a **name** and **folder path**
3. Optionally set a **command**, **terminal profile**, or **run as administrator**
4. Save

## Open a shortcut

- Search **Quick Shell**, pick a shortcut, press **Enter**
- Or type a **home keyword** at the Command Palette home screen (for example `api`) to jump there directly

## Useful shortcuts

| Action | How |
| --- | --- |
| Open the menu on a row | **Ctrl+K** or **⋯** |
| Run | **Enter** |
| Run as admin | **Ctrl+Enter** |
| Edit | **Ctrl+E** |
| Favorite | **Ctrl+F** |
| Undo / redo | **Ctrl+Z** / **Ctrl+Y** |
| Quick Shell settings | Settings row in the list, or **⋯** → **Quick Shell settings** |

## Settings

In **Quick Shell settings** you can:

- Choose your default **terminal application** and **profile**
- **Export** or **import** shortcuts (backup or another PC)
- **Refresh terminal list** after installing a new terminal

When importing, choose **Merge** to keep your shortcuts and add new ones, or **Replace all** to swap the whole file.

## Where data is stored

Shortcuts are saved on your PC at:

`%LOCALAPPDATA%\QuickShell\shortcuts.json`

Settings are saved at:

`%LOCALAPPDATA%\QuickShell\settings.json`

## More detail

The full README on GitHub has advanced options (JSON fields, section headers, building from source):

[github.com/tonythethompson/QuickShell](https://github.com/tonythethompson/QuickShell)
