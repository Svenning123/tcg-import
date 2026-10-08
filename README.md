# tcg-import

A small Windows app that sends Magic: The Gathering decks from [Archidekt](https://archidekt.com) to [TCG Arena](https://tcg-arena.fr).

1. Search Archidekt by deck name and/or username, or paste a deck link. **Filters** narrows by format (Commander by
   default), Commander bracket, colours (exact or including), commander, a card in the deck, and sort order. The
   commander and card boxes suggest card names as you type.
2. Star decks to keep them under **Favorites** (saved between runs).
3. Or sign in on the **Archidekt account** tab to list your own decks (private ones too) and your bookmarks.
4. **Send to TCG Arena** opens TCG Arena's import page in your browser; click **Import** there to save the deck.

Sending the same deck again replaces the earlier copy in TCG Arena, so re-send after editing on Archidekt.

The **Printings** tab controls card art:

- **Keep Archidekt card art** (on by default): each card keeps the printing chosen on Archidekt, so change a card's
  art on Archidekt and re-send.
- **Blocked sets**: cards from a blocked set (e.g. FIN) get another printing instead: the closest older one, or else
  the closest newer one, preferring regular paper sets over promos, collectible extras and digital-only sets.
  Blocking a set can include its related sets (for FIN: its Commander decks, promos, tokens, art series and so on).
  A card that only exists in blocked sets keeps its printing. Blocking also applies with card art off, when TCG
  Arena's own pick would be from a blocked set.

## Install

Run `TcgImport-Setup-<version>.exe` (see [Building the installer](#building-the-installer)). It installs for the
current user without admin rights and includes .NET, so nothing else is needed. The installer isn't code-signed, so
Windows SmartScreen may warn about it: choose **More info → Run anyway**.

In the browser that opens, be signed in to TCG Arena and have **Magic the Gathering** added under Games.

## Develop

Needs the .NET 10 SDK.

```
dotnet run --project src/TcgImport.App
dotnet test
```

### Building the installer

Install [Inno Setup 6](https://jrsoftware.org/isinfo.php) once (`winget install --id JRSoftware.InnoSetup -e --scope user`),
then run `.\build-installer.ps1`. It writes `artifacts\TcgImport-Setup-<version>.exe`; the version comes from
`src/TcgImport.App/TcgImport.App.csproj`.

## How it works

- **Archidekt**: the app reads its undocumented JSON API (`/api/decks/v3/` for search, `/api/decks/{id}/` for a deck),
  so it may break if Archidekt changes it. Signing in uses `/api/rest-auth/login/`; the password is never stored, only
  Archidekt's sign-in token, encrypted with Windows DPAPI for the current user.
- **TCG Arena**: there's no write API. The app builds a link to its import page,
  `/import?game=Magic%20the%20Gathering&id=archidekt-{id}&name=...&deck=...`, where `deck` is a plain-text decklist
  encoded as `encodeURIComponent(btoa(decklist))`. The decklist has `Commander` and `Sideboard` headings; Maybeboard
  and other categories excluded on Archidekt are left out.
- **Card art**: TCG Arena's importer accepts a TCG Arena card id instead of a card name. The app downloads TCG Arena's
  MTG card data (about 11 MB, re-checked every 12 hours) and maps each Archidekt printing (a Scryfall id) to TCG Arena's
  id for that printing. Cards it can't map, or all cards with the option off, are sent by full name
  (double-faced cards as `Front // Back`) and TCG Arena picks the art.
- **Blocked sets** use Scryfall's set list (`api.scryfall.com/sets`, cached for a week) for release dates and
  related sets. Promo sets span many years under one date, which is why they rank below regular sets.
- **Card name suggestions** use the search archidekt.com uses in its own filters (`/api/cards/v2/?nameSearch=...`).
- TCG Arena silently drops cards it doesn't recognise. The app shows the expected card count so you can compare it
  with **Total cards** on the import page.

## Data

- `%APPDATA%\TcgImport\state.json`: favorites, blocked sets, settings, and the encrypted Archidekt sign-in.
  Links open in the Windows default browser, or in the browser executable set as `browserPath` there.
- `%LOCALAPPDATA%\TcgImport\`: cached index of TCG Arena's card data and Scryfall's set list.

## Credits

Mana symbols come from the [Mana](https://github.com/andrewgioia/mana) font by Andrew Gioia (SIL OFL 1.1, see
`src/TcgImport.App/Assets/Fonts/OFL.txt`). The symbols themselves are © Wizards of the Coast. TCG Import is not
affiliated with Archidekt, TCG Arena, Scryfall or Wizards of the Coast.
