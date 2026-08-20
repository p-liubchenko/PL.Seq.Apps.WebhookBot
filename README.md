# PL.Seq.Apps.WebhookBot

A [Seq](https://datalust.co/seq) app that sends alert / event notifications to a
**Teams Webhook Bot**. The message body is rendered from a **Liquid** template
([Fluid](https://github.com/sebastienros/fluid)) with full access to the alert and
event context, then POSTed as `{"text": "..."}` — the payload the webhook bot expects:

```bash
curl -X POST -H 'Content-Type: application/json' \
  -d '{"text":"Hello World"}' \
  https://webhookbot.c-toss.com/api/bot/webhooks/<your-webhook-id>
```

> ### Disclaimer
> This is an **independent, unofficial** tool. It is **not affiliated with,
> endorsed by, or connected to CoinToss Inc.** (or the operators of
> `c-toss.com` / `webhookbot.c-toss.com`) in any way. I built it because I needed
> it for my own use. It simply POSTs JSON to whatever webhook URL you configure.
> It is provided as-is, with no warranty and no responsibility or liability to any
> third party — see [LICENSE](LICENSE.txt). Any product or company names are the
> property of their respective owners and are used for identification only.

## Requirements

- **Seq 2024.1 or later** — the plugin targets **`net8.0`**, which Seq's app host
  runs on. See [.NET target](#net-target) below if you are on an older Seq.

## Template context

Everything on the event is exposed to the template:

| Reference | What it is |
| --- | --- |
| `{{ service }}`, `{{ count }}`, ... | Each event property as a top-level variable (grouping dimensions and aggregates use the alias from the query) |
| `{{ properties.<name> }}` | Any property by name (also supports nested access, e.g. `{{ User.Id }}` / `{{ properties.User.Id }}`) |
| `{{ evt.Message }}` | The rendered event message |
| `{{ evt.MessageTemplate }}` | The raw message template |
| `{{ evt.Level }}` | Log level (e.g. `Error`) |
| `{{ evt.Exception }}` | Exception text, if any |
| `{{ evt.Timestamp }}` | Local timestamp |
| `{{ evt.TimestampUtc }}` | UTC timestamp |
| `{{ evt.Id }}` | Event id |
| `{{ evt.EventType }}` | Event type |

Full Liquid syntax is supported, so you can do conditionals and formatting:

```liquid
{% if count > 50 %}🚨{% else %}⚠️{% endif %} Error spike for {{ service }} ({{ count }} in the last minute)
```

## Settings

| Setting | Required | Scope | Description |
| --- | --- | --- | --- |
| **Webhook URL** | yes | app instance | Full webhook bot URL, e.g. `https://webhookbot.c-toss.com/api/bot/webhooks/<your-webhook-id>` |
| **Message template (Liquid)** | yes | per alert | The Liquid template rendered into the `text` field (see [Template context](#template-context)). Declared as an *invocation parameter*, so it appears in each alert's notification ("customize this notification") settings rather than on the app instance — one instance can serve many alerts, each with its own message. |
| **Debug logging** | no | app instance | Logs the available template variables and the rendered message — handy when writing a template for a new alert |

> The template is rendered by the app inside `OnAsync`, against the event Seq
> delivers when the alert fires — Seq does not pre-render it. This matches how
> Datalust's own apps (e.g. `Seq.App.EmailPlus`) work. Seq's "customize this
> notification" layer can add extra properties to that event, which then become
> available to the template.

## Wiring it to your alert

Given a signal / query such as:

```sql
select count(*) as count
from stream
where @Level = 'Error'
group by time(1m), ServiceName as service
having count > 10
limit 100
```

Create an **Alert** on that query and set its notification channel to a
**Webhook Bot Notifier** app instance. Then set the message template to:

```liquid
Error spike for {{ service }}
```

Because the query aliases the grouping as `... as service`, Seq exposes that
dimension on the alert event, and it is available as the top-level Liquid variable
`{{ service }}`. The aggregate `count` is likewise available as `{{ count }}`.

> **Tip:** if you are unsure what a property is called for a given alert, enable
> **Debug logging** on the app instance and check the Seq diagnostic log — it lists
> every available template variable for each event it receives.

If the template fails to render, or the webhook returns a non-success status, the
error is written to Seq's own diagnostic log rather than throwing.

## Build & package

A Seq app that targets a concrete framework (rather than `netstandard2.0`) must
ship its dependencies inside the package, so packaging is **publish → pack**, not
just `pack`:

```bash
dotnet build   PL.Seq.Apps.WebhookBot/PL.Seq.Apps.WebhookBot.csproj -c Release
dotnet publish PL.Seq.Apps.WebhookBot/PL.Seq.Apps.WebhookBot.csproj -c Release --no-build -o PL.Seq.Apps.WebhookBot/obj/publish
dotnet pack    PL.Seq.Apps.WebhookBot/PL.Seq.Apps.WebhookBot.csproj -c Release --no-build -o nupkg
```

The publish output is packed into `lib/net8.0` (see the `<None Include="./obj/publish/**">`
item in the `.csproj`). `Seq.Apps.dll` and `Serilog.dll` are excluded because the
Seq app host provides them. The result is `nupkg/PL.Seq.Apps.WebhookBot.1.0.0.nupkg`.

## Install in Seq

1. Put the `.nupkg` on a NuGet feed Seq can reach. Options:
   - Copy it into a local folder and add that folder under **Settings → Feeds** in Seq, or
   - Push it to your internal NuGet feed / nuget.org.
2. In Seq go to **Settings → Apps → Install from NuGet** and install
   `PL.Seq.Apps.WebhookBot`.
3. Create an **instance** of the app (Settings → Apps → Add instance) and set the
   **Webhook URL**.
4. On your alert, choose that instance as the notification and fill in the
   **Message template**.

## CI/CD

[`.github/workflows/main.yml`](.github/workflows/main.yml) builds, (optionally) tests,
publishes and pushes the package to nuget.org on pushes to `master`, `dev`,
`feature/**` and `release/**`:

- The package version is read from `<VersionPrefix>` in the `.csproj`.
- `dev` builds get a `-dev.<run_number>` suffix; `release/**` builds get `-rc.<run_number>`;
  `master` publishes the stable version.
- Pushes to nuget.org only happen on `master` / `dev` / `release/**`, and only when
  that exact version does not already exist.
- Requires a repository secret **`NUGET_API_KEY`**.

## .NET target

Seq loads app plugins on its **app-host runtime**, so a plugin must target a
framework **no newer** than that runtime — otherwise it silently fails to load.

| Target | Loads on | Notes |
| --- | --- | --- |
| `net8.0` *(this project)* | Seq 2024.1+ | Current Seq app host runs on .NET 8. |
| `net6.0` | Seq 2022.1+ | What Datalust ship for their own apps; maximum compatibility. |
| `netstandard2.0` | any Seq | Broadest, but no newer runtime APIs. |
| `net10.0` | — | **Does not load** — no shipping Seq app host runs on .NET 10. |

If your Seq server is older than 2024.1, change `<TargetFramework>` to `net6.0`
(and the workflow's `dotnet-version` to `6.0.x`).
