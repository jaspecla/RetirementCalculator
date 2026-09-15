const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { execFileSync } = require('node:child_process');

const workflowPath = '.github/workflows/implement-accepted-plan.lock.yml';
const maximumAttempts = 3;
const maximumBytes = 20 * 1024 * 1024;
const checkpointRoot = '/tmp/gh-aw';
const digest = value => crypto.createHash('sha256').update(value).digest('hex');

function requireCondition(condition, message) {
  if (!condition) throw new Error(message);
}

function readJson(file) {
  const stat = fs.lstatSync(file);
  requireCondition(stat.isFile() && !stat.isSymbolicLink() && stat.size <= maximumBytes,
    'Checkpoint must be a regular file no larger than 20 MiB.');
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}

function writeJson(file, value) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const temporary = `${file}.${crypto.randomUUID()}.tmp`;
  fs.writeFileSync(temporary, JSON.stringify(value), { flag: 'wx' });
  fs.renameSync(temporary, file);
}

function git(argumentsList, options = {}) {
  return execFileSync('git', argumentsList, { maxBuffer: maximumBytes, ...options });
}

function validateSnapshot(snapshot, headSha) {
  requireCondition(snapshot?.version === 1 && snapshot.headSha === headSha, 'Checkpoint base SHA mismatch.');
  requireCondition(['working', 'continue', 'complete', 'blocked'].includes(snapshot.status), 'Invalid checkpoint status.');
  requireCondition(typeof snapshot.patch === 'string' && snapshot.patch.length <= maximumBytes,
    'Checkpoint patch is missing or too large.');
  const patch = Buffer.from(snapshot.patch, 'base64');
  requireCondition(patch.toString('base64') === snapshot.patch && digest(patch) === snapshot.patchHash,
    'Checkpoint patch integrity check failed.');
  for (const field of ['completed', 'remaining', 'validation']) {
    requireCondition(Array.isArray(snapshot.progress?.[field]) && snapshot.progress[field].length <= 200 &&
      snapshot.progress[field].every(item => typeof item === 'string' && item.length <= 4000),
    `Invalid checkpoint progress: ${field}.`);
  }
  requireCondition(typeof snapshot.progress?.notes === 'string' && snapshot.progress.notes.length <= 16000,
    'Invalid checkpoint notes.');
  return patch;
}

function validateIdentity(saved, current) {
  for (const field of ['repository', 'pullRequest', 'headSha', 'planId', 'planHash', 'reviewsHash']) {
    requireCondition(saved[field] === current[field], `Checkpoint is stale: ${field} changed.`);
  }
}

async function evidence(github, repository, pullRequest) {
  requireCondition(Number.isSafeInteger(pullRequest) && pullRequest > 0, 'Invalid pull request number.');
  const { data: pull } = await github.rest.pulls.get({ ...repository, pull_number: pullRequest });
  const fullName = `${repository.owner}/${repository.repo}`;
  requireCondition(pull.state === 'open' && !pull.merged && pull.head.repo?.full_name === fullName &&
    pull.labels.some(label => label.name === 'plan_accepted'), 'PR must be open, same-repository, and plan_accepted.');
  const { data: comments } = await github.rest.issues.listComments({ ...repository, issue_number: pullRequest, per_page: 1 });
  const plan = comments[0];
  requireCondition(plan && typeof plan.body === 'string' && plan.body.trim(), 'Accepted first-comment plan is missing.');
  const reviews = await github.paginate(github.rest.pulls.listReviews, { ...repository, pull_number: pullRequest, per_page: 100 });
  const reviewData = reviews.sort((left, right) => left.id - right.id).map(review =>
    [review.id, review.commit_id, review.state, review.body, review.submitted_at, review.user?.login]);
  return {
    repository: fullName, pullRequest, headSha: pull.head.sha,
    planId: plan.id, planHash: digest(plan.body), reviewsHash: digest(JSON.stringify(reviewData))
  };
}

