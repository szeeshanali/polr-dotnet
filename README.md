# Polr.NET

[![CI](https://github.com/szeeshanali/polr-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/szeeshanali/polr-dotnet/actions/workflows/ci.yml)
[![License: GPL v2+](https://img.shields.io/badge/license-GPL--2.0--or--later-blue.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com)
[![MySQL](https://img.shields.io/badge/MySQL-8-4479A1.svg)](https://www.mysql.com)

A fast, self-hosted URL shortener for .NET 10 and MySQL. Create short, branded links on your own
domain, see who clicks them, and manage everything from a simple dashboard or a REST API.

## Why use it

- **Own your links and data.** Links and click stats stay on your server, not with a third-party
  shortener. No ads, no tracking scripts, no links that expire when a free plan ends.
- **Branded short links.** Use your own domain (`go.example.com/launch`) instead of a generic one,
  so people can see where a link comes from.
- **Know what works.** See clicks per day, countries and referrers for every link, so you can tell
  which campaign, post or email actually brought visitors.
- **Built for teams.** User accounts, admin and user roles, per-user API keys and quotas, and the
  option to turn off public sign-up for a private, internal shortener.
- **Easy for developers.** A small REST API with API keys and per-user quotas that answers in JSON or plain text.
- **Cheap to run, easy to scale.** One small container plus MySQL. The app keeps no server-side
  state, so you can add instances behind a load balancer when traffic grows.

## Who it's for

- **Businesses and creators** who want branded, trustworthy links in social posts, ads, QR codes and print.
- **Marketing teams** who need to track which channel or campaign drives traffic.
- **Developers** who need to create short links from their own apps, bots or scripts.
- **Companies with privacy or compliance rules** that require link data to stay in-house.

## Features

- Short links with custom endings, counter-based endings (`5kq`) or random endings (`aZ3kd`).
- Secret links that only open with an extra key in the URL.
- A QR code for every new short link.
- Click counts, plus optional per-click analytics: clicks per day, countries (MaxMind GeoLite2) and referrers.
- User accounts: sign-up (optional), email activation, password reset, reCAPTCHA, email domain allow-list.
- Admin dashboard to manage links (edit, disable, delete) and users (roles, API access, quotas).
- A [REST API](docs/API.md) with API keys and per-user quotas.
- Built for speed: redirects come from an in-memory cache and clicks are written in batches in the background.
- Stateless app, so you can run several instances behind a load balancer.
- Light and dark mode.

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
CREATE DATABASE polrnet;
CREATE USER 'polrnet'@'%' IDENTIFIED BY 'polrnet';
GRANT ALL ON polrnet.* TO 'polrnet'@'%';
```

```bash
cd src/PolrNet
dotnet user-secrets set "ConnectionStrings:PolrNet" "Server=localhost;Database=polrnet;User ID=polrnet;Password=polrnet"
dotnet user-secrets set "PolrNet:Admin:Username" "admin"
dotnet user-secrets set "PolrNet:Admin:Password" "change-me-please"
dotnet run
```

The app creates its tables on start and serves on http://localhost:5100.
In development, emails that would be sent (activation, password reset) are written to the console.

## Configuration

Settings live in [`appsettings.json`](src/PolrNet/appsettings.json) under `PolrNet`. Override any of them
with environment variables, using `__` for nesting: `PolrNet__AllowSignup=true`, `PolrNet__Smtp__Host=smtp.example.com`.

| Setting | Default | What it does |
|---|---|---|
| `ConnectionStrings:PolrNet` | local MySQL | MySQL connection string. |
| `AppName` | `Polr.NET` | Name shown in the interface and emails. |
| `BaseUrl` | `http://localhost:8080` | Public address used to build short links. |
| `Stylesheet` | Polr.NET theme | Bootstrap 5 stylesheet URL to use instead, e.g. a [Bootswatch](https://bootswatch.com) theme. |
| `PublicInterface` | `true` | Show the shorten form to visitors who are not signed in. |
| `IndexRedirect` | | Where anonymous visitors of `/` go when the public interface is off. |
| `Redirect404` | `false` | Send unknown and disabled links to `IndexRedirect`. |
| `ShortenRequiresLogin` | `false` | Only signed-in users can shorten from the web form. |
| `AllowSignup` | `false` | Let visitors create accounts. |
| `RequireEmailActivation` | `false` | New accounts must confirm their email. |
| `PasswordRecovery` | `false` | Allow password resets by email. |
| `AllowedEmailDomains` | `[]` | When set, only these email domains can sign up. |
| `AutoApiKey` | `false` | Give new users an API key. |
| `AnonymousApi` | `false` | Allow API calls without a key. |
| `AnonymousApiQuota` | `5` | Links per minute per IP for anonymous API calls. |
| `RandomEndings` | `false` | Random endings instead of counter-based ones. |
| `RandomEndingLength` | `5` | Length of random endings. |
| `EndingBase` | `32` | `32` (lowercase) or `62` (mixed case) for counter-based endings. |
| `AdvancedAnalytics` | `false` | Record each click and enable the stats pages. |
| `AnalyticsMaxDays` | `365` | Longest date range for stats. |
| `GeoIpDatabasePath` | | Path to `GeoLite2-Country.mmdb` for click countries. |
| `SecretKeyBytes` | `2` | Size of secret link keys (shown as hex). |
| `ApiKeyBytes` | `15` | Size of API keys (shown as hex). |
| `LinkCacheSeconds` | `60` | How long a link stays cached. Edits reach other instances within this time. |
| `ClickFlushMilliseconds` | `1000` | How often buffered clicks are written to MySQL. |
| `DataProtectionKeysPath` | | Folder for sign-in cookie keys. Share it between instances. |
| `Admin:Username` / `Email` / `Password` | | Admin account created on first start if it does not exist. |
| `Smtp:Host` / `Port` / `Username` / `Password` / `FromAddress` / `FromName` | | SMTP server for activation and reset emails. |
| `Recaptcha:SiteKey` / `SecretKey` | | Turns on reCAPTCHA for sign-up when both are set. |

**Behind a reverse proxy** (nginx, Caddy, Traefik, a cloud load balancer), set
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` so Polr.NET sees the real client IP and scheme.

**Country stats** need the free [MaxMind GeoLite2 Country](https://dev.maxmind.com/geoip/geolite2-free-geolocation-data)
database. Download it (or keep it updated with [geoipupdate](https://github.com/maxmind/geoipupdate))
and set `GeoIpDatabasePath`.

## How it works

- **Redirects** (`GET /{ending}`) do one indexed lookup, cached in memory for `LinkCacheSeconds`.
- **Clicks** go into an in-memory queue. A background service writes them every `ClickFlushMilliseconds`,
  one `UPDATE` per link and one multi-row `INSERT` per batch, so a redirect never waits for MySQL.
  If the process crashes, at most one flush interval of clicks is lost.
- **New endings** rely on the unique index: when two requests pick the same ending, the second simply takes the next one.
- **No server-side sessions**: sign-ins are cookies, so any instance can serve any request.
  Share `DataProtectionKeysPath` between instances.

```
src/PolrNet/
  Program.cs          app setup and routes
  PolrNetOptions.cs   all settings
  Data/               MySQL access (Dapper) and the schema in Data/Sql
  Services/           link creation, click recording, email, validation rules
  Endpoints/          redirects and the public API
  Pages/              the web interface (Razor Pages)
  wwwroot/            stylesheet and scripts
tests/PolrNet.Tests/  unit tests and integration tests against MySQL
```

## Moving from Polr 2.x

Polr.NET can take over the database of an existing [Polr](https://github.com/cydrobolt/polr) 2.x install.
Links, users, passwords, API keys and stats carry over, short URLs stay the same, and existing
Polr API clients keep working.

1. Back up the database.
2. Run [`docs/upgrade-from-polr.sql`](docs/upgrade-from-polr.sql) on it once.
3. Point `ConnectionStrings:PolrNet` at the database and carry your old `.env` settings over:

   | Old `.env` | Polr.NET setting |
   |---|---|
   | `APP_NAME`, `APP_STYLESHEET` | `AppName`, `Stylesheet` |
   | `APP_PROTOCOL` + `APP_ADDRESS` | `BaseUrl` |
   | `SETTING_PUBLIC_INTERFACE`, `SETTING_INDEX_REDIRECT`, `SETTING_REDIRECT_404` | `PublicInterface`, `IndexRedirect`, `Redirect404` |
   | `SETTING_SHORTEN_PERMISSION` | `ShortenRequiresLogin` |
   | `POLR_ALLOW_ACCT_CREATION`, `POLR_ACCT_ACTIVATION` | `AllowSignup`, `RequireEmailActivation` |
   | `SETTING_PASSWORD_RECOV`, `SETTING_ALLOWED_EMAIL_DOMAINS` | `PasswordRecovery`, `AllowedEmailDomains` |
   | `SETTING_AUTO_API`, `SETTING_ANON_API`, `SETTING_ANON_API_QUOTA` | `AutoApiKey`, `AnonymousApi`, `AnonymousApiQuota` |
   | `SETTING_PSEUDORANDOM_ENDING`, `_PSEUDO_RANDOM_KEY_LENGTH`, `POLR_BASE` | `RandomEndings`, `RandomEndingLength`, `EndingBase` |
   | `SETTING_ADV_ANALYTICS`, `_ANALYTICS_MAX_DAYS_DIFF` | `AdvancedAnalytics`, `AnalyticsMaxDays` |
   | `POLR_SECRET_BYTES`, `_API_KEY_LENGTH` | `SecretKeyBytes`, `ApiKeyBytes` |
   | `MAIL_*`, `POLR_RECAPTCHA_*` | `Smtp:*`, `Recaptcha:*` |

4. Start Polr.NET.

Things that work differently in Polr.NET:

- Setup happens through configuration instead of a web wizard.
- Long URLs must be `http` or `https`.
- Endings are case-sensitive (useful with `EndingBase: 62`).
- Disabled links answer `410 Gone`; other errors use proper status codes.
- New passwords need at least 8 characters.
- The password reset form does not reveal whether an email has an account.
- The admin tables are paged on the server, and the stats page lists countries instead of drawing a map.

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
