# Quick Shell website (GitHub Pages)

Jekyll site published from the `/docs` folder.

## Enable GitHub Pages

1. Open **Settings → Pages** on [github.com/tonythethompson/QuickShell](https://github.com/tonythethompson/QuickShell)
2. **Build and deployment → Source:** Deploy from a branch
3. **Branch:** `master` (or `main`) · **Folder:** `/docs`
4. Save — the site will be at **https://tonythethompson.github.io/QuickShell/**

Use that URL for the Microsoft Store **privacy policy** and **website** fields if needed.

## Preview locally

Requires Ruby and Bundler:

```powershell
cd docs
bundle install
bundle exec jekyll serve
```

Open http://localhost:4000/QuickShell/
