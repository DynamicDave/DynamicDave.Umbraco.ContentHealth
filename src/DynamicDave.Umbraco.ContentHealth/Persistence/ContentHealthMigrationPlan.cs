using Umbraco.Cms.Core.Packaging;
using Umbraco.Cms.Infrastructure.Migrations;

namespace DynamicDave.Umbraco.ContentHealth.Persistence;

public class ContentHealthMigrationPlan : PackageMigrationPlan
{
    public ContentHealthMigrationPlan()
        : base(Constants.PackageName)
    {
    }

    protected override void DefinePlan() => To<CreateLinkTable>("dynamicdave-contenthealth-1-link-table");
}

// AsyncMigrationBase exists in Umbraco 17 and 18; MigrationBase was removed in 18.
public class CreateLinkTable(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists(LinkDto.TableName))
        {
            Create.Table<LinkDto>().Do();
        }

        return Task.CompletedTask;
    }
}
