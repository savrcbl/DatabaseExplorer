# Database Explorer

A lightweight, modern desktop tool for browsing and exporting data from SQL Server, PostgreSQL, and SQLite databases — without writing a query.

Built with WPF, MVVM (CommunityToolkit.Mvvm), and dependency injection (`Microsoft.Extensions.Hosting`).

## Features

- **Multi-engine support** — SQL Server, PostgreSQL, and SQLite behind a common provider abstraction.
- **Schema tree browsing** — connect and explore schemas, tables, views, and stored procedures.
- **Data grid with paging** — load a capped row count for quick previews, then "Load All" on demand.
- **Ad-hoc SQL editor** — run custom queries against the active connection.
- **Quick filter** — client-side, cross-column search over the currently loaded grid.
- **Export** — save any result set to CSV or Excel (`.xlsx`).
- **Saved connections** — store named connections locally, encrypted with Windows DPAPI (tied to your Windows user account; see [Security notes](#security-notes)).
- **Light/dark theming**, including native dark title bar support on Windows 10/11.
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
| PostgreSQL | `Host=localhost;Port=5432;Database=my_database;Username=postgres;Password=your_password;` |
| SQLite     | `Data Source=C:\path\to\database.db` |

> These are illustrative placeholders only — never commit a real connection string or password to source control. See [Security notes](#security-notes).

## Project structure

```
DatabaseExplorer/
├── Core/                 # Interfaces, models, and exceptions shared across the app
├── DataProviders/        # One folder per engine: SqlServer, PostgreSql, Sqlite
├── Database/             # Provider factory (selects the right IDatabaseProvider)
├── Services/             # Cross-cutting services: export, dialogs, theming, saved connections
├── ViewModels/           # MVVM view models
├── Views/                # WPF windows, controls, and dialogs
└── Themes/               # Light/dark resource dictionaries
```

Adding a new database engine means implementing `IDatabaseProvider`, `IDatabaseConnection`, and `IDatabaseQueryService` in a new `DataProviders/<Engine>` folder, then registering it in `App.xaml.cs`.

## Security notes

- All metadata and data queries are parameterized or use quoted/escaped identifiers — there is no known SQL injection path in the built-in browsing features. The **query editor runs whatever SQL you type**, exactly like SSMS or psql would; treat it with the same care.
- Saved connections (including passwords) are encrypted at rest with `ProtectedData.Protect` (Windows DPAPI, current-user scope). This is convenient local-machine protection, **not** a substitute for a real secrets manager — it won't protect the data if another process running as the same Windows user reads it, and saved connections don't roam between machines or users.
- CSV/Excel exports neutralize leading `=`, `+`, `-`, and `@` characters to prevent formula-injection when the exported file is later opened in Excel or Sheets.

If you find a security issue, please open an issue (or, for anything sensitive, contact the maintainer directly) rather than filing a public exploit write-up.

## Known limitations

- Windows-only (WPF + DPAPI).
- Saved-connection storage is per-Windows-user and doesn't sync across machines.
- No automated test suite yet — contributions welcome, particularly around the `DataProviders` layer.

## Contributing

Issues and pull requests are welcome. Please keep new provider code consistent with the existing pattern (parameterized queries, quoted identifiers, `DatabaseQueryException` wrapping).

## License

[MIT](LICENSE)
