# DynamicDave.Umbraco.ContentHealth

Finds broken internal links in Umbraco content: links to pages and media that were deleted, are in the recycle bin, or are not published (in the language of the link, or because a parent page is not published).

- **Link health dashboard** in the Content section: the number of problems per kind, filters (problem, live or draft only, document type), sorting, paging and CSV export, plus the last scan and a Scan now button.
- **Link health view** on every document: the broken links in the page, and the pages that link to it, so you know what breaks before you unpublish or delete it.

Links are found in rich text, content pickers, multinode treepickers, multi URL pickers, media pickers and block list, block grid and single block content, and in third-party property editors that report their references to Umbraco (the same detection as Umbraco's "Referenced by").

## Install

    dotnet add package DynamicDave.Umbraco.ContentHealth

Supported Umbraco versions: **17.x and 18.x** (net10.0), tested on 17.0.0, 17.7.0 and 18.2.1. The backoffice UI is available in English, Dutch, German, French and Danish.

## How it works

The package keeps an index of all internal links in its own table (`dynamicDaveContentHealthLink`, created by a package migration). A page is checked again when it is saved, published, unpublished or moved. Links to a page or media item are checked again when that item is published, unpublished, moved, trashed, restored or deleted. A full scan runs shortly after installation and every night, to catch anything the events missed.

A link that is only in a page's unpublished draft is listed as "Draft only", so you can fix it before publishing.

Since Umbraco 17.2, Umbraco does not refresh its publish status after a page is restored from the recycle bin. The package reloads it for the restored page and its descendants, so links to it are reported as unpublished.

## Configuration

All settings are optional:

```json
{
  "DynamicDave": {
    "ContentHealth": {
      "NightlyScanTime": "03:00",
      "ExcludedDocumentTypes": [ "redirectPage" ],
      "ExcludedProperties": [ "legacyLinks" ],
      "ReportUnpublishedTargets": true
    }
  }
}
```

| Setting | Default | |
|---|---|---|
| `NightlyScanTime` | `"03:00"` | Server time of the nightly full scan (`HH:mm`). Empty disables it; saving, publishing and Scan now keep working. An invalid value disables it and logs a warning. |
| `ExcludedDocumentTypes` | `[]` | Document type aliases that are not scanned as a source. |
| `ExcludedProperties` | `[]` | Property aliases that are not scanned. |
| `ReportUnpublishedTargets` | `true` | `false`: links to unpublished pages are not reported, for sites that link ahead to pages published later. |

Changes to the exclusions apply from the next scan.

## Permissions

- The dashboard and the document view need access to the Content section. Lists and counts only include pages within the user's start nodes that the user's groups may browse, the same rule as the content tree. Pages that link to a document but that the user may not see are counted, not listed.
- The document view needs Browse access to the document.
- Scan now needs access to both the Content and the Settings section (the API requires both).

## Load balancing

Full scans and link status updates run on the scheduling server (or the single server). Saving a page on a subscriber server updates that page's own links there; the status of links to it is updated on the scheduling server a few seconds later.

## Limitations

- External links are not checked.
- Only documents are scanned as a source, not media, members or Umbraco 18 library elements.
- Only values without a segment are scanned.
- The location of a link is the property and language, not the block inside the property.
- When a page with descendants is moved to the recycle bin, the descendants' links are hidden right away and removed by the next full scan.
- The Link health tab shows no count badge.
- The Link health tab is not shown on a new document until it is saved.
- Read-only: links are not changed or repaired.

## License

MIT
