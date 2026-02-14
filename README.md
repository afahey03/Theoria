# Theoria

A search engine for theology and philosophy. Type a query, get ranked results from the internet.

## Quick Start

### Prerequisites

- .NET 8 SDK
- Node.js

### Run the Web App

```bash
# Terminal 1 — API
cd src/Theoria.Api
dotnet run

# Terminal 2 — React frontend
cd src/Theoria.Web
npm install
npm run dev
```

Open `http://localhost:3000` and search.

### Run the Desktop App

```bash
cd src/Theoria.Desktop
dotnet run
```

## How It Works

1. You type a query (e.g. "Aquinas natural law")
2. DuckDuckGo is scraped for relevant URLs
3. Each page is fetched and its text extracted
4. Content is scored with BM25 and ranked
5. Highlighted snippets are returned

Both the web and desktop apps share the same engine (`Theoria.Engine`), so results are identical.

## Project Structure

```
src/
  Theoria.Engine     Core search engine (tokenizer, index, BM25, snippets)
  Theoria.Shared     DTOs and interfaces
  Theoria.Api        ASP.NET Core API
  Theoria.Desktop    WPF desktop client
  Theoria.Web        React + Vite frontend
```

## License

[MIT](LICENSE)
