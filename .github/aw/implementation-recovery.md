# Implementation Recovery

## Issue Planning and Task Activation

The issue lifecycle is now:

1. **Assess Issue Readiness is unchanged.** Its `ready_for_implementation` label
   starts `plan-ready-issue` for a top-level source issue.
2. Planning posts one readable, structured **source issue comment**, marked
   `issue-plan:v1`. Human comments on that same issue can revise the plan in
   place before approval. There is no parent planning PR.
3. A repository writer applies **`plan_accepted` to the parent issue**. The
   planner's guarded decomposition output reads the stored accepted plan (not a
   newly generated task list), freezes its identity, and creates up to 20 distinct
   self-contained **real GitHub sub-issues** using the sub-issue API.
4. A repository writer explicitly applies **`implementation_ready` to a chosen
   sub-issue**. This is the implementation opt-in label; parent approval alone
   never starts child implementation. Readiness labels on children do not start
   another planning cycle.
5. `bootstrap-implementation-task.yml` creates a blank draft **task** PR on the
   stable branch `implementation/issue-<number>`, copies the self-contained task
   contract into its chronologically first comment, adds PR `plan_accepted`, and
   explicitly dispatches `implement-accepted-plan` on the default branch.
   That PR closes only its source sub-issue, not the parent.
6. The existing implementation → independent Claude review → focused fixes loop
   continues with the existing review check, severity gate, five-review budget,
   checkpoint format, and three-attempt recovery limit. No automatic merge is
   introduced. Task-branch label events are excluded from initial implementation
   activation because the bootstrap owns that dispatch.

Tasks carry their own goal, scope/owned paths, implementation steps, acceptance
criteria, and MSTest validation, plus the shared plan context. Dependencies use
stable task keys from the accepted plan. A dependent task cannot bootstrap or
publish until each prerequisite has a trusted task PR merged into the default
branch; closing the prerequisite issue alone is insufficient. Independent tasks
can run concurrently through separate issue/PR concurrency groups. Overlapping
file ownership must be represented as a dependency in the reviewed plan.
After prerequisites merge, reapply `implementation_ready` or manually run the
bootstrap with that child issue number; no automatic dependency wake-up is implied.

### Configuration prerequisites

Merge the changed workflow sources/locks, both helpers, and the bootstrap/recovery
controllers into the default branch. Create the exact labels
`ready_for_implementation`, `plan_accepted`, and `implementation_ready`.
Configure:

- Repository variable **`PLAN_AUTOMATION_LOGIN`**: the login of a dedicated
  automation account. Do not use a shared human account or change the identity
  while plans/tasks are active; marker authorship is part of authorization.
- Secret **`GH_AW_GITHUB_TOKEN`**: that account's PAT, authorized for this repository
  and organization SSO where required, with Contents, Issues, Pull requests, and
  Actions read/write (and metadata read). It must permit creating branches/PRs
  and dispatching workflows. The helper verifies the authenticated login; this
  implementation uses a PAT, not an installation token. Never fall back to the
  built-in token for lifecycle writes. The PAT-generated comments/events must
  match the configured author, and its explicit dispatch starts implementation
  without relying on the suppressed `GITHUB_TOKEN` label-event chain.
- Preserve existing Copilot authentication, **`GH_AW_CI_TRIGGER_TOKEN`**, and
  independent review/required-check settings. The CI trigger token remains
  responsible for the implementation safe output's extra trigger commit.

The agent retains read-only repository access. All planning/decomposition writes
are guarded safe outputs; bootstrap mutations are deterministic conventional
workflow code, never agent-selected API targets. Helpers are loaded from the
trusted workflow revision, not from a PR checkout. Lifecycle writes run only from
the default branch. Label/manual activation requires a repository writer; the
readiness bot may request planning only. Feedback may revise an unaccepted plan
but cannot accept it, create tasks, or select privileged targets.

### Idempotency and partial failure recovery

- Plans are identified by marker **and trusted author**, with exact source and
  prior-plan SHA256 checks before publication. Stale feedback, PR events,
  duplicate/untrusted markers, and changed scope fail closed.
- Decomposition first records `plan-decomposition:v1` with the frozen plan hash.
  Full paginated issue lists recover newly created orphans even when create
  responses or relationship attachment fail; GitHub search indexing is not used.
  Existing children, including closed children, are reused, not duplicated.
  Edited/ambiguous identities or a different parent relationship require human
  investigation. Do not delete identity markers or detach children to retry.
