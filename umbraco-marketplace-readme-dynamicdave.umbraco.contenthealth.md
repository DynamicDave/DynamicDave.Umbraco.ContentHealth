# Content Health – Broken Links

Find broken internal links before your visitors do. Content Health finds links to pages and media that were deleted, are in the recycle bin or are not published.

## Features

- **Link health dashboard** in the Content section: problems per kind, filters, sorting, paging and CSV export
- **Link health view** on every document: its broken links and the pages that link to it, so you know what breaks before you unpublish or delete
- Finds links in rich text, pickers, media pickers, block list, block grid and more
- Kept up to date on save and publish, with a nightly full scan
- Links that are only in an unpublished draft are shown as "Draft only"

## Compatibility

- Umbraco **17 and 18** (.NET 10)
- Backoffice UI in English, Dutch, German, French and Danish
- Free and open source (MIT)

## Getting started

```
dotnet add package DynamicDave.Umbraco.ContentHealth
```

The first scan runs shortly after the site starts. Full documentation and configuration: [README on GitHub](https://github.com/DynamicDave/DynamicDave.Umbraco.ContentHealth/blob/main/src/DynamicDave.Umbraco.ContentHealth/README.md).
