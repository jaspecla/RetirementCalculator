# Implementation Recovery

`implement-accepted-plan.md` allows 60 minutes of agent execution. The installed
gh-aw compiler does not emit a separate agent-job timeout, so GitHub Actions'
default job budget leaves time for setup, checkpoint upload, and cleanup.

## Activation

Merge the workflow source, regenerated lock file, checkpoint helper, and
`resume-implementation.yml` into the default branch before starting a new
implementation dispatch. The `workflow_run` controller only activates when it
exists on the default branch. Old runs retain their original workflow revision
and cannot acquire checkpoint support by being rerun.

The controller uses the repository `GITHUB_TOKEN` with `actions: write` solely
to rerun the original workflow. The implementation agent retains read-only
repository access; its existing safe output remains the only code publication
path. No additional secret is required. Repository or organization policy must
permit the controller's Actions write permission.

## Checkpoint Lifecycle

1. Preparation verifies an open, same-repository PR with `plan_accepted`, the
   current head SHA, and the first-comment plan. It fingerprints the plan and
   review evidence independently of the agent.
2. Workers update `/tmp/gh-aw/checkpoint-progress.json` and invoke the snapshot
   helper after each completed unit. A single atomic JSON snapshot contains the
   aggregate binary Git patch (including non-ignored new files), its checksum,
   completed and remaining items, test results, and handoff notes. Workers do not
   commit or push. Top-level dotfiles, dot-directories, and `AGENTS.md` are
   excluded, preserving gh-aw's trusted configuration. Restoration runs after
   gh-aw's own PR checkout and configuration setup.
3. At 50 elapsed minutes the orchestrator stops starting workers and aims to exit
   by minute 55 with status `continue` and a `noop`, leaving time for upload.
4. Post-agent steps package the most recent complete snapshot, even after an
   execution-step failure, and upload `implementation-checkpoint-N` for attempt
   N. Artifacts expire after seven days and must stay below 20 MiB. Never include
   credentials or unrelated files.
5. The controller reruns the same run after a successful `continue` handoff or
   an agent execution timeout with a recoverable snapshot. A timeout reported
   only as `failure` is recognized when the execution step lasted approximately
   the full 60-minute budget. Other failures are not automatically retried.
6. Attempt N+1 downloads attempt N's artifact. Restoration checks the repository,
   PR, run ID, attempt, workflow revision, head SHA, plan, reviews, and patch
   checksum before `git apply --check` and restoration. The agent rechecks the
   source issue and all authorization rules and validates code before publishing.

There are at most three attempts per run (up to 180 agent minutes). A per-PR
concurrency group serializes implementation runs. Duplicate completion events
are ignored when a newer attempt is already queued or running. A pending run
can still be replaced by GitHub's concurrency queue behavior; this is not an
unbounded work queue.

The retry controller never executes the patch or handoff notes. The implementation
restores code on its isolated runner, subject to its normal sandbox and final
safe-output safeguards. Checkpoints are untrusted context, not new instructions
or proof of passing validation.

## Status and Manual Recovery

- `working`: last complete worker snapshot; recoverable after an execution timeout.
- `continue`: deliberate time-budget handoff; recoverable after a successful run
  or execution timeout.
- `blocked`: eligibility, scope, validation, or other real blocker; no retry.
- `complete`: validated final delivery is about to be requested, or no changes
  remain necessary; no retry. Inspect safe-output results to confirm the push
  actually succeeded. This status alone is not proof of delivery.

After an attempt ends, inspect its checkpoint artifact and the Resume
Implementation job summary. If the retry controller did not run, manually
rerunning **all jobs** of the original run restores the immediately preceding
checkpoint, provided the attempt limit and freshness checks still pass. Do not
use "rerun failed jobs" for continuation: the fresh preparation, agent, and
safe-output jobs must all participate.

After three attempts, a changed PR/plan/review, a blocked checkpoint, or a failed
final push, investigate and start a new workflow dispatch when appropriate. A
new dispatch begins from committed PR code, not a previous run's uncommitted
checkpoint. Old artifacts remain available for manual inspection until expiry.

Local snapshots are not durable until upload finishes. A hard runner/job
termination can prevent upload entirely; the last interrupted worker's edits
are not captured automatically. The controller cannot guarantee recovery when
no artifact exists. All resumed work must pass final validation before the
single aggregate push and independent review.

## Validation

```powershell
gh aw compile implement-accepted-plan
dotnet test tests/RetirementCalculator.Web.Tests/RetirementCalculator.Web.Tests.csproj --filter FullyQualifiedName~ImplementationCheckpointTests
```

The MSTest suite requires Node.js and Git. It uses isolated temporary repositories
and mocked GitHub responses; it does not dispatch workflows or mutate GitHub.

For an end-to-end smoke test after merging, use a disposable accepted-plan PR,
request a deliberate checkpoint handoff, verify attempt 2 restores the edits,
and verify only the final validated patch is pushed. Test a removed label or
changed plan before a continuation to confirm restoration is rejected. Actual
GitHub artifact transfer, token policy, and timeout cancellation behavior cannot
be proven by the local mocked tests.