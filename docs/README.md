# Quick Shell website (GitHub Pages)

Jekyll site published from the `/docs` folder.

**Live site:** https://quickshell.trackdub.com

## Enable GitHub Pages

1. Open **Settings → Pages** on [github.com/tonythethompson/QuickShell](https://github.com/tonythethompson/QuickShell)
2. **Build and deployment → Source:** **GitHub Actions** (uses `.github/workflows/pages.yml`)
3. **Custom domain:** `quickshell.trackdub.com` (also set in `docs/CNAME`)
4. Enable **Enforce HTTPS** after DNS verifies

Use https://quickshell.trackdub.com/privacy/ for the Microsoft Store **privacy policy** URL.

## DNS (trackdub.com)

At your DNS provider, add:

| Type | Name | Value |
|------|------|--------|
| **CNAME** | `quickshell` | `tonythethompson.github.io` |

Propagation can take a few minutes up to 48 hours. GitHub Pages will show a green check when the domain is verified.

## Preview locally

Requires Ruby and Bundler:

```powershell
cd docs
bundle install
bundle exec jekyll serve
```

Open http://localhost:4000/
