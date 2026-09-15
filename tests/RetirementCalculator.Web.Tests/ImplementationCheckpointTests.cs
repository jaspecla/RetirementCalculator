using System.Diagnostics;

namespace RetirementCalculator.Web.Tests;

[TestClass]
public sealed class ImplementationCheckpointTests
{
    private const string Fixture = """
        const assert = require('node:assert/strict');
        const fs = require('node:fs');
        const path = require('node:path');
        const { execFileSync } = require('node:child_process');
        const helper = require(process.env.CHECKPOINT_HELPER);
        const root = path.join(process.cwd(), 'sandbox');
        const repository = path.join(process.cwd(), 'repository');
        fs.mkdirSync(repository);
        process.chdir(repository);
        const git = (...args) => execFileSync('git', args, { encoding: 'utf8' });
        git('init', '--initial-branch=main');
        git('config', 'core.autocrlf', 'false');
        fs.writeFileSync('tracked.txt', 'original\n');
        fs.writeFileSync('deleted.txt', 'delete me\n');
        fs.writeFileSync('renamed.txt', 'rename me\n');
        git('add', '--all');
        git('-c', 'user.name=Checkpoint Test', '-c', 'user.email=test@example.invalid', 'commit', '-m', 'Fixture');
        const headSha = git('rev-parse', 'HEAD').trim();
        const pull = { state: 'open', merged: false, head: { sha: headSha, repo: { full_name: 'owner/repo' } }, labels: [{ name: 'plan_accepted' }] };
        const plan = { id: 42, body: 'Accepted plan for issue #1' };
        let reviews = [];
        let reruns = 0;
        const run = { id: 100, path: '.github/workflows/implement-accepted-plan.lock.yml', event: 'workflow_dispatch', status: 'completed', conclusion: 'success', run_attempt: 1 };
        const jobs = [{ name: 'agent', steps: [{ name: 'Execute GitHub Copilot CLI', conclusion: 'failure', started_at: '2026-09-15T10:00:00Z', completed_at: '2026-09-15T11:00:00Z' }] }];
        const github = {
          rest: {
            pulls: { get: async () => ({ data: pull }), listReviews: 'reviews' },
            issues: { listComments: async () => ({ data: [plan] }) },
            actions: {
              getWorkflowRun: async () => ({ data: run }), listJobsForWorkflowRunAttempt: 'jobs',
              reRunWorkflow: async () => { reruns++; run.status = 'queued'; }
            }
          },
          paginate: async method => method === 'reviews' ? reviews : jobs
        };
        const context = { repo: { owner: 'owner', repo: 'repo' }, runId: 100, payload: { inputs: { pull_request_number: '7' }, workflow_run: { id: 100, run_attempt: 1 } } };
        const core = { setOutput() {}, info() {}, summary: { addRaw() { return this; }, async write() {} } };
        const options = { github, context, core, root };
        const progressFile = path.join(root, 'checkpoint-progress.json');
        const snapshotFile = path.join(root, 'implementation-snapshot.json');
        const read = file => JSON.parse(fs.readFileSync(file, 'utf8'));
        const saveForRestore = () => {
          helper.save(options);
          fs.mkdirSync(path.join(process.env.RUNNER_TEMP, 'implementation-restore'), { recursive: true });
          fs.copyFileSync(path.join(process.env.RUNNER_TEMP, 'implementation-upload', 'checkpoint.json'), path.join(process.env.RUNNER_TEMP, 'implementation-restore', 'checkpoint.json'));
        };
        """;

    [TestMethod]
    public void Snapshot_RoundTripsModifiedDeletedRenamedAndNewBinaryFiles()
    {
        RunNode("""
            await helper.prepare(options);
            fs.writeFileSync('tracked.txt', 'changed\n');
            fs.unlinkSync('deleted.txt');
            fs.renameSync('renamed.txt', 'moved.txt');
            const binary = Buffer.from([0, 255, 128, 13, 10, 0]);
            fs.writeFileSync('new.bin', binary);
            const progress = { completed: ['item 1'], remaining: ['item 2'], validation: ['focused tests passed'], notes: 'initial implementation' };
            fs.writeFileSync(progressFile, JSON.stringify(progress));
            helper.snapshot(progressFile, 'continue', root);
            saveForRestore();
            git('reset', '--hard', 'HEAD');
            git('clean', '-fd');
            process.env.GITHUB_RUN_ATTEMPT = '2';
            await helper.prepare(options);
            assert.equal(fs.readFileSync('tracked.txt', 'utf8'), 'changed\n');
            assert.equal(fs.existsSync('deleted.txt'), false);
            assert.equal(fs.existsSync('renamed.txt'), false);
            assert.equal(fs.readFileSync('moved.txt', 'utf8'), 'rename me\n');
            assert.deepEqual(fs.readFileSync('new.bin'), binary);
            assert.deepEqual(read(progressFile), progress);
            assert.equal(read(snapshotFile).status, 'working');
            assert.equal(git('rev-parse', 'HEAD').trim(), headSha);
            """);
    }

