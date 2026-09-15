# Database Explorer

A lightweight, modern desktop tool for browsing and exporting data from SQL Server, PostgreSQL, SQLite, and Supabase — without writing a query.

Built with WPF, MVVM (CommunityToolkit.Mvvm), and dependency injection (`Microsoft.Extensions.Hosting`).

## Features

- **Multi-engine support** — SQL Server, PostgreSQL, SQLite, and Supabase behind a common provider abstraction. Supabase is Postgres under the hood, so it's also reachable through the PostgreSQL provider directly (see [Connecting to Supabase](#connecting-to-supabase)).
- **Schema tree browsing** — connect and explore schemas, tables, views, and stored procedures.
- **Data grid with paging** — load a capped row count for quick previews, then "Load All" on demand.
- **Ad-hoc SQL editor** — run custom queries against the active connection. On a Supabase connection this instead calls a Postgres function (RPC) by name, since Supabase is accessed over REST rather than a raw SQL socket — see [Connecting to Supabase](#connecting-to-supabase).
- **Quick filter** — client-side, cross-column search over the currently loaded grid.
- **Export** — save any result set to CSV or Excel (`.xlsx`).
- **Saved connections** — store named connections locally, encrypted with Windows DPAPI (tied to your Windows user account; see [Security notes](#security-notes)).
- **Light/dark toggle**, persisted between launches. There's no "follow system theme" mode — the app keeps whatever you last picked, including native dark title bar support on Windows 10/11.
- **Clipboard helpers** — copy a cell, row, or the whole table.

## Getting started

### Prerequisites

- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (the app targets `net10.0-windows`)

### Build & run

```bash
git clone https://github.com/J-Savier/DatabaseExplorer.git
cd DatabaseExplorer
dotnet build
dotnet run --project DatabaseExplorer
```

### Connecting

Pick a provider from the dropdown and enter a connection string. Examples:

| Provider   | Example connection string |
|------------|----------------------------|
| SQL Server | `Server=localhost;Database=MyDatabase;User Id=sa;Password=your_password;TrustServerCertificate=True;` |
| PostgreSQL | `Host=localhost;Port=5432;Database=my_database;Username=postgres;Password=your_password;` (also accepts a `postgresql://user:pass@host:port/db` URI, e.g. Supabase's connection string) |
| SQLite     | `Data Source=C:\path\to\database.db` |
| Supabase   | `Url=https://YOUR_PROJECT.supabase.co;ApiKey=YOUR_SECRET_KEY;` — see [Connecting to Supabase](#connecting-to-supabase) below, this one needs care |

> These are illustrative placeholders only — never commit a real connection string or password to source control. See [Security notes](#security-notes).

### Connecting to Supabase

Supabase can be reached two different ways, and they are **not equally safe**:

**Option A — PostgreSQL provider (recommended for this app).** Supabase's database is a real Postgres instance. In the Supabase dashboard, click **Connect**, copy the **Session pooler** connection string, and paste it directly into the connection string box with **PostgreSQL** selected as the provider — the app detects the `postgresql://` URI and converts it automatically. This gives full schema browsing through normal `pg_catalog` introspection, same as any other Postgres database, with no API-key considerations at all.

**Option B — Supabase provider (REST/PostgREST-based).** This talks to Supabase's auto-generated REST API instead of the database directly.

> [!WARNING]
> **The Supabase provider requires your project's *secret* key (`sb_secret_...`), not the publishable key.** Supabase deliberately blocks the publishable/anon key from listing tables and columns at the platform level — that's not a bug, it's how Supabase protects public-facing keys. The app will show a red inline warning if you paste a publishable key.
>
> The secret key is **not** a regular database password. It bypasses Row Level Security entirely and has full administrative access to every table in the project, for any request that includes it — anyone who obtains it can read or write anything, RLS policies or not. Treat it with the same seriousness as a root database credential:
> - Only use it on a machine you control and trust.
> - Never share a saved-connection export, screen recording, or screenshot that shows it.
> - If a secret key is ever exposed, roll it immediately from Settings > API Keys in the Supabase dashboard — old keys can't be "revoked" individually, only regenerated, which invalidates the previous one.
> - Saved connections are encrypted at rest via Windows DPAPI (see [Security notes](#security-notes)), which protects the key file on disk under your Windows account — it does **not** reduce what the key itself can do once decrypted in memory by this app.

With the Supabase provider, the query editor calls a Postgres function (RPC) by name instead of running raw SQL (PostgREST has no raw-SQL endpoint): type `my_function` or `my_function {"arg": 1}`. Functions must already exist in the database — see the Supabase SQL Editor to create one — and will appear under **Procedures** in the tree once they do.

## Project structure

```
DatabaseExplorer/
├── Core/                 # Interfaces, models, and exceptions shared across the app
├── DataProviders/        # One folder per engine: SqlServer, PostgreSql, Sqlite, Supabase
├── Database/             # Provider factory (selects the right IDatabaseProvider)
├── Services/             # Cross-cutting services: export, dialogs, theming, saved connections
├── ViewModels/           # MVVM view models
├── Views/                # WPF windows, controls, and dialogs
└── Themes/               # Light/dark resource dictionaries
```

Adding a new database engine means implementing `IDatabaseProvider`, `IDatabaseConnection`, and `IDatabaseQueryService` in a new `DataProviders/<Engine>` folder, then registering it in `App.xaml.cs`.

## Security notes

- All metadata and data queries are parameterized or use quoted/escaped identifiers — there is no known SQL injection path in the built-in browsing features. The **query editor runs whatever SQL you type**, exactly like SSMS or psql would; treat it with the same care.
- **Supabase is the one credential type in this app that operates above normal database-user permissions.** A Supabase secret key isn't scoped like a SQL Server or PostgreSQL login — it bypasses Row Level Security and can read/write every table in the project regardless of any policy. A leaked SQL Server password exposes one database with whatever permissions that login has; a leaked Supabase secret key exposes the entire project. See [Connecting to Supabase](#connecting-to-supabase) for handling guidance.
- Saved connections (including passwords) are encrypted at rest with `ProtectedData.Protect` (Windows DPAPI, current-user scope). This is convenient local-machine protection, **not** a substitute for a real secrets manager — it won't protect the data if another process running as the same Windows user reads it, and saved connections don't roam between machines or users.
- CSV/Excel exports neutralize leading `=`, `+`, `-`, and `@` characters to prevent formula-injection when the exported file is later opened in Excel or Sheets.

If you find a security issue, please open an issue (or, for anything sensitive, contact the maintainer directly) rather than filing a public exploit write-up.

## Known limitations

- Windows-only (WPF + DPAPI).
- Saved-connection storage is per-Windows-user and doesn't sync across machines.
- The Supabase provider only sees the `public` schema, can't distinguish views from tables (both come from the same PostgREST spec), and has no raw-SQL execution — use the PostgreSQL provider instead if you need any of these.
- No automated test suite yet — contributions welcome, particularly around the `DataProviders` layer.

## Contributing

Issues and pull requests are welcome. Please keep new provider code consistent with the existing pattern (parameterized queries, quoted identifiers, `DatabaseQueryException` wrapping).

## License

[MIT](LICENSE)
