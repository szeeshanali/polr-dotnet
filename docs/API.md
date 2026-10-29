# Polr API v2

The API is compatible with Polr API v2, so existing Polr client libraries work unchanged.

All endpoints live under `/api/v2/`. Arguments can be sent in the query string, as a form body
(`application/x-www-form-urlencoded`) or as a JSON body (`application/json`).

## Authentication

Send your API key as the `key` argument with every request, for example `?key=YOUR_KEY`.

An admin turns on API access for a user in **Dashboard → Users**. The user then finds their key
in **Dashboard → API**. If `Polr:AnonymousApi` is on, requests without a key are allowed and are
limited per IP address.

## Quotas

Each user has a quota: the number of links they may create through the API per minute.
A negative quota means unlimited. When the quota is used up, every API call returns `429 QUOTA_EXCEEDED`.

## Response types

Set `response_type=json` for JSON. Without it, `shorten` and `lookup` answer in plain text, while
`shorten_bulk` and `data/link` always answer in JSON.

```json
{ "action": "shorten", "result": "https://sho.rt/5kq" }
```

Every API response has the header `Access-Control-Allow-Origin: *`.

## Endpoints

### `GET|POST /api/v2/action/shorten`

| Argument | Required | Description |
|---|---|---|
| `url` | yes | The http or https URL to shorten. URL-encode it. |
| `is_secret` | no | `true` for a secret link, which only opens with an extra key in the URL. |
| `custom_ending` | no | Letters, numbers, `-` and `_`. |

```
GET /api/v2/action/shorten?key=KEY&url=https%3A%2F%2Fexample.com&custom_ending=example
```

```json
{ "action": "shorten", "result": "https://sho.rt/example" }
```

Shortening a URL you already shortened (without `custom_ending` or `is_secret`) returns the existing link.

### `POST /api/v2/action/shorten_bulk`

| Argument | Required | Description |
|---|---|---|
| `data` | yes | A JSON **string** with a `links` array. Each item takes `url`, `is_secret` and `custom_ending`. |

```json
{
  "links": [
    { "url": "https://example.com/a" },
    { "url": "https://example.com/b", "is_secret": true },
    { "url": "https://example.com/c", "custom_ending": "see" }
  ]
}
```

```json
{
  "action": "shorten_bulk",
  "result": {
    "shortened_links": [
      { "long_url": "https://example.com/a", "short_url": "https://sho.rt/81" },
      { "long_url": "https://example.com/b", "short_url": "https://sho.rt/84/b496" },
      { "long_url": "https://example.com/c", "short_url": "https://sho.rt/see" }
    ]
  }
}
```

All URLs are checked before any link is created. If creating one of the links fails, the links before it are kept.

### `GET|POST /api/v2/action/lookup`

| Argument | Required | Description |
|---|---|---|
| `url_ending` | yes | The ending to look up, e.g. `5kq`. |
| `url_key` | for secret links | The secret key of a secret link. |

Plain text returns the long URL. JSON returns:

```json
{
  "action": "lookup",
  "result": {
    "long_url": "https://example.com",
    "created_at": { "date": "2026-02-12 15:20:34.000000", "timezone_type": 3, "timezone": "UTC" },
    "clicks": 42,
    "updated_at": { "date": "2026-02-12 15:20:34.000000", "timezone_type": 3, "timezone": "UTC" }
  }
}
```

The date format is kept for compatibility with existing Polr clients.

### `GET|POST /api/v2/data/link`

Click analytics for one link. Needs an API key (no anonymous access) and `Polr:AdvancedAnalytics`
turned on. Regular users can only read their own links; admins can read any link.

| Argument | Required | Description |
|---|---|---|
| `url_ending` | yes | The link's ending. |
| `stats_type` | yes | `day`, `country` or `referer`. |
| `left_bound` | no | Start date, e.g. `2026-02-28 22:41:43`. Defaults to now. |
| `right_bound` | no | End date. Defaults to now. |

The range may cover at most `Polr:AnalyticsMaxDays` days (365 by default).

```json
{
  "action": "data_link_day",
  "result": {
    "url_ending": "5gk",
    "data": [ { "x": "2026-03-10", "y": 42 }, { "x": "2026-03-11", "y": 1 } ]
  }
}
```

`country` and `referer` return `{ "label": "US", "clicks": 6 }` items. Clicks without a referrer are labelled `Direct`.

## Errors

Errors use HTTP status codes. With `response_type=json` the body is:

```json
{ "status_code": 400, "error_code": "CREATION_ERROR", "error": "This URL ending is already in use." }
```

Without it, the body is plain text such as `400 This URL ending is already in use.`

| Status | `error_code` | When |
|---|---|---|
| 400 | `MISSING_PARAMETERS` | A required argument is missing or invalid. |
| 400 | `INVALID_PARAMETERS` | `data` for `shorten_bulk` is not valid JSON. |
| 400 | `CREATION_ERROR` | The link could not be created, e.g. the custom ending is taken. |
| 400 | `ANALYTICS_ERROR` | The date range is invalid or too long. |
| 400 | `INVALID_ANALYTICS_TYPE` | `stats_type` is not `day`, `country` or `referer`. |
| 401 | `AUTH_ERROR` | The API key is missing or invalid, or anonymous access is not allowed. |
| 401 | `ACCESS_DENIED` | Wrong `url_key`, or the link belongs to someone else. |
| 401 | `JSON_ONLY` | The endpoint only answers in JSON. |
| 404 | `NOT_FOUND` | No link with that ending. |
| 429 | `QUOTA_EXCEEDED` | The per-minute quota is used up. |