async function prepare({ github, context, core, root = checkpointRoot }) {
  const attempt = Number(process.env.GITHUB_RUN_ATTEMPT);
  requireCondition(Number.isSafeInteger(attempt) && attempt >= 1 && attempt <= maximumAttempts,
    'Attempt limit reached; start a new workflow dispatch after investigating.');
  const pullRequest = Number(context.payload.pull_request?.number || context.payload.inputs?.pull_request_number);
  const identity = await evidence(github, context.repo, pullRequest);
  const actualHead = git(['rev-parse', 'HEAD']).toString().trim();
  requireCondition(actualHead === identity.headSha, 'Checkout no longer matches the current PR head.');
  const reviewedHead = context.payload.inputs?.reviewed_head_sha;
  requireCondition(!reviewedHead || reviewedHead === identity.headSha, 'Dispatched review is stale.');
  const metadata = {
    version: 1, ...identity, runId: context.runId, attempt,
    workflowSha: process.env.GITHUB_WORKFLOW_SHA, startedAt: new Date().toISOString()
  };
  let snapshot = {
    version: 1, headSha: identity.headSha, status: 'working', patch: '', patchHash: digest(''),
    progress: { completed: [], remaining: [], validation: [], notes: 'Fresh implementation attempt.' }
  };
  if (attempt > 1) {
    const previous = readJson(path.join(process.env.RUNNER_TEMP, 'implementation-restore', 'checkpoint.json'));
    validateIdentity(previous.metadata, metadata);
    requireCondition(previous.metadata.runId === context.runId && previous.metadata.attempt === attempt - 1 &&
      previous.metadata.workflowSha === metadata.workflowSha, 'Checkpoint does not belong to the preceding attempt.');
    snapshot = previous.snapshot;
    const patch = validateSnapshot(snapshot, identity.headSha);
    requireCondition(['working', 'continue'].includes(snapshot.status), 'Completed or blocked checkpoints cannot resume.');
    if (patch.length) {
      const changedPaths = git(['apply', '--numstat', '-z', '--binary', '-'], { input: patch }).toString().split('\0').filter(Boolean);
      for (const entry of changedPaths) {
        const file = entry.replace(/^[^\t]+\t[^\t]+\t/, '');
        requireCondition(!file.startsWith('.') && file !== 'AGENTS.md', 'Checkpoint cannot replace trusted agent configuration.');
      }
      git(['apply', '--check', '--binary', '-'], { input: patch });
      git(['apply', '--binary', '-'], { input: patch });
    }
    snapshot.status = 'working';
  }
  writeJson(path.join(process.env.RUNNER_TEMP, 'implementation-metadata.json'), metadata);
  writeJson(path.join(root, 'implementation-context.json'), metadata);
  writeJson(path.join(root, 'implementation-snapshot.json'), snapshot);
  writeJson(path.join(root, 'checkpoint-progress.json'), snapshot.progress);
  fs.copyFileSync(__filename, path.join(root, 'implementation-checkpoint.cjs'));
  core.setOutput('ready', 'true');
  core.info(`Implementation attempt ${attempt}/${maximumAttempts}; restored ${attempt > 1 ? 'previous checkpoint' : 'no prior work'}.`);
}

function snapshot(progressFile, status, root = checkpointRoot) {
  const metadata = readJson(path.join(root, 'implementation-context.json'));
  requireCondition(git(['rev-parse', 'HEAD']).toString().trim() === metadata.headSha, 'Workers must not commit changes.');
  const files = ['.', ':(glob,exclude).*', ':(glob,exclude).*/**', ':(exclude)AGENTS.md'];
  git(['add', '--intent-to-add', '--all', '--', ...files]);
  const patch = git(['diff', '--binary', '--no-ext-diff', '--no-textconv', 'HEAD', '--', ...files]);
  const result = {
    version: 1, headSha: metadata.headSha, status, patch: patch.toString('base64'),
    patchHash: digest(patch), progress: readJson(progressFile)
  };
  validateSnapshot(result, metadata.headSha);
  requireCondition(Buffer.byteLength(JSON.stringify(result)) <= maximumBytes, 'Checkpoint exceeds 20 MiB.');
  writeJson(path.join(root, 'implementation-snapshot.json'), result);
}