    [TestMethod]
    public void Snapshot_InvalidProgressPreservesPreviousAtomicSnapshot()
    {
        RunNode("""
            await helper.prepare(options);
            const previous = fs.readFileSync(snapshotFile, 'utf8');
            fs.writeFileSync('tracked.txt', 'changed');
            fs.writeFileSync(progressFile, '{}');
            assert.throws(() => helper.snapshot(progressFile, 'working', root), /Invalid checkpoint progress/);
            assert.equal(fs.readFileSync(snapshotFile, 'utf8'), previous);
            """);
    }

    [TestMethod]
    public void Snapshot_ExcludesFrameworkConfiguration()
    {
        RunNode("""
            await helper.prepare(options);
            fs.mkdirSync('.github/workflows', { recursive: true });
            fs.writeFileSync('.github/workflows/test.yml', 'framework configuration');
            fs.writeFileSync('.env', 'must not be captured');
            fs.writeFileSync('AGENTS.md', 'trusted instructions');
            fs.writeFileSync('tracked.txt', 'changed');
            helper.snapshot(progressFile, 'working', root);
            const patch = Buffer.from(read(snapshotFile).patch, 'base64').toString();
            assert.ok(patch.includes('tracked.txt'));
            assert.equal(patch.includes('.github'), false);
            assert.equal(patch.includes('.env'), false);
            assert.equal(patch.includes('AGENTS.md'), false);
            """);
    }

    [TestMethod]
    [DataRow(".github/workflows/override.yml")]
    [DataRow("../outside.txt")]
    public void Restore_RejectsProtectedOrEscapingPatch(string target)
    {
        RunNode("""
            await helper.prepare(options);
            saveForRestore();
            const file = path.join(process.env.RUNNER_TEMP, 'implementation-restore', 'checkpoint.json');
            const checkpoint = read(file);
            const target = process.env.TEST_CASE;
            const patch = Buffer.from(`diff --git a/${target} b/${target}\nnew file mode 100644\n--- /dev/null\n+++ b/${target}\n@@ -0,0 +1 @@\n+untrusted\n`);
            checkpoint.snapshot.patch = patch.toString('base64');
            checkpoint.snapshot.patchHash = require('node:crypto').createHash('sha256').update(patch).digest('hex');
            fs.writeFileSync(file, JSON.stringify(checkpoint));
            process.env.GITHUB_RUN_ATTEMPT = '2';
            await assert.rejects(() => helper.prepare(options));
            assert.equal(fs.existsSync(target), false);
            """, target);
    }

    [TestMethod]
    public void Restore_MissingArtifactOrAttemptLimitFailsClosed()
    {
        RunNode("""
            process.env.GITHUB_RUN_ATTEMPT = '2';
            await assert.rejects(() => helper.prepare(options), /ENOENT/);
            process.env.GITHUB_RUN_ATTEMPT = '4';
            await assert.rejects(() => helper.prepare(options), /Attempt limit/);
            """);
    }

    [TestMethod]
    [DataRow("headSha")]
    [DataRow("planHash")]
    [DataRow("reviewsHash")]
    [DataRow("workflowSha")]
    [DataRow("runId")]
    [DataRow("attempt")]
    [DataRow("patchHash")]
    public void Restore_RejectsStaleOrCorruptCheckpoint(string changedField)
    {
        RunNode("""
            await helper.prepare(options);
            saveForRestore();
            const file = path.join(process.env.RUNNER_TEMP, 'implementation-restore', 'checkpoint.json');
            const checkpoint = read(file);
            const field = process.env.TEST_CASE;
            if (field === 'patchHash') checkpoint.snapshot.patchHash = 'corrupt';
            else checkpoint.metadata[field] = 'different';
            fs.writeFileSync(file, JSON.stringify(checkpoint));
            process.env.GITHUB_RUN_ATTEMPT = '2';
            await assert.rejects(() => helper.prepare(options));
            assert.equal(git('status', '--porcelain'), '');
            """, changedField);
    }

