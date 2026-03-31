using System.Drawing;
using System.Text;
using System.Diagnostics;

namespace TrackerApp;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var applicationStartTickCount = Stopwatch.GetTimestamp();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => ShowFatalError(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var exception = args.ExceptionObject as Exception
                ?? new Exception(args.ExceptionObject?.ToString() ?? "Unknown unhandled exception.");
            ShowFatalError(exception);
        };

        try
        {
            File.AppendAllText(
                "C:\\CodexProjects\\Tracker\\artifacts\\program-args.log",
                $"[{DateTime.Now:HH:mm:ss}] args={string.Join(" | ", args)}{Environment.NewLine}");
            AppendProgramLog("Main entered");

            ToolStripManager.RenderMode = ToolStripManagerRenderMode.System;
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetDefaultFont(new Font("Microsoft Sans Serif", 8.25F, FontStyle.Regular, GraphicsUnit.Point));

            using var database = new AppDatabase();
            AppendProgramLog("AppDatabase created");
            database.Initialize();
            AppendProgramLog("AppDatabase initialized");

            TanakhVerificationRequest? verificationRequest = null;
            CoreVerificationRequest? coreVerificationRequest = null;
            ManagementVerificationRequest? managementVerificationRequest = null;
            ReviewVerificationRequest? reviewVerificationRequest = null;
            DailyWorkflowVerificationRequest? dailyWorkflowVerificationRequest = null;
            LibraryVerificationRequest? libraryVerificationRequest = null;
            TagFlowVerificationRequest? tagFlowVerificationRequest = null;
            BottomActionsVerificationRequest? bottomActionsVerificationRequest = null;
            ProductVerificationRequest? productVerificationRequest = null;
            StudyUnitVerificationRequest? studyUnitVerificationRequest = null;
            FreshStartVerificationRequest? freshStartVerificationRequest = null;
            MaintenanceVerificationRequest? maintenanceVerificationRequest = null;
            if (TryParseTanakhVerificationArgs(args, out var overviewScreenshotPath, out var detailScreenshotPath, out var reportPath))
            {
                verificationRequest = new TanakhVerificationRequest(
                    overviewScreenshotPath,
                    detailScreenshotPath,
                    reportPath);
            }
            else if (TryParseCoreVerificationArgs(args, out var searchScreenshotPath, out var tagsScreenshotPath, out var reviewScreenshotPath, out var treeScreenshotPath, out var coreReportPath))
            {
                coreVerificationRequest = new CoreVerificationRequest(
                    searchScreenshotPath,
                    tagsScreenshotPath,
                    reviewScreenshotPath,
                    treeScreenshotPath,
                    coreReportPath);
            }
            else if (TryParseManagementVerificationArgs(args, out var importScreenshotPath, out var dialogScreenshotPath, out var advancedSearchScreenshotPath, out var bulkScreenshotPath, out var managementReportPath))
            {
                managementVerificationRequest = new ManagementVerificationRequest(
                    importScreenshotPath,
                    dialogScreenshotPath,
                    advancedSearchScreenshotPath,
                    bulkScreenshotPath,
                    managementReportPath);
            }
            else if (TryParseReviewVerificationArgs(args, out var reviewSessionScreenshotPath, out var reviewResumeScreenshotPath, out var reviewReportPath))
            {
                reviewVerificationRequest = new ReviewVerificationRequest(
                    reviewSessionScreenshotPath,
                    reviewResumeScreenshotPath,
                    reviewReportPath);
            }
            else if (TryParseDailyWorkflowVerificationArgs(args, out var dashboardScreenshotPath, out var queuesScreenshotPath, out var weakSpotsScreenshotPath, out var dailyReportPath))
            {
                dailyWorkflowVerificationRequest = new DailyWorkflowVerificationRequest(
                    dashboardScreenshotPath,
                    queuesScreenshotPath,
                    weakSpotsScreenshotPath,
                    dailyReportPath);
            }
            else if (TryParseLibraryVerificationArgs(args, out var libraryScreenshotPath, out var libraryReportPath))
            {
                libraryVerificationRequest = new LibraryVerificationRequest(
                    libraryScreenshotPath,
                    libraryReportPath);
            }
            else if (TryParseTagFlowVerificationArgs(args, out var tagFlowScreenshotPath, out var tagFlowReportPath))
            {
                tagFlowVerificationRequest = new TagFlowVerificationRequest(
                    tagFlowScreenshotPath,
                    tagFlowReportPath);
            }
            else if (TryParseBottomActionsVerificationArgs(args, out var bottomCardScreenshotPath, out var bottomReviewScreenshotPath, out var bottomActionsReportPath))
            {
                bottomActionsVerificationRequest = new BottomActionsVerificationRequest(
                    bottomCardScreenshotPath,
                    bottomReviewScreenshotPath,
                    bottomActionsReportPath);
            }
            else if (TryParseProductVerificationArgs(args, out var productScreenshotPath, out var productReportPath))
            {
                productVerificationRequest = new ProductVerificationRequest(
                    productScreenshotPath,
                    productReportPath);
            }
            else if (TryParseStudyUnitVerificationArgs(args, out var unitMarkScreenshotPath, out var unitDashboardScreenshotPath, out var unitSessionScreenshotPath, out var unitSummaryScreenshotPath, out var unitReportPath))
            {
                studyUnitVerificationRequest = new StudyUnitVerificationRequest(
                    unitMarkScreenshotPath,
                    unitDashboardScreenshotPath,
                    unitSessionScreenshotPath,
                    unitSummaryScreenshotPath,
                    unitReportPath);
            }
            else if (TryParseFreshStartVerificationArgs(args, out var freshStartMainScreenshotPath, out var freshStartDashboardScreenshotPath, out var freshStartReportPath))
            {
                freshStartVerificationRequest = new FreshStartVerificationRequest(
                    freshStartMainScreenshotPath,
                    freshStartDashboardScreenshotPath,
                    freshStartReportPath);
            }
            else if (TryParseMaintenanceVerificationArgs(args, out var maintenanceScreenshotPath, out var maintenanceReportPath))
            {
                maintenanceVerificationRequest = new MaintenanceVerificationRequest(
                    maintenanceScreenshotPath,
                    maintenanceReportPath);
            }

            var form = new MainForm(database, verificationRequest, coreVerificationRequest, managementVerificationRequest, reviewVerificationRequest, dailyWorkflowVerificationRequest, libraryVerificationRequest, tagFlowVerificationRequest, bottomActionsVerificationRequest, productVerificationRequest, studyUnitVerificationRequest, freshStartVerificationRequest, maintenanceVerificationRequest, applicationStartTickCount);
            AppendProgramLog("MainForm created");

            Application.Run(form);
            AppendProgramLog("Application.Run finished");
        }
        catch (Exception exception)
        {
            AppendProgramLog($"Fatal exception: {exception}");
            ShowFatalError(exception);
        }
    }

    private static void AppendProgramLog(string message)
    {
        const string logPath = "C:\\CodexProjects\\Tracker\\artifacts\\program-trace.log";
        File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static void ShowFatalError(Exception exception)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Application Error");
        builder.AppendLine();
        builder.AppendLine(exception.Message);
        builder.AppendLine();
        builder.AppendLine(exception.StackTrace ?? "(no stack trace)");

        if (exception.InnerException is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Inner Exception:");
            builder.AppendLine(exception.InnerException.Message);
            builder.AppendLine();
            builder.AppendLine(exception.InnerException.StackTrace ?? "(no inner stack trace)");
        }

        MessageBox.Show(
            builder.ToString(),
            "TrackerApp Error",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        Environment.Exit(1);
    }

    private static bool TryParseTanakhVerificationArgs(
        IReadOnlyList<string> args,
        out string overviewScreenshotPath,
        out string detailScreenshotPath,
        out string reportPath)
    {
        overviewScreenshotPath = string.Empty;
        detailScreenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 4 || !string.Equals(args[0], "--tanakh-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        overviewScreenshotPath = args[1];
        detailScreenshotPath = args[2];
        reportPath = args[3];
        return true;
    }

    private static bool TryParseCoreVerificationArgs(
        IReadOnlyList<string> args,
        out string searchScreenshotPath,
        out string tagsScreenshotPath,
        out string reviewScreenshotPath,
        out string treeScreenshotPath,
        out string reportPath)
    {
        searchScreenshotPath = string.Empty;
        tagsScreenshotPath = string.Empty;
        reviewScreenshotPath = string.Empty;
        treeScreenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 6 || !string.Equals(args[0], "--core-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        searchScreenshotPath = args[1];
        tagsScreenshotPath = args[2];
        reviewScreenshotPath = args[3];
        treeScreenshotPath = args[4];
        reportPath = args[5];
        return true;
    }

    private static bool TryParseManagementVerificationArgs(
        IReadOnlyList<string> args,
        out string importScreenshotPath,
        out string dialogScreenshotPath,
        out string advancedSearchScreenshotPath,
        out string bulkScreenshotPath,
        out string reportPath)
    {
        importScreenshotPath = string.Empty;
        dialogScreenshotPath = string.Empty;
        advancedSearchScreenshotPath = string.Empty;
        bulkScreenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 6 || !string.Equals(args[0], "--management-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        importScreenshotPath = args[1];
        dialogScreenshotPath = args[2];
        advancedSearchScreenshotPath = args[3];
        bulkScreenshotPath = args[4];
        reportPath = args[5];
        return true;
    }

    private static bool TryParseReviewVerificationArgs(
        IReadOnlyList<string> args,
        out string reviewSessionScreenshotPath,
        out string reviewResumeScreenshotPath,
        out string reportPath)
    {
        reviewSessionScreenshotPath = string.Empty;
        reviewResumeScreenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 4 || !string.Equals(args[0], "--review-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        reviewSessionScreenshotPath = args[1];
        reviewResumeScreenshotPath = args[2];
        reportPath = args[3];
        return true;
    }

    private static bool TryParseDailyWorkflowVerificationArgs(
        IReadOnlyList<string> args,
        out string dashboardScreenshotPath,
        out string queuesScreenshotPath,
        out string weakSpotsScreenshotPath,
        out string reportPath)
    {
        dashboardScreenshotPath = string.Empty;
        queuesScreenshotPath = string.Empty;
        weakSpotsScreenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 5 || !string.Equals(args[0], "--daily-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        dashboardScreenshotPath = args[1];
        queuesScreenshotPath = args[2];
        weakSpotsScreenshotPath = args[3];
        reportPath = args[4];
        return true;
    }

    private static bool TryParseLibraryVerificationArgs(
        IReadOnlyList<string> args,
        out string screenshotPath,
        out string reportPath)
    {
        screenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 3 || !string.Equals(args[0], "--library-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        screenshotPath = args[1];
        reportPath = args[2];
        return true;
    }

    private static bool TryParseProductVerificationArgs(
        IReadOnlyList<string> args,
        out string screenshotPath,
        out string reportPath)
    {
        screenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 3 || !string.Equals(args[0], "--product-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        screenshotPath = args[1];
        reportPath = args[2];
        return true;
    }

    private static bool TryParseTagFlowVerificationArgs(
        IReadOnlyList<string> args,
        out string screenshotPath,
        out string reportPath)
    {
        screenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 3 || !string.Equals(args[0], "--tag-flow-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        screenshotPath = args[1];
        reportPath = args[2];
        return true;
    }

    private static bool TryParseBottomActionsVerificationArgs(
        IReadOnlyList<string> args,
        out string cardScreenshotPath,
        out string reviewScreenshotPath,
        out string reportPath)
    {
        cardScreenshotPath = string.Empty;
        reviewScreenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 4 || !string.Equals(args[0], "--bottom-actions-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        cardScreenshotPath = args[1];
        reviewScreenshotPath = args[2];
        reportPath = args[3];
        return true;
    }

    private static bool TryParseStudyUnitVerificationArgs(
        IReadOnlyList<string> args,
        out string markScreenshotPath,
        out string dashboardScreenshotPath,
        out string sessionScreenshotPath,
        out string summaryScreenshotPath,
        out string reportPath)
    {
        markScreenshotPath = string.Empty;
        dashboardScreenshotPath = string.Empty;
        sessionScreenshotPath = string.Empty;
        summaryScreenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 6 || !string.Equals(args[0], "--study-unit-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        markScreenshotPath = args[1];
        dashboardScreenshotPath = args[2];
        sessionScreenshotPath = args[3];
        summaryScreenshotPath = args[4];
        reportPath = args[5];
        return true;
    }

    private static bool TryParseFreshStartVerificationArgs(
        IReadOnlyList<string> args,
        out string mainScreenshotPath,
        out string dashboardScreenshotPath,
        out string reportPath)
    {
        mainScreenshotPath = string.Empty;
        dashboardScreenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 4 || !string.Equals(args[0], "--fresh-start-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        mainScreenshotPath = args[1];
        dashboardScreenshotPath = args[2];
        reportPath = args[3];
        return true;
    }

    private static bool TryParseMaintenanceVerificationArgs(
        IReadOnlyList<string> args,
        out string screenshotPath,
        out string reportPath)
    {
        screenshotPath = string.Empty;
        reportPath = string.Empty;

        if (args.Count != 3 || !string.Equals(args[0], "--maintenance-verify", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        screenshotPath = args[1];
        reportPath = args[2];
        return true;
    }
}

public sealed record TanakhVerificationRequest(
    string OverviewScreenshotPath,
    string DetailScreenshotPath,
    string ReportPath);

public sealed record CoreVerificationRequest(
    string SearchScreenshotPath,
    string TagsScreenshotPath,
    string ReviewScreenshotPath,
    string TreeScreenshotPath,
    string ReportPath);

public sealed record ManagementVerificationRequest(
    string ImportScreenshotPath,
    string CardDialogScreenshotPath,
    string AdvancedSearchScreenshotPath,
    string BulkScreenshotPath,
    string ReportPath);

public sealed record ReviewVerificationRequest(
    string SessionScreenshotPath,
    string ResumeScreenshotPath,
    string ReportPath);

public sealed record DailyWorkflowVerificationRequest(
    string DashboardScreenshotPath,
    string QueuesScreenshotPath,
    string WeakSpotsScreenshotPath,
    string ReportPath);

public sealed record LibraryVerificationRequest(
    string ScreenshotPath,
    string ReportPath);

public sealed record TagFlowVerificationRequest(
    string ScreenshotPath,
    string ReportPath);

public sealed record BottomActionsVerificationRequest(
    string CardScreenshotPath,
    string ReviewScreenshotPath,
    string ReportPath);

public sealed record ProductVerificationRequest(
    string ScreenshotPath,
    string ReportPath);

public sealed record StudyUnitVerificationRequest(
    string MarkScreenshotPath,
    string DashboardScreenshotPath,
    string SessionScreenshotPath,
    string SummaryScreenshotPath,
    string ReportPath);

public sealed record FreshStartVerificationRequest(
    string MainScreenshotPath,
    string DashboardScreenshotPath,
    string ReportPath);

public sealed record MaintenanceVerificationRequest(
    string ScreenshotPath,
    string ReportPath);
