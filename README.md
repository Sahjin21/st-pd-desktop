# st-pd-desktop

Georgia State Public Defender — Desktop Application (WPF / .NET 8)

## Overview

A Windows desktop application for case management, replacing the legacy Microsoft Access front-end.
Connects to a shared SQL Server Express database on the network — same data, modern UI, proper multi-user support.

## Tech Stack

- **WPF (.NET 8)** — Windows desktop UI
- **Entity Framework Core 8** — SQL Server Express via ODBC
- **CommunityToolkit.Mvvm** — MVVM pattern with source generators
- **SQL Server Express** — shared network database

## Project Structure

```
src/
├── PdTracker.Core/        # Domain entities, interfaces
├── PdTracker.Data/        # EF Core DbContext, migrations
├── PdTracker.Desktop/     # WPF app (Views, ViewModels, Resources)
├── PdTracker.sln           # Solution file
```

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (or connect to existing server)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (recommended) or VS Code + C# extension

### Database Setup

1. Create a SQL Server Express database named `PdTracker`
2. Update the connection string in `PdTracker.Desktop/appsettings.json`:
   ```
   Server=YOUR_SERVER\SQLEXPRESS;Database=PdTracker;Trusted_Connection=True;TrustServerCertificate=True
   ```
3. Run EF migrations to create tables, or point EF at the existing Access BE via linked tables

### Build & Run

```bash
cd src
dotnet restore
dotnet build
dotnet run --project PdTracker.Desktop
dotnet run --project src/PdTracker.Desktop/PdTracker.Desktop.csproj
```

### Build Installer

```bash
dotnet publish PdTracker.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Features

- **Search Defendant** — by name, SOID, or app number
- **New Application** — 8-tab intake wizard
- **Attorney List** — add/edit/search attorneys
- **Search Voucher** — by voucher # or defendant name
- **Defendant A–Z** — read-only spreadsheet view

## Status

Early development — shell scaffolded. UI forms and database connection working.
