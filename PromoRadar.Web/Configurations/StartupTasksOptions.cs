namespace PromoRadar.Web.Configurations;

public class StartupTasksOptions
{
    public bool ApplyMigrationsOnStartup { get; set; }

    public bool SeedReferenceDataOnStartup { get; set; }

    public bool SeedDemoDataOnStartup { get; set; }

    public bool ScheduleRecurringJobsOnStartup { get; set; }
}
