using DynamicDave.Umbraco.ContentHealth.Indexing;
using DynamicDave.Umbraco.ContentHealth.Persistence;
using DynamicDave.Umbraco.ContentHealth.Status;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace DynamicDave.Umbraco.ContentHealth.Composers;

public class ContentHealthComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<ContentHealthOptions>().Bind(builder.Config.GetSection(Constants.OptionsSection));
        builder.Services.TryAddSingleton(TimeProvider.System);

        builder.Services.AddSingleton<ITargetStateSource, UmbracoTargetStateSource>();
        builder.Services.AddSingleton<TargetStatusResolver>();
        builder.Services.AddSingleton<ContentLinkReader>();
        builder.Services.AddSingleton<LinkIndexRepository>();
        builder.Services.AddSingleton<LinkIndexer>();
    }
}
