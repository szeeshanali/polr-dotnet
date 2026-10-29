# Polr.NET

[![CI](https://github.com/szeeshanali/polr-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/szeeshanali/polr-dotnet/actions/workflows/ci.yml)
[![License: GPL v2+](https://img.shields.io/badge/license-GPL--2.0--or--later-blue.svg)](LICENSE)

A fast, self-hosted link shortener for .NET 10 and MySQL.

## Features

- Short links with custom endings, counter-based endings (`5kq`) or random endings (`aZ3kd`).
- Secret links that only open with an extra key in the URL.
- Click counts, plus optional per-click analytics: clicks per day, countries (MaxMind GeoLite2) and referrers.
- User accounts: sign-up (optional), email activation, password reset, reCAPTCHA, email domain allow-list.
- Admin dashboard to manage links (edit, disable, delete) and users (roles, API access, quotas).
- [Polr API v2](docs/API.md), compatible with existing Polr client libraries.
- Works with existing Polr databases, so a Polr install can move over with its links and users.
- Built for speed: redirects come from an in-memory cache and clicks are written in batches in the background.
- Stateless app, so you can run several instances behind a load balancer.

## Quick start with Docker

```bash
git clone https://github.com/szeeshanali/polr-dotnet.git
cd polr-dotnet
docker compose up -d
```

Open http://localhost:8080 and sign in as `admin` with the password `change-me-please`.
Change the passwords in `docker-compose.yml` before putting it online.

## Running without Docker

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and MySQL 8 (or MariaDB 10.11+).

```sql
CREATE DATABASE polr;
CREATE USER 'polr'@'%' IDENTIFIED BY 'polr';
GRANT ALL ON polr.* TO 'polr'@'%';
```

```bash
cd src/Polr
dotnet user-secrets set "ConnectionStrings:Polr" "Server=localhost;Database=polr;User ID=polr;Password=polr"
dotnet user-secrets set "Polr:Admin:Username" "admin"
dotnet user-secrets set "Polr:Admin:Password" "change-me-please"
dotnet run
```

The app creates its tables on start and serves on http://localhost:5100.
In development, emails that would be sent (activation, password reset) are written to the console.

## Configuration

Settings live in [`appsettings.json`](src/Polr/appsettings.json) under `Polr`. Override any of them
with environment variables, using `__` for nesting: `Polr__AllowSignup=true`, `Polr__Smtp__Host=smtp.example.com`.

| Setting | Default | Polr `.env` | What it does |
|---|---|---|---|
| `ConnectionStrings:Polr` | local MySQL | `DB_*` | MySQL connection string. |
| `AppName` | `Polr` | `APP_NAME` | Name shown in the interface and emails. |
| `BaseUrl` | `http://localhost:8080` | `APP_PROTOCOL` + `APP_ADDRESS` | Public address used to build short links. |
| `Stylesheet` | Bootstrap 5 | `APP_STYLESHEET` | Bootstrap 5 stylesheet URL, e.g. a [Bootswatch](https://bootswatch.com) theme. |
| `PublicInterface` | `true` | `SETTING_PUBLIC_INTERFACE` | Show the shorten form to visitors who are not signed in. |
| `IndexRedirect` | | `SETTING_INDEX_REDIRECT` | Where anonymous visitors of `/` go when the public interface is off. |
| `Redirect404` | `false` | `SETTING_REDIRECT_404` | Send unknown and disabled links to `IndexRedirect`. |
| `ShortenRequiresLogin` | `false` | `SETTING_SHORTEN_PERMISSION` | Only signed-in users can shorten from the web form. |
| `AllowSignup` | `false` | `POLR_ALLOW_ACCT_CREATION` | Let visitors create accounts. |
| `RequireEmailActivation` | `false` | `POLR_ACCT_ACTIVATION` | New accounts must confirm their email. |
| `PasswordRecovery` | `false` | `SETTING_PASSWORD_RECOV` | Allow password resets by email. |
| `AllowedEmailDomains` | `[]` | `SETTING_ALLOWED_EMAIL_DOMAINS` | When set, only these email domains can sign up. |
| `AutoApiKey` | `false` | `SETTING_AUTO_API` | Give new users an API key. |
| `AnonymousApi` | `false` | `SETTING_ANON_API` | Allow API calls without a key. |
| `AnonymousApiQuota` | `5` | `SETTING_ANON_API_QUOTA` | Links per minute per IP for anonymous API calls. |
| `RandomEndings` | `false` | `SETTING_PSEUDORANDOM_ENDING` | Random endings instead of counter-based ones. |
| `RandomEndingLength` | `5` | `_PSEUDO_RANDOM_KEY_LENGTH` | Length of random endings. |
| `EndingBase` | `32` | `POLR_BASE` | `32` (lowercase) or `62` (mixed case) for counter-based endings. |
| `AdvancedAnalytics` | `false` | `SETTING_ADV_ANALYTICS` | Record each click and enable the stats pages. |
| `AnalyticsMaxDays` | `365` | `_ANALYTICS_MAX_DAYS_DIFF` | Longest date range for stats. |
| `GeoIpDatabasePath` | | `MAXMIND_LICENSE_KEY` | Path to `GeoLite2-Country.mmdb` for click countries. |
| `SecretKeyBytes` | `2` | `POLR_SECRET_BYTES` | Size of secret link keys (shown as hex). |
| `ApiKeyBytes` | `15` | `_API_KEY_LENGTH` | Size of API keys (shown as hex). |
| `LinkCacheSeconds` | `60` | | How long a link stays cached. Edits reach other instances within this time. |
| `ClickFlushMilliseconds` | `1000` | | How often buffered clicks are written to MySQL. |
| `DataProtectionKeysPath` | | | Folder for sign-in cookie keys. Share it between instances. |
| `Admin:Username` / `Email` / `Password` | | | Admin account created on first start if it does not exist. |
| `Smtp:Host` / `Port` / `Username` / `Password` / `FromAddress` / `FromName` | | `MAIL_*` | SMTP server for activation and reset emails. |
| `Recaptcha:SiteKey` / `SecretKey` | | `POLR_RECAPTCHA_*` | Turns on reCAPTCHA for sign-up when both are set. |

**Behind a reverse proxy** (nginx, Caddy, Traefik, a cloud load balancer), set
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so Polr.NET sees the real client IP and scheme.

**Country stats** need the free [MaxMind GeoLite2 Country](https://dev.maxmind.com/geoip/geolite2-free-geolocation-data)
database. Download it (or keep it updated with [geoipupdate](https://github.com/maxmind/geoipupdate))
and set `GeoIpDatabasePath`.

## Moving from Polr

1. Back up your Polr database.
2. Run [`docs/upgrade-from-polr.sql`](docs/upgrade-from-polr.sql) on it once.
3. Copy your `.env` settings to Polr.NET using the table above, and point `ConnectionStrings:Polr` at the database.
4. Start Polr.NET. Links, users, passwords, API keys and stats carry over, and short URLs stay the same.

Differences from Polr:

- Setup happens through configuration instead of a web wizard.
- Long URLs must be `http` or `https`.
- Endings are case-sensitive (useful with `EndingBase: 62`).
- Disabled links answer `410 Gone`; other errors use proper status codes.
- New passwords need at least 8 characters.
- The password reset form no longer reveals whether an email has an account.
- The admin tables are paged on the server; the world map on the stats page is replaced by a country list.

## How it works

- **Redirects** (`GET /{ending}`) do one indexed lookup, cached in memory for `LinkCacheSeconds`.
- **Clicks** go into an in-memory queue. A background service writes them every `ClickFlushMilliseconds`,
  one `UPDATE` per link and one multi-row `INSERT` per batch, so a redirect never waits for MySQL.
  If the process crashes, at most one flush interval of clicks is lost.
- **New endings** rely on the unique index: when two requests pick the same ending, the second simply takes the next one.
- **No server-side sessions**: sign-ins are cookies, so any instance can serve any request.
  Share `DataProtectionKeysPath` between instances.

```
src/Polr/
  Program.cs          app setup and routes
  PolrOptions.cs      all settings
  Data/               MySQL access (Dapper) and the schema in Data/Sql
  Services/           link creation, click recording, email, validation rules
  Endpoints/          redirects and the public API
  Pages/              the web interface (Razor Pages)
tests/Polr.Tests/     unit tests and integration tests against MySQL
```

## Development

```bash
dotnet build
dotnet test                                     # needs Docker for the MySQL integration tests
dotnet test --filter "Category!=Integration"    # unit tests only
```

See [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request.

## License and credits

Polr.NET is free software under the [GNU General Public License v2.0 or later](LICENSE).

Based on [Polr](https://github.com/cydrobolt/polr) by Chaoyi Zha and contributors (GPL-2.0-or-later).