    [TestMethod]
    [DataRow("label")]
    [DataRow("closed")]
    [DataRow("fork")]
    [DataRow("plan")]
    [DataRow("review")]
    public void Restore_RechecksLiveEligibilityAndEvidence(string changedField)
    {
        RunNode("""
            await helper.prepare(options);
            saveForRestore();
            switch (process.env.TEST_CASE) {
              case 'label': pull.labels = []; break;
              case 'closed': pull.state = 'closed'; break;
              case 'fork': pull.head.repo.full_name = 'someone/fork'; break;
              case 'plan': plan.body = 'Changed accepted plan'; break;
              case 'review': reviews = [{ id: 3, commit_id: headSha, state: 'CHANGES_REQUESTED', body: 'New findings' }]; break;
            }
            process.env.GITHUB_RUN_ATTEMPT = '2';
            await assert.rejects(() => helper.prepare(options));
            """, changedField);
    }

    [TestMethod]
    [DataRow("continue", "success", 1, true)]
    [DataRow("working", "failure", 1, true)]
    [DataRow("blocked", "failure", 1, false)]
    [DataRow("complete", "failure", 1, false)]
    [DataRow("working", "cancelled", 1, false)]
    [DataRow("working", "success", 1, false)]
    [DataRow("continue", "success", 3, false)]
    public void Retry_RespectsStatusAndAttemptLimit(string status, string conclusion, int attempt, bool expected)
    {
        RunNode($$"""
            run.conclusion = '{{conclusion}}';
            run.run_attempt = {{attempt}};
            assert.equal(helper.shouldRetry(run, { status: '{{status}}' }, jobs), {{expected.ToString().ToLowerInvariant()}});
            """);
    }

    [TestMethod]
    public void Retry_DoesNotRetryOrdinaryFailureOrDetectionTimeout()
    {
        RunNode("""
            run.conclusion = 'failure';
            jobs[0].steps[0].completed_at = '2026-09-15T10:02:00Z';
            assert.equal(helper.shouldRetry(run, { status: 'working' }, jobs), false);
            jobs[0].steps[0].conclusion = 'timed_out';
            assert.equal(helper.shouldRetry(run, { status: 'working' }, jobs), true);
            jobs[0].name = 'detection';
            assert.equal(helper.shouldRetry(run, { status: 'working' }, jobs), false);
            """);
    }

    [TestMethod]
    public void Retry_RerunsSameRunOnlyOnceForDuplicateEvent()
    {
        RunNode("""
            await helper.prepare(options);
            helper.snapshot(progressFile, 'continue', root);
            saveForRestore();
            await helper.retry(options);
            await helper.retry(options);
            assert.equal(reruns, 1);
            """);
    }

    [TestMethod]
    public void Retry_RejectsCheckpointForDifferentRun()
    {
        RunNode("""
            await helper.prepare(options);
            helper.snapshot(progressFile, 'continue', root);
            saveForRestore();
            run.id = 200;
            await assert.rejects(() => helper.retry(options), /provenance mismatch/);
            assert.equal(reruns, 0);
            """);
    }

    private static void RunNode(string testCode, string testCase = "")
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "RetirementCalculator.slnx")))
        {
            repository = repository.Parent;
        }

        Assert.IsNotNull(repository, "Repository root must be available to run workflow tests.");
        var temporaryDirectory = Directory.CreateTempSubdirectory("implementation-checkpoint-");
        try
        {
            var startInfo = new ProcessStartInfo("node")
            {
                WorkingDirectory = temporaryDirectory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--eval");
            startInfo.ArgumentList.Add(Fixture + "\n(async () => {\n" + testCode + "\n})().catch(error => { console.error(error); process.exitCode = 1; });");
            startInfo.Environment["CHECKPOINT_HELPER"] = Path.Combine(repository.FullName, ".github", "scripts", "implementation-checkpoint.cjs");
            startInfo.Environment["RUNNER_TEMP"] = temporaryDirectory.FullName;
            startInfo.Environment["GITHUB_RUN_ATTEMPT"] = "1";
            startInfo.Environment["GITHUB_WORKFLOW_SHA"] = "workflow-commit";
            startInfo.Environment["TEST_CASE"] = testCase;
            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                Assert.Fail("Checkpoint test exceeded 30 seconds.");
            }

            Assert.AreEqual(0, process.ExitCode, output.GetAwaiter().GetResult() + errors.GetAwaiter().GetResult());
        }
        finally
        {
            foreach (var file in temporaryDirectory.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                file.Attributes = FileAttributes.Normal;
            }

            temporaryDirectory.Delete(recursive: true);
        }
    }
}