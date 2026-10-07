using System.Text.Json;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace TestSite.Seeding;

/// <summary>
/// Seeds one example of every link problem, in every standard editor. Expected after a full scan (live/draft):
/// Missing 3/1, Trashed 2/0, Unpublished 3/0, plus 5 OK links; 10 documents indexed.
/// </summary>
public class TestContentSeeder(
    IRuntimeState runtimeState,
    IContentService contentService,
    IContentTypeService contentTypeService,
    IDataTypeService dataTypeService,
    IMediaService mediaService,
    ILanguageService languageService,
    PropertyEditorCollection propertyEditors,
    IConfigurationEditorJsonSerializer configurationSerializer,
    IShortStringHelper shortStringHelper,
    ILogger<TestContentSeeder> logger)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private static readonly Guid SuperUser = Constants.Security.SuperUserKey;

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (runtimeState.Level != RuntimeLevel.Run || contentService.GetRootContent().Any())
        {
            return; // not ready, or already seeded
        }

        logger.LogInformation("Seeding link health test content");
        await EnsureDutchAsync();
        var (linkPage, variantPage, linkBlock) = await CreateContentTypesAsync();

        // Media targets
        var logo = CreateMedia("Logo");
        var deletedMedia = CreateMedia("Deleted logo");
        mediaService.Delete(deletedMedia);
        var trashedMedia = CreateMedia("Trashed logo");
        mediaService.MoveToRecycleBin(trashedMedia);

        // Document targets
        var ok = CreatePage("Target ok", linkPage, publish: true);
        var unpublished = CreatePage("Target unpublished", linkPage, publish: false);
        var trashed = CreatePage("Target trashed", linkPage, publish: true);
        contentService.MoveToRecycleBin(trashed);
        var deleted = CreatePage("Target deleted", linkPage, publish: false);
        contentService.Delete(deleted);
        var hiddenParent = CreatePage("Hidden parent", linkPage, publish: true);
        var orphan = CreatePage("Orphan target", linkPage, publish: true, parent: hiddenParent);
        contentService.Unpublish(hiddenParent);
        var variantTarget = CreateVariant("Variant target", "Variant doel", variantPage, body: null);
        Publish(variantTarget, "en-US"); // not published in nl-NL

        // Sources
        CreatePage("Home", linkPage, publish: true, values: c =>
        {
            c.SetValue("body", RichText(ok.Key));         // OK
            c.SetValue("image", MediaPicker(logo.Key));   // OK
        });
        CreatePage("Broken links", linkPage, publish: true, values: c =>
        {
            c.SetValue("body", RichText(deleted.Key));                                  // Missing
            c.SetValue("picker", DocumentUdi(trashed.Key));                             // Trashed
            c.SetValue("links", MultiUrl(unpublished.Key));                             // Unpublished
            c.SetValue("pages", $"{DocumentUdi(orphan.Key)},{DocumentUdi(ok.Key)}");    // Unpublished (ancestor) + OK
            c.SetValue("image", MediaPicker(deletedMedia.Key));                         // Missing
            c.SetValue("blocks", BlockList(linkBlock.Key, deleted.Key));                // Missing
        });
        CreatePage("Media page", linkPage, publish: true, values: c => c.SetValue("image", MediaPicker(trashedMedia.Key))); // Trashed

        var variantSource = CreateVariant("Variant source", "Variant bron", variantPage, body: RichText(variantTarget.Key));
        Publish(variantSource, "*"); // nl-NL: Unpublished, en-US: OK

        var draftOnly = CreatePage("Draft only", linkPage, publish: true, values: c => c.SetValue("picker", DocumentUdi(ok.Key))); // live: OK
        draftOnly.SetValue("picker", DocumentUdi(deleted.Key)); // draft only: Missing
        contentService.Save(draftOnly);
    }

    private async Task EnsureDutchAsync()
    {
        if (await languageService.GetAsync("nl-NL") is null)
        {
            LogIfFailed("Create language nl-NL", await languageService.CreateAsync(new Language("nl-NL", "Dutch (Netherlands)"), SuperUser));
        }
    }

    private async Task<(IContentType LinkPage, IContentType VariantPage, IContentType LinkBlock)> CreateContentTypesAsync()
    {
        var richText = await RequiredDataTypeAsync(Constants.DataTypes.Guids.RichtextEditorGuid);
        var contentPicker = await RequiredDataTypeAsync(Constants.DataTypes.Guids.ContentPickerGuid);
        var mediaPicker = await RequiredDataTypeAsync(Constants.DataTypes.Guids.MediaPicker3Guid);
        var multiUrl = await CreateDataTypeAsync("Test Multi URL Picker", Constants.PropertyEditors.Aliases.MultiUrlPicker, new Dictionary<string, object>());
        var treePicker = await CreateDataTypeAsync("Test Multinode Treepicker", Constants.PropertyEditors.Aliases.MultiNodeTreePicker, new Dictionary<string, object>());

        var linkBlock = new ContentType(shortStringHelper, Constants.System.Root) { Alias = "linkBlock", Name = "Link block", IsElement = true };
        linkBlock.AddPropertyType(Property(contentPicker, "link", "Link"), "content", "Content");
        await CreateContentTypeAsync(linkBlock);

        var blocks = await CreateDataTypeAsync("Test Block List", Constants.PropertyEditors.Aliases.BlockList, new Dictionary<string, object>
        {
            ["blocks"] = new[] { new BlockListConfiguration.BlockConfiguration { ContentElementTypeKey = linkBlock.Key } },
        });

        var linkPage = new ContentType(shortStringHelper, Constants.System.Root) { Alias = "linkPage", Name = "Link page", AllowedAsRoot = true };
        linkPage.AddPropertyType(Property(richText, "body", "Body"), "content", "Content");
        linkPage.AddPropertyType(Property(contentPicker, "picker", "Picker"), "content", "Content");
        linkPage.AddPropertyType(Property(multiUrl, "links", "Links"), "content", "Content");
        linkPage.AddPropertyType(Property(treePicker, "pages", "Pages"), "content", "Content");
        linkPage.AddPropertyType(Property(mediaPicker, "image", "Image"), "content", "Content");
        linkPage.AddPropertyType(Property(blocks, "blocks", "Blocks"), "content", "Content");
        linkPage.AllowedContentTypes = [new ContentTypeSort(linkPage.Key, 0, linkPage.Alias)];
        await CreateContentTypeAsync(linkPage);

        var variantPage = new ContentType(shortStringHelper, Constants.System.Root)
        {
            Alias = "variantPage",
            Name = "Variant page",
            AllowedAsRoot = true,
            Variations = ContentVariation.Culture,
        };
        variantPage.AddPropertyType(Property(richText, "body", "Body", ContentVariation.Culture), "content", "Content");
        await CreateContentTypeAsync(variantPage);

        return (linkPage, variantPage, linkBlock);
    }

    private PropertyType Property(IDataType dataType, string alias, string name, ContentVariation variations = ContentVariation.Nothing)
        => new(shortStringHelper, dataType, alias) { Name = name, Variations = variations };

    private async Task<IDataType> RequiredDataTypeAsync(Guid key)
        => await dataTypeService.GetAsync(key) ?? throw new InvalidOperationException($"Default data type {key} not found");

    private async Task<IDataType> CreateDataTypeAsync(string name, string editorAlias, IDictionary<string, object> configuration)
    {
        if (!propertyEditors.TryGet(editorAlias, out var editor))
        {
            throw new InvalidOperationException($"Property editor {editorAlias} not found");
        }

        var dataType = new DataType(editor, configurationSerializer, Constants.System.Root)
        {
            Name = name,
            DatabaseType = ValueStorageType.Ntext,
            ConfigurationData = configuration,
        };
        LogIfFailed($"Create data type {name}", await dataTypeService.CreateAsync(dataType, SuperUser));
        return dataType;
    }

    private async Task CreateContentTypeAsync(IContentType type)
        => LogIfFailed($"Create content type {type.Alias}", await contentTypeService.CreateAsync(type, SuperUser));

    private IMedia CreateMedia(string name)
    {
        var media = mediaService.CreateMedia(name, Constants.System.Root, Constants.Conventions.MediaTypes.Image);
        mediaService.Save(media);
        return media;
    }

    private IContent CreatePage(string name, IContentType type, bool publish, IContent? parent = null, Action<IContent>? values = null)
    {
        var content = contentService.Create(name, parent?.Id ?? Constants.System.Root, type);
        values?.Invoke(content);
        contentService.Save(content);
        if (publish)
        {
            Publish(content, "*");
        }

        return content;
    }

    private IContent CreateVariant(string english, string dutch, IContentType type, string? body)
    {
        var content = contentService.Create(english, Constants.System.Root, type);
        content.SetCultureName(english, "en-US");
        content.SetCultureName(dutch, "nl-NL");
        if (body is not null)
        {
            content.SetValue("body", body, "en-US");
            content.SetValue("body", body, "nl-NL");
        }

        contentService.Save(content);
        return content;
    }

    private void Publish(IContent content, params string[] cultures)
    {
        var result = contentService.Publish(content, cultures);
        if (!result.Success)
        {
            logger.LogWarning("Publishing {Name} failed: {Result}", content.Name, result.Result);
        }
    }

    private void LogIfFailed(string action, dynamic attempt)
    {
        if (!attempt.Success)
        {
            logger.LogWarning("{Action} failed: {Status}", action, (object?)attempt.Status);
        }
    }

    private static string DocumentUdi(Guid key) => $"umb://document/{key:N}";

    private static string RichText(Guid documentKey) => JsonSerializer.Serialize(new
    {
        markup = $"<p>See <a href=\"/{{localLink:{documentKey}}}\" type=\"document\" title=\"Link\">this page</a>.</p>",
        blocks = (object?)null,
    });

    private static string MultiUrl(Guid documentKey) => JsonSerializer.Serialize(new[]
    {
        new { name = "Link", target = (string?)null, udi = DocumentUdi(documentKey), unique = documentKey, type = "document", url = (string?)null, queryString = (string?)null },
    });

    private static string MediaPicker(Guid mediaKey) => JsonSerializer.Serialize(new[]
    {
        new { key = Guid.NewGuid(), mediaKey, mediaTypeAlias = "Image", crops = Array.Empty<object>(), focalPoint = (object?)null },
    });

    private static string BlockList(Guid elementTypeKey, Guid linkedDocumentKey)
    {
        var contentKey = Guid.NewGuid();
        return JsonSerializer.Serialize(new
        {
            layout = new Dictionary<string, object> { ["Umbraco.BlockList"] = new[] { new { contentKey, settingsKey = (Guid?)null } } },
            contentData = new[]
            {
                new
                {
                    contentTypeKey = elementTypeKey,
                    key = contentKey,
                    values = new[]
                    {
                        new { alias = "link", culture = (string?)null, segment = (string?)null, editorAlias = Constants.PropertyEditors.Aliases.ContentPicker, value = DocumentUdi(linkedDocumentKey) },
                    },
                },
            },
            settingsData = Array.Empty<object>(),
            expose = new[] { new { contentKey, culture = (string?)null, segment = (string?)null } },
        });
    }
}