- Retry decomposition with
  `gh aw run plan-ready-issue --ref main --raw-field issue_number=<parent>`.
  Replace `main` with the repository default branch. It resumes the same frozen
  plan. A decomposed plan cannot be revised even after removing approval;
  use a new source issue for changed scope.
- Bootstrap recovers the stable branch, an existing PR (including create-response
  loss), first-comment publication, and label failures. Closed/merged PRs and
  first-comment collisions never produce replacement PRs or overwrite comments.
  Rerun **Bootstrap Implementation Task** with `issue_number=<child>` to recover.
- GitHub workflow dispatch has no idempotency key. A durable
  `implementation-dispatch:v1` receipt is written **before** dispatch. If the
  dispatch response is lost, or execution stops between claim and dispatch, a
  retry does **not** dispatch again. Inspect Actions first; if no run was created,
  explicitly recover with
  `gh aw run implement-accepted-plan --ref main --raw-field pull_request_number=<task-pr>`.
  Do not remove the receipt or blindly redispatch an already running task.
- Preparation, checkpoint retries, final publication, and review/repair dispatch
  recheck the real source relationship, trusted first-comment contract, live
  parent acceptance and source hash, child opt-in, and merged dependencies.
  Removing either approval label blocks further publication. Existing legacy
  planning PRs keep their existing checkpoint/first-comment contract.

GitHub APIs do not provide a transaction spanning issue state, comments, branches,
and dispatch. Guards recheck before mutations but cannot make concurrent human
edits atomic. Freeze accepted plans during execution; investigate failures rather
than bypassing guards. Parent-level planning and child bootstrap concurrency are
serialized per issue, while independent task implementations remain parallel.

## Execution Budget

`implement-accepted-plan.md` allows 60 minutes of agent execution. The installed
gh-aw compiler does not emit a separate agent-job timeout, so GitHub Actions'
default job budget leaves time for setup, checkpoint upload, and cleanup.

The harness also has a separate two-minute post-result inactivity watchdog.
Queuing a comment, push, or `noop` arms it; it can terminate quiet workers and
still report success because a safe output exists. Prepare review responses
before repairs, but queue them only after all workers, validation, and snapshots
finish, immediately followed by the final push or `noop`. A green run with only
a response comment does not prove that repairs were published. Without a push,
there is no PR `synchronize` event to trigger another independent review.

## Activation

Merge the workflow source, regenerated lock file, checkpoint helper, and
`resume-implementation.yml` into the default branch before starting a new
implementation dispatch. The `workflow_run` controller only activates when it
exists on the default branch. Old runs retain their original workflow revision
and cannot acquire checkpoint support by being rerun.

The controller uses the repository `GITHUB_TOKEN` with `actions: write` solely
to rerun the original workflow. The implementation agent retains read-only
repository access; its existing safe output remains the only code publication
path. Recovery alone requires no additional secret beyond the lifecycle setup above. Repository or organization policy must
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
gh aw compile plan-ready-issue review-implemented-plan
dotnet test tests/RetirementCalculator.Web.Tests/RetirementCalculator.Web.Tests.csproj --filter "FullyQualifiedName~ImplementationCheckpointTests|FullyQualifiedName~IssuePlanLifecycleTests"
```

Use the compiler version recorded in the locks (currently v0.86.2). The MSTest
suite requires Node.js and Git. It uses isolated temporary repositories and mocked
GitHub responses; it does not dispatch workflows or mutate GitHub. CI includes
the lifecycle sources, bootstrap, and helpers in its path filters.

For an end-to-end smoke test after merging, use a disposable accepted-plan PR,
request a deliberate checkpoint handoff, verify attempt 2 restores the edits,
and verify only the final validated patch is pushed. Test a removed label or
changed plan before a continuation to confirm restoration is rejected. Actual
GitHub artifact transfer, token policy, and timeout cancellation behavior cannot
be proven by the local mocked tests.

Also smoke-test a disposable parent through planning, source-comment revision,
acceptance, sub-issue attachment, and explicit child opt-in. Verify one task PR,
its first-comment contract, one initial dispatch, and the independent review
check after publication. Retry a partially completed decomposition/bootstrap and
exercise both a blocked dependency and two independent children. Local tests do
not prove PAT/SSO policy, real API consistency, threat detection, model output
quality, GitHub event delivery, or Actions concurrency behavior.