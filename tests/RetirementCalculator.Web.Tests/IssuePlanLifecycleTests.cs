using System.Diagnostics;

namespace RetirementCalculator.Web.Tests;

[TestClass]
public sealed class IssuePlanLifecycleTests
{
    [TestMethod]
    [DataRow("planning")]
    [DataRow("feedback")]
    [DataRow("noPlanFeedback")]
    [DataRow("stalePlan")]
    [DataRow("staleSource")]
    [DataRow("untrustedPlan")]
    [DataRow("parentOnly")]
    [DataRow("notPullRequest")]
    [DataRow("removedReadiness")]
    [DataRow("nonWriter")]
    [DataRow("wrongToken")]
    [DataRow("nonDefaultBranch")]
    [DataRow("readyBot")]
    [DataRow("injectedTarget")]
    [DataRow("invalidGraph")]
    [DataRow("decomposition")]
    [DataRow("lostCreateResponse")]
    [DataRow("attachFailure")]
    [DataRow("closedChildReplay")]
    [DataRow("frozenPlan")]
    [DataRow("duplicateChild")]
    [DataRow("removedChildMarker")]
    [DataRow("wrongRelationship")]
    [DataRow("bootstrap")]
    [DataRow("lostPrResponse")]
    [DataRow("lostBranchResponse")]
    [DataRow("lostContractResponse")]
    [DataRow("untrustedBranch")]
    [DataRow("unrelatedBranch")]
    [DataRow("labelFailure")]
    [DataRow("uncertainDispatch")]
    [DataRow("rejectedDispatch")]
    [DataRow("closedPr")]
    [DataRow("commentCollision")]
    [DataRow("removedOptIn")]
    [DataRow("changedContract")]
    [DataRow("changedParent")]
    [DataRow("removedParentApproval")]
    [DataRow("staleSourceAtPublication")]
    [DataRow("legacyContract")]
    [DataRow("dependencies")]
    [DataRow("independentParallel")]
    [DataRow("reviewTarget")]
    [DataRow("multipleOutputs")]
    [DataRow("workflowWiring")]
    public async Task Lifecycle_EnforcesAuthorizationAndRecovery(string scenario)
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "RetirementCalculator.slnx")))
        {
            repository = repository.Parent;
        }

        Assert.IsNotNull(repository);
        var temporaryDirectory = Directory.CreateTempSubdirectory("issue-plan-lifecycle-");
        try
        {
            var startInfo = new ProcessStartInfo("node")
            {
                WorkingDirectory = temporaryDirectory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(Path.Combine(repository.FullName, "tests", "RetirementCalculator.Web.Tests",
                "Fixtures", "issue-plan-lifecycle.cjs"));
            startInfo.ArgumentList.Add(scenario);
            startInfo.Environment["LIFECYCLE_HELPER"] = Path.Combine(repository.FullName, ".github", "scripts", "issue-plan-lifecycle.cjs");
            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail($"Lifecycle scenario {scenario} exceeded 30 seconds.");
            }

            Assert.AreEqual(0, process.ExitCode, await output + await errors);
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }
}
