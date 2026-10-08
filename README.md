# tcg-import

A small Windows app that sends Magic: The Gathering decks from [Archidekt](https://archidekt.com) to [TCG Arena](https://tcg-arena.fr).

1. Search Archidekt by deck name and/or username, or paste a deck link. **Filters** narrows by format, Commander
   bracket, colours (exact or including), commander, a card in the deck, and sort order.
2. Star decks to keep them under **Favorites** (saved between runs).
3. **Send to TCG Arena** opens TCG Arena's import page in your browser; click **Import** there to save the deck.

Sending the same deck again replaces the earlier copy in TCG Arena, so re-send after editing on Archidekt.

## Requirements

- Windows with the .NET 10 SDK.
- Only public or unlisted Archidekt decks.
- In the browser that opens, be signed in to TCG Arena and have **Magic the Gathering** added under Games.

## Run

```
dotnet run --project src/TcgImport.App
```

Tests: `dotnet test`

## How it works

- **Archidekt**: the app reads its undocumented JSON API (`/api/decks/v3/` for search, `/api/decks/{id}/` for a deck), so it may break if Archidekt changes it.
- **TCG Arena**: there's no write API. The app builds a link to its import page,
  `/import?game=Magic%20the%20Gathering&id=archidekt-{id}&name=...&deck=...`, where `deck` is the plain-text decklist
  encoded as `encodeURIComponent(btoa(decklist))`. The decklist has `Commander` and `Sideboard` headings; Maybeboard
  and other categories excluded on Archidekt are left out. Cards are matched by full name, so double-faced cards are
  sent as `Front // Back`.
- TCG Arena silently drops cards it doesn't recognise. The app shows the expected card count so you can compare it
  with **Total cards** on the import page.

## Settings

Favorites and the last username are stored in `%APPDATA%\TcgImport\state.json`.
Links open in the Windows default browser, or in the browser executable set as `browserPath` there.