function save({ core, root = checkpointRoot }) {
  const metadata = readJson(path.join(process.env.RUNNER_TEMP, 'implementation-metadata.json'));
  const savedSnapshot = readJson(path.join(root, 'implementation-snapshot.json'));
  validateSnapshot(savedSnapshot, metadata.headSha);
  writeJson(path.join(process.env.RUNNER_TEMP, 'implementation-upload', 'checkpoint.json'), { metadata, snapshot: savedSnapshot });
  core.info(`Saved ${savedSnapshot.status} checkpoint for attempt ${metadata.attempt}.`);
}

function shouldRetry(run, savedSnapshot, jobs) {
  if (run.status !== 'completed' || run.run_attempt >= maximumAttempts ||
    ['cancelled', 'skipped', 'action_required'].includes(run.conclusion) ||
    ['blocked', 'complete'].includes(savedSnapshot.status)) return false;
  if (savedSnapshot.status === 'continue' && run.conclusion === 'success') return true;
  if (!['failure', 'timed_out'].includes(run.conclusion)) return false;
  return jobs.some(job => job.name === 'agent' && (job.conclusion === 'timed_out' ||
    job.steps?.some(step => step.name === 'Execute GitHub Copilot CLI' &&
      (step.conclusion === 'timed_out' || (step.conclusion === 'failure' &&
        Date.parse(step.completed_at) - Date.parse(step.started_at) >= 60 * 60 * 1000 - 5000)))));
}

async function retry({ github, context, core }) {
  const source = context.payload.workflow_run;
  const { data: run } = await github.rest.actions.getWorkflowRun({ ...context.repo, run_id: source.id });
  requireCondition(run.path === workflowPath && ['pull_request', 'workflow_dispatch'].includes(run.event), 'Unexpected source workflow.');
  if (run.status !== 'completed' || run.run_attempt !== source.run_attempt) {
    core.info('A newer attempt already exists or is running.');
    return;
  }
  const checkpointFile = path.join(process.env.RUNNER_TEMP, 'implementation-restore', 'checkpoint.json');
  if (!fs.existsSync(checkpointFile)) {
    core.info('No checkpoint was uploaded; automatic continuation is not possible.');
    return;
  }
  const checkpoint = readJson(checkpointFile);
  requireCondition(checkpoint.metadata.version === 1 && checkpoint.metadata.runId === run.id &&
    checkpoint.metadata.attempt === run.run_attempt, 'Checkpoint provenance mismatch.');
  const current = await evidence(github, context.repo, checkpoint.metadata.pullRequest);
  validateIdentity(checkpoint.metadata, current);
  validateSnapshot(checkpoint.snapshot, current.headSha);
  if (run.event === 'pull_request') {
    requireCondition(run.pull_requests.some(pull => pull.number === current.pullRequest), 'Checkpoint targets another PR.');
  }
  const jobs = await github.paginate(github.rest.actions.listJobsForWorkflowRunAttempt,
    { ...context.repo, run_id: run.id, attempt_number: run.run_attempt, per_page: 100 });
  if (!shouldRetry(run, checkpoint.snapshot, jobs)) {
    await core.summary.addRaw(`No automatic continuation: checkpoint=${checkpoint.snapshot.status}, conclusion=${run.conclusion}, attempt=${run.run_attempt}/${maximumAttempts}.`).write();
    return;
  }
  await github.rest.actions.reRunWorkflow({ ...context.repo, run_id: run.id });
  await core.summary.addRaw(`Requested implementation attempt ${run.run_attempt + 1}/${maximumAttempts} for PR #${current.pullRequest}, restoring attempt ${run.run_attempt}.`).write();
}

module.exports = { prepare, save, retry, snapshot, validateSnapshot, validateIdentity, shouldRetry, evidence };

if (require.main === module) {
  requireCondition(process.argv[2] === 'snapshot', 'Only the snapshot command is available in the agent sandbox.');
  snapshot(process.argv[3], process.argv[4] || 'working');
}