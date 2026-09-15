// Trusted control-plane code. Load from the workflow revision, never from a PR checkout.
const crypto = require('node:crypto');
const fs = require('node:fs');
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const requireThat = (condition, message) => { if (!condition) throw new Error(message); };
const label = (issue, name) => issue.labels.some(item => item.name === name);
const sourceHash = issue => hash(JSON.stringify([issue.title, issue.body || '']));
const number = value => {
  requireThat(/^[1-9][0-9]*$/.test(String(value)) && Number.isSafeInteger(Number(value)), 'Invalid issue number.');
  return Number(value);
};
const marker = (kind, value) => `<!-- ${kind}:v1 ${Buffer.from(JSON.stringify(value)).toString('base64')} -->`;
function decode(body, kind) {
  const matches = [...(body || '').matchAll(new RegExp(`<!-- ${kind}:v1 ([A-Za-z0-9+/=]+) -->`, 'g'))];
  requireThat(matches.length === 1, `Missing or ambiguous ${kind} marker.`);
  return JSON.parse(Buffer.from(matches[0][1], 'base64').toString());
}
function validatePlan(plan) {
  requireThat(plan && typeof plan.summary === 'string' && plan.summary.trim() &&
    Array.isArray(plan.tasks) && plan.tasks.length >= 1 && plan.tasks.length <= 20, 'Plan needs 1–20 self-contained tasks.');
  const keys = new Set();
  for (const task of plan.tasks) {
    requireThat(/^[a-z][a-z0-9-]{0,39}$/.test(task.key) && !keys.has(task.key), 'Task keys must be distinct.');
    keys.add(task.key);
    for (const field of ['title', 'goal', 'scope', 'implementation', 'acceptance', 'validation']) {
      requireThat(typeof task[field] === 'string' && task[field].trim() && task[field].length <= 6000,
        `Task requires bounded ${field}.`);
    }
    requireThat(task.title.length <= 180 && !/[\r\n]/.test(task.title), 'Invalid task title.');
    requireThat(Array.isArray(task.depends_on) && new Set(task.depends_on).size === task.depends_on.length,
      'Dependencies must be distinct task keys.');
  }
  const visited = new Set();
  const visiting = new Set();
  function visit(key) {
    requireThat(keys.has(key) && !visiting.has(key), 'Unknown or cyclic dependency.');
    if (visited.has(key)) return;
    visiting.add(key);
    plan.tasks.find(task => task.key === key).depends_on.forEach(visit);
    visiting.delete(key);
    visited.add(key);
  }
  keys.forEach(visit);
  requireThat(JSON.stringify(plan).length <= 24000, 'Plan is too large.');
  return plan;
}
function taskText(task) {
  return `#### ${task.key}: ${task.title}\n\n` +
    ['goal', 'scope', 'implementation', 'acceptance', 'validation']
      .map(field => `**${field}:**\n${task[field]}`).join('\n\n') +
    `\n\n**Dependencies:** ${task.depends_on.join(', ') || 'None (independent)'}`;
}
function planBody(record) {
  return `### Implementation plan\n\n${record.plan.summary}\n\n### Source issue\n\n#${record.issue}\n\n` +
    record.plan.tasks.map(taskText).join('\n\n') +
    `\n\nApply \`plan_accepted\` to this issue to create real sub-issues. No planning PR is created.\n\n${marker('issue-plan', record)}`;
}
function taskBody(record, task) {
  return `### Source issue\n\nParent #${record.issue}; accepted plan comment ${record.comment} (${record.hash}).\n\n` +
    `### Implementation plan\n\n${record.plan.summary}\n\n${taskText(task)}\n\n` +
    'Apply `implementation_ready` to this sub-issue after its dependencies are merged. ' +
    'This opt-in creates its task PR; it does not accept the parent again.\n\n' +
    marker('implementation-task', { parent: record.issue, plan: record.comment, hash: record.hash, key: task.key });
}
function contractBody(taskIssue, record, task) {
  return `### Source issue\n\nCloses #${taskIssue.number}\n\nParent #${record.issue}; accepted plan comment ${record.comment}.\n\n` +
    `### Implementation plan\n\n${record.plan.summary}\n\n${taskText(task)}\n\n` +
    marker('implementation-contract', { issue: taskIssue.number, parent: record.issue, hash: record.hash, key: task.key });
}
function api(github, repo, login) {
  requireThat(typeof login === 'string' && /^[A-Za-z0-9-]+(?:\[bot\])?$/.test(login),
    'Configure PLAN_AUTOMATION_LOGIN to the dedicated automation token author.');
  const issue = async n => (await github.rest.issues.get({ ...repo, issue_number: n })).data;
  const comments = n => github.paginate(github.rest.issues.listComments, { ...repo, issue_number: n, per_page: 100 });
  const parent = async n => {
    try { return (await github.request('GET /repos/{owner}/{repo}/issues/{issue_number}/parent', { ...repo, issue_number: n })).data; }
    catch (error) { if (error.status === 404) return null; throw error; }
  };
  const trusted = item => item?.user?.login === login;
  async function root(n, accepted) {
    const current = await issue(n);
    requireThat(!current.pull_request && current.state === 'open' &&
      label(current, accepted ? 'plan_accepted' : 'ready_for_implementation') &&
      (accepted || !label(current, 'plan_accepted')), 'Source issue is not eligible.');
    requireThat(!await parent(n), 'Sub-issues cannot enter parent planning.');
    return current;
  }
  async function accepted(n) {
    const current = await root(n, true);
    const matches = (await comments(n)).filter(c => (c.body || '').includes('<!-- issue-plan:v1 '));
    requireThat(matches.length === 1 && trusted(matches[0]), 'Accepted plan must have a unique trusted author and marker.');
    const comment = matches[0];
    const record = decode(comment.body, 'issue-plan');
    validatePlan(record.plan);
    requireThat(record.issue === n && record.source_hash === sourceHash(current) && planBody(record) === comment.body,
      'Accepted plan or source issue changed; stop and investigate.');
    return { ...record, comment: comment.id, hash: hash(comment.body) };
  }
  async function task(n, requireReady = true) {
    const current = await issue(n);
    requireThat(!current.pull_request && trusted(current) && (!requireReady ||
      (current.state === 'open' && label(current, 'implementation_ready'))), 'Task is not implementation_ready or trusted.');
    const identity = decode(current.body, 'implementation-task');
    const relationship = await parent(n);
    requireThat(relationship?.number === identity.parent, 'Task must have its real source parent relationship.');
    const record = await accepted(number(identity.parent));
    const definition = record.plan.tasks.find(t => t.key === identity.key);
    requireThat(definition && identity.plan === record.comment && identity.hash === record.hash &&
      current.body === taskBody(record, definition) && current.title === definition.title, 'Task contract differs from accepted plan.');
    const state = (await comments(record.issue)).filter(c => (c.body || '').startsWith('<!-- plan-decomposition:v1 '));
    requireThat(state.length === 1 && trusted(state[0]) &&
      state[0].body === marker('plan-decomposition', { hash: record.hash, comment: record.comment }),
    'Missing frozen decomposition identity.');
    return { current, record, definition };
  }
  return { issue, comments, parent, trusted, root, accepted, task };
}
async function authorize(github, context, login, planning = false) {
  const identity = (await github.rest.users.getAuthenticated()).data;
  requireThat(identity.login === login, 'GH_AW_GITHUB_TOKEN must belong to PLAN_AUTOMATION_LOGIN.');
  const repository = (await github.rest.repos.get(context.repo)).data;
  requireThat(context.ref === `refs/heads/${repository.default_branch}`, 'Lifecycle writes require the default branch.');
  const event = context.eventName;
  requireThat(['issues', 'issue_comment', 'workflow_dispatch'].includes(event), 'Unsupported lifecycle event.');
  if (event === 'issue_comment') {
    requireThat(planning && !context.payload.issue.pull_request &&
      context.payload.comment.user.login !== login && context.payload.comment.user.type !== 'Bot', 'Not human source-issue feedback.');
    const { data: live } = await github.rest.issues.getComment({ ...context.repo, comment_id: context.payload.comment.id });
    requireThat(live.body === context.payload.comment.body && live.updated_at === context.payload.comment.updated_at,
      'Feedback was edited or deleted after the event.');
    return;
  }
  // Assess Issue Readiness uses the built-in bot. It may request planning, never acceptance/bootstrap.
  if (planning && event === 'issues' && context.payload.label?.name === 'ready_for_implementation' &&
    context.actor === 'github-actions[bot]') return;
  const { data } = await github.rest.repos.getCollaboratorPermissionLevel({ ...context.repo, username: context.actor });
  requireThat(['admin', 'maintain', 'write'].includes(data.permission), 'A repository writer must authorize this operation.');
}
async function publish({ github, context, login, item }) {
  await authorize(github, context, login, true);
  requireThat(context.eventName !== 'issues' || context.payload.label?.name === 'ready_for_implementation', 'Wrong planning label event.');
  const n = number(context.payload.issue?.number || context.payload.inputs?.issue_number);
  const a = api(github, context.repo, login);
  const current = await a.root(n, false);
  requireThat(item.expected_source_hash === sourceHash(current), 'Source issue changed during planning.');
  const comments = await a.comments(n);
  requireThat(!comments.some(c => (c.body || '').includes('<!-- plan-decomposition:v1 ')), 'A decomposed plan cannot be revised.');
  const matches = comments.filter(c => (c.body || '').includes('<!-- issue-plan:v1 '));
  requireThat(matches.length <= 1 && (!matches.length || a.trusted(matches[0])), 'Ambiguous or untrusted plan marker.');
  const previous = matches[0];
  requireThat((previous ? hash(previous.body) : '') === item.expected_plan_hash, 'Plan changed during generation.');
  requireThat(context.eventName !== 'issue_comment' || (previous && previous.id !== context.payload.comment.id),
    'Feedback may revise only an existing plan, not the plan comment itself.');
  const plan = validatePlan(JSON.parse(item.plan_json));
  const body = planBody({ issue: n, source_hash: sourceHash(current), plan });
  if (body === previous?.body) return;
  requireThat(sourceHash(await a.root(n, false)) === item.expected_source_hash, 'Source issue changed before publication.');
  const latest = (await a.comments(n)).filter(c => (c.body || '').includes('<!-- issue-plan:v1 '));
  requireThat(latest.length === matches.length && (!previous ||
    (latest[0].id === previous.id && a.trusted(latest[0]) && hash(latest[0].body) === item.expected_plan_hash)),
  'Plan changed before publication.');
  if (previous) await github.rest.issues.updateComment({ ...context.repo, comment_id: previous.id, body });
  else await github.rest.issues.createComment({ ...context.repo, issue_number: n, body });
}
async function decompose({ github, context, login }) {
  await authorize(github, context, login);
  requireThat(context.eventName === 'workflow_dispatch' ||
    (context.eventName === 'issues' && context.payload.label?.name === 'plan_accepted'), 'Wrong decomposition event.');
  const n = number(context.payload.issue?.number || context.payload.inputs?.issue_number);
  const a = api(github, context.repo, login);
  const record = await a.accepted(n);
  const frozen = marker('plan-decomposition', { hash: record.hash, comment: record.comment });
  const states = (await a.comments(n)).filter(c => (c.body || '').includes('<!-- plan-decomposition:v1 '));
  requireThat(states.length <= 1 && (!states.length || (a.trusted(states[0]) && states[0].body === frozen)),
    'Decomposition already started from another plan; no new tasks may be created.');
  // Persist the immutable identity BEFORE creating anything. Recover orphans using issue-list pagination,
  // not search indexing, which can lag after a successful create whose response was lost.
  if (!states.length) await github.rest.issues.createComment({ ...context.repo, issue_number: n, body: frozen });
  const all = await github.paginate(github.rest.issues.listForRepo, { ...context.repo, state: 'all', per_page: 100 });
  const children = await github.paginate('GET /repos/{owner}/{repo}/issues/{issue_number}/sub_issues',
    { ...context.repo, issue_number: n, per_page: 100 });
  requireThat(children.every(child => a.trusted(child) &&
    record.plan.tasks.some(task => child.body === taskBody(record, task))),
  'Existing sub-issues differ from the frozen plan; investigate rather than duplicating work.');
  for (const task of record.plan.tasks) {
    requireThat((await a.accepted(n)).hash === record.hash, 'Accepted plan changed during decomposition.');
    const body = taskBody(record, task);
    const taskMarker = marker('implementation-task', { parent: n, plan: record.comment, hash: record.hash, key: task.key });
    const matches = all.filter(i => !i.pull_request && (i.body || '').includes(taskMarker));
    requireThat(matches.length <= 1 && (!matches.length || (a.trusted(matches[0]) &&
      matches[0].body === body && matches[0].title === task.title)), 'Duplicate, edited, or untrusted task identity.');
    const child = matches[0] || (await github.rest.issues.create({ ...context.repo, title: task.title, body })).data;
    const relationship = await a.parent(child.number);
    requireThat(!relationship || relationship.number === n, 'Task belongs to a different parent.');
    requireThat((await a.accepted(n)).hash === record.hash, 'Accepted plan changed before sub-issue attachment.');
    if (!relationship) await github.request('POST /repos/{owner}/{repo}/issues/{issue_number}/sub_issues',
      { ...context.repo, issue_number: n, sub_issue_id: child.id });
  }
}
async function dependencies(github, repo, login, task) {
  if (!task.definition.depends_on.length) return;
  const a = api(github, repo, login);
  const children = await github.paginate('GET /repos/{owner}/{repo}/issues/{issue_number}/sub_issues',
    { ...repo, issue_number: task.record.issue, per_page: 100 });
  for (const key of task.definition.depends_on) {
    const body = taskBody(task.record, task.record.plan.tasks.find(t => t.key === key));
    const matches = children.filter(i => i.body === body && a.trusted(i));
    requireThat(matches.length === 1, `Dependency ${key} is missing or ambiguous.`);
    const dependency = await a.task(matches[0].number, false);
    const pulls = await github.paginate(github.rest.pulls.list,
      { ...repo, head: `${repo.owner}:implementation/issue-${dependency.current.number}`, state: 'all', per_page: 100 });
    requireThat(pulls.length === 1 && pulls[0].merged_at && pulls[0].base.ref ===
      (await github.rest.repos.get(repo)).data.default_branch, `Dependency ${key} must be merged, not merely closed.`);
    const comments = await a.comments(pulls[0].number);
    requireThat(a.trusted(pulls[0]) && a.trusted(comments[0]) && comments[0].body ===
      contractBody(dependency.current, dependency.record, dependency.definition), 'Dependency PR contract is invalid.');
  }
}
async function validatePull({ github, context, login, pullRequest, expectedHead }) {
  const n = number(pullRequest || context.payload.pull_request?.number || context.payload.inputs?.pull_request_number);
  const pull = (await github.rest.pulls.get({ ...context.repo, pull_number: n })).data;
  requireThat(pull.state === 'open' && !pull.merged && label(pull, 'plan_accepted') &&
    pull.head.repo?.full_name === `${context.repo.owner}/${context.repo.repo}` &&
    (!expectedHead || pull.head.sha === expectedHead), 'PR is no longer eligible or its head changed.');
  // Old planning PRs retain their existing checkpoint/first-comment contract.
  if (!pull.head.ref.startsWith('implementation/')) return pull;
  const match = /^implementation\/issue-([1-9][0-9]*)$/.exec(pull.head.ref);
  requireThat(match, 'Invalid implementation branch.');
  const a = api(github, context.repo, login);
  const task = await a.task(number(match[1]));
  requireThat(a.trusted(pull) && pull.base.ref === (await github.rest.repos.get(context.repo)).data.default_branch,
    'Task PR author or base mismatch.');
  const comments = await a.comments(n);
  requireThat(a.trusted(comments[0]) && comments[0].body === contractBody(task.current, task.record, task.definition),
    'First-comment task contract changed or is untrusted.');
  await dependencies(github, context.repo, login, task);
  return pull;
}
async function bootstrap({ github, context, login, core }) {
  await authorize(github, context, login);
  requireThat(context.eventName === 'workflow_dispatch' ||
    (context.eventName === 'issues' && context.payload.label?.name === 'implementation_ready'), 'Wrong bootstrap label event.');
  const n = number(context.payload.issue?.number || context.payload.inputs?.issue_number);
  const a = api(github, context.repo, login);
  const task = await a.task(n);
  await dependencies(github, context.repo, login, task);
  const repo = context.repo;
  const base = (await github.rest.repos.get(repo)).data.default_branch;
  const branch = `implementation/issue-${n}`;
  const pulls = await github.paginate(github.rest.pulls.list, { ...repo, head: `${repo.owner}:${branch}`, state: 'all', per_page: 100 });
  requireThat(pulls.length <= 1, 'Duplicate task PRs require human investigation.');
  let pull = pulls[0];
  if (pull) requireThat(a.trusted(pull) && pull.state === 'open' && !pull.merged_at &&
    pull.head.repo?.full_name === `${repo.owner}/${repo.repo}` && pull.base.ref === base, 'Existing task PR is closed or untrusted; do not duplicate.');
  if (!pull) {
    let ref;
    try { ref = (await github.rest.git.getRef({ ...repo, ref: `heads/${branch}` })).data; }
    catch (error) { if (error.status !== 404) throw error; }
    const message = `chore: initialize implementation task #${n}`;
    if (!ref) {
      const baseRef = (await github.rest.git.getRef({ ...repo, ref: `heads/${base}` })).data;
      const commit = (await github.rest.git.getCommit({ ...repo, commit_sha: baseRef.object.sha })).data;
      await a.task(n);
      const empty = (await github.rest.git.createCommit({ ...repo, message, tree: commit.tree.sha, parents: [baseRef.object.sha] })).data;
      ref = (await github.rest.git.createRef({ ...repo, ref: `refs/heads/${branch}`, sha: empty.sha })).data;
    }
    const tip = (await github.rest.git.getCommit({ ...repo, commit_sha: ref.object.sha })).data;
    requireThat(tip.message === message && tip.parents.length === 1, 'Existing task branch is not a bootstrap commit.');
    const parent = (await github.rest.git.getCommit({ ...repo, commit_sha: tip.parents[0].sha })).data;
    requireThat(tip.tree.sha === parent.tree.sha, 'Bootstrap branch must contain no code changes.');
    const comparison = (await github.rest.repos.compareCommitsWithBasehead({ ...repo, basehead: `${base}...${tip.parents[0].sha}` })).data;
    requireThat(['identical', 'behind'].includes(comparison.status), 'Bootstrap commit must descend from the default branch.');
    await a.task(n);
    pull = (await github.rest.pulls.create({ ...repo, title: `Task #${n}: ${task.definition.title}`, head: branch,
      base, body: `Closes #${n}\n\nImplementation task from parent #${task.record.issue}.`, draft: true })).data;
  }
  const contract = contractBody(task.current, task.record, task.definition);
  const comments = await a.comments(pull.number);
  requireThat(!comments.length || (a.trusted(comments[0]) && comments[0].body === contract), 'First-comment collision; do not overwrite.');
  await a.task(n);
  if (!comments.length) await github.rest.issues.createComment({ ...repo, issue_number: pull.number, body: contract });
  if (!label(pull, 'plan_accepted')) await github.rest.issues.addLabels({ ...repo, issue_number: pull.number, labels: ['plan_accepted'] });
  await validatePull({ github, context, login, pullRequest: pull.number });
  const receipt = marker('implementation-dispatch', { issue: n, pull: pull.number });
  const receipts = (await a.comments(n)).filter(c => (c.body || '').includes('<!-- implementation-dispatch:v1 '));
  requireThat(receipts.length <= 1 && (!receipts.length || (a.trusted(receipts[0]) && receipts[0].body === receipt)),
    'Invalid bootstrap dispatch receipt.');
  if (receipts.length) {
    core.info('Task PR already bootstrapped. Dispatch was claimed; inspect Actions before manually recovering an uncertain dispatch.');
    return;
  }
  // GitHub dispatch has no idempotency key. Claim BEFORE sending: a lost response must not start a
  // second implementation. An interrupted claim requires an explicit maintainer recovery dispatch.
  await github.rest.issues.createComment({ ...repo, issue_number: n, body: receipt });
  await validatePull({ github, context, login, pullRequest: pull.number });
  await github.rest.actions.createWorkflowDispatch({ ...repo, workflow_id: 'implement-accepted-plan.lock.yml',
    ref: base, inputs: { pull_request_number: String(pull.number) } });
  core.info(`Dispatched implementation for task #${n}, PR #${pull.number}.`);
}
async function safeOutput(options) {
  const stat = fs.lstatSync(process.env.GH_AW_AGENT_OUTPUT);
  requireThat(stat.isFile() && !stat.isSymbolicLink() && stat.size <= 1024 * 1024, 'Invalid lifecycle output artifact.');
  const data = JSON.parse(fs.readFileSync(process.env.GH_AW_AGENT_OUTPUT, 'utf8'));
  const writes = data.items.filter(i => ['publish_issue_plan', 'decompose_accepted_plan'].includes(i.type));
  requireThat(writes.length === 1, 'Exactly one lifecycle output is allowed.');
  if (writes[0].type === 'publish_issue_plan') await publish({ ...options, item: writes[0] });
  else await decompose(options);
}
async function guardDelivery({ github, context, login, review = false, outputFile }) {
  const pull = await validatePull({ github, context, login,
    expectedHead: context.payload.pull_request?.head?.sha || context.payload.inputs?.reviewed_head_sha });
  if (!review) return;
  const file = outputFile || '/tmp/gh-aw/agent_output.json';
  const output = JSON.parse(fs.readFileSync(file, 'utf8'));
  for (const item of output.items.filter(i => i.type === 'dispatch_workflow')) {
    requireThat(String(item.inputs?.pull_request_number) === String(context.payload.pull_request.number) &&
      item.inputs?.reviewed_head_sha === pull.head.sha, 'Review dispatch must target only this PR and reviewed head.');
  }
}
module.exports = { hash, sourceHash, marker, decode, validatePlan, planBody, taskBody, contractBody,
  api, publish, decompose, bootstrap, validatePull, safeOutput, guardDelivery };
