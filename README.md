# Theoria

A search engine for theology and philosophy. Type a query, get ranked results from the internet.

<img width="1919" height="1030" alt="image" src="https://github.com/user-attachments/assets/b766d36b-13b7-4c26-9c0e-c1fc9b1383dc" />

<img width="1916" height="941" alt="image" src="https://github.com/user-attachments/assets/8598cf1f-1627-4280-a222-d1b580e016a4" />

## Tech Stack

**Language:** C#, TypeScript, XAML  
**Runtime:** .NET 8, Node.js  
**Backend:** ASP.NET Core  
**Frontend:** React, Vite  
**Desktop:** WPF, WebView2 (Chromium)  

## Quick Start

### Prerequisites

- .NET 8 SDK
- Node.js

### Development for Web App

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

### Development for Desktop App

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
  Theoria.Engine     Core search engine
  Theoria.Shared     DTOs and interfaces
  Theoria.Api        ASP.NET Core API
  Theoria.Desktop    WPF desktop client
  Theoria.Web        React + Vite frontend
```
