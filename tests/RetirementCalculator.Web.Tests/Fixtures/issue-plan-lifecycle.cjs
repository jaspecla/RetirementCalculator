const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const h = require(process.env.LIFECYCLE_HELPER);
const login = 'workflow-automation';
const user = { login, type: 'User' };
const repo = { owner: 'owner', repo: 'repo' };
const copy = value => JSON.parse(JSON.stringify(value));
const definition = key => ({
  key, title: `Implement ${key}`, goal: 'A bounded outcome', scope: `src/${key}, corresponding tests`,
  implementation: 'Implement the specified behavior without changing other tasks.',
  acceptance: 'Observable acceptance criteria', validation: 'dotnet test --filter task', depends_on: []
});
const plan = { summary: 'Shared issue constraints and acceptance criteria', tasks: [definition('first'), definition('second')] };
const issues = new Map([[1, { id: 1001, number: 1, title: 'Source feature', body: 'Source acceptance criteria',
  state: 'open', labels: [{ name: 'ready_for_implementation' }], user: { login: 'reporter' } }]]);
const comments = new Map();
const parents = new Map();
const pulls = new Map();
const refs = new Map([['heads/main', { object: { sha: 'base' } }]]);
const commits = new Map([['base', { sha: 'base', tree: { sha: 'tree' }, parents: [], message: 'base' }]]);
const calls = [];
let nextIssue = 10;
let nextComment = 100;
let nextCommit = 1;
let permission = 'write';
let tokenLogin = login;
let fail = '';
let failAfter = '';
let permissionChecks = 0;
const context = { repo, eventName: 'issues', actor: 'maintainer', ref: 'refs/heads/main',
  payload: { issue: copy(issues.get(1)), label: { name: 'ready_for_implementation' } } };
const core = { info() {} };
const listComments = n => comments.get(n) || [];
function mutate(name, parameters, action) {
  calls.push([name, copy(parameters)]);
  if (fail === name) { fail = ''; throw new Error(`injected ${name}`); }
  const result = action();
  if (failAfter === name) { failAfter = ''; throw new Error(`lost ${name} response`); }
  return { data: copy(result) };
}
const github = {
  rest: {
    users: { getAuthenticated: async () => ({ data: { login: tokenLogin } }) },
    repos: {
      get: async () => ({ data: { default_branch: 'main' } }),
      getCollaboratorPermissionLevel: async () => { permissionChecks++; return { data: { permission } }; },
      compareCommitsWithBasehead: async () => ({ data: { status: 'identical' } })
    },
    issues: {
      get: async p => ({ data: copy(issues.get(p.issue_number)) }),
      listComments: 'comments',
      listForRepo: 'issues',
      getComment: async p => ({ data: copy([...comments.values()].flat().find(c => c.id === p.comment_id)) }),
      createComment: async p => mutate('createComment', p, () => {
        const comment = { id: nextComment++, body: p.body, updated_at: 'now', user };
        comments.set(p.issue_number, [...listComments(p.issue_number), comment]);
        return comment;
      }),
      updateComment: async p => mutate('updateComment', p, () => {
        const comment = [...comments.values()].flat().find(c => c.id === p.comment_id);
        comment.body = p.body;
        comment.updated_at += '-updated';
        return comment;
      }),
      create: async p => mutate('createIssue', p, () => {
        const issue = { number: nextIssue++, id: nextIssue + 1000, title: p.title, body: p.body, labels: [], user, state: 'open' };
        issues.set(issue.number, issue);
        return issue;
      }),
      addLabels: async p => mutate('addLabels', p, () => {
        const issue = pulls.get(p.issue_number) || issues.get(p.issue_number);
        issue.labels.push(...p.labels.map(name => ({ name })));
        return issue.labels;
      })
    },
    pulls: {
      list: 'pulls',
      get: async p => ({ data: copy(pulls.get(p.pull_number)) }),
      create: async p => mutate('createPull', p, () => {
        const pull = { number: nextIssue++, user, title: p.title, body: p.body, state: 'open', draft: p.draft,
          head: { ref: p.head, sha: refs.get(`heads/${p.head}`).object.sha, repo: { full_name: 'owner/repo' } },
          base: { ref: p.base }, labels: [] };
        pulls.set(pull.number, pull);
        return pull;
      })
    },
    git: {
      getRef: async p => {
        if (!refs.has(p.ref)) throw Object.assign(new Error('not found'), { status: 404 });
        return { data: copy(refs.get(p.ref)) };
      },
      getCommit: async p => ({ data: copy(commits.get(p.commit_sha)) }),
      createCommit: async p => mutate('createCommit', p, () => {
        const commit = { sha: `empty-${nextCommit++}`, tree: { sha: p.tree }, parents: p.parents.map(sha => ({ sha })), message: p.message };
        commits.set(commit.sha, commit);
        return commit;
      }),
      createRef: async p => mutate('createRef', p, () => {
        const ref = { object: { sha: p.sha } };
        refs.set(p.ref.replace(/^refs\//, ''), ref);
        return ref;
      })
    },
    actions: { createWorkflowDispatch: async p => mutate('dispatch', p, () => ({})) }
  },
  paginate: async (method, p) => {
    if (method === 'comments') return copy(listComments(p.issue_number));
    if (method === 'issues') return copy([...issues.values()]);
    if (method === 'pulls') return copy([...pulls.values()].filter(i => `${repo.owner}:${i.head.ref}` === p.head));
    if (method.includes('/sub_issues')) return copy([...issues.values()].filter(i => parents.get(i.number) === p.issue_number));
    throw new Error(`Unexpected pagination ${method}`);
  },
  request: async (route, p) => {
    if (route.startsWith('GET')) {
      if (!parents.has(p.issue_number)) throw Object.assign(new Error('no parent'), { status: 404 });
      return { data: copy(issues.get(parents.get(p.issue_number))) };
    }
    return mutate('attach', p, () => {
      const child = [...issues.values()].find(i => i.id === p.sub_issue_id);
      assert.ok(child, 'Sub-issue API requires database ID, not issue number');
      parents.set(child.number, p.issue_number);
      return {};
    });
  }
};
const options = { github, context, login, core };
const item = () => ({
  plan_json: JSON.stringify(plan), expected_source_hash: h.sourceHash(issues.get(1)),
  expected_plan_hash: listComments(1).find(c => c.body.includes('<!-- issue-plan:v1 ')) ?
    h.hash(listComments(1).find(c => c.body.includes('<!-- issue-plan:v1 ')).body) : ''
});
const publish = () => h.publish({ ...options, item: item() });
const accept = async () => {
  await publish();
  issues.get(1).labels.push({ name: 'plan_accepted' });
  context.payload.label.name = 'plan_accepted';
};
const decompose = () => h.decompose(options);
const ready = async () => {
  await accept();
  await decompose();
  const child = [...issues.values()].find(i => i.number !== 1);
  child.labels.push({ name: 'implementation_ready' });
  context.payload.issue = copy(child);
  context.payload.label.name = 'implementation_ready';
  return child;
};
const bootstrap = () => h.bootstrap(options);
const count = name => calls.filter(([operation]) => operation === name).length;
const rejectWithoutWrites = async action => {
  const before = calls.length;
  await assert.rejects(action);
  assert.equal(calls.length, before);
};
const scenarios = {
  async planning() {
    await publish();
    assert.equal(pulls.size, 0);
    assert.equal(listComments(1).length, 1);
    assert.ok(listComments(1)[0].body.includes('### Implementation plan'));
    await publish();
    assert.equal(count('createComment'), 1);
    plan.summary = 'Revised summary with compatible requirements';
    await publish();
    assert.equal(listComments(1).length, 1);
    assert.equal(count('updateComment'), 1);
  },
  async feedback() {
    await publish();
    const comment = { id: 500, body: 'Please revise the validation', updated_at: 'event', user: { login: 'reporter', type: 'User' } };
    comments.get(1).push(comment);
    context.eventName = 'issue_comment';
    context.payload.comment = copy(comment);
    plan.summary = 'Revised from source issue feedback';
    await publish();
    assert.equal(count('updateComment'), 1);
    comment.body = 'Edited again';
    await rejectWithoutWrites(publish);
  },
  async noPlanFeedback() {
    context.eventName = 'issue_comment';
    const comment = { id: 500, body: 'Please plan', updated_at: 'event', user: { login: 'reporter', type: 'User' } };
    comments.set(1, [comment]);
    context.payload.comment = copy(comment);
    await rejectWithoutWrites(publish);
  },
  async stalePlan() {
    await publish();
    const stale = item();
    listComments(1)[0].body += 'concurrent edit';
    await rejectWithoutWrites(() => h.publish({ ...options, item: stale }));
  },
  async staleSource() {
    const stale = item();
    issues.get(1).body = 'Changed source';
    await rejectWithoutWrites(() => h.publish({ ...options, item: stale }));
  },
  async untrustedPlan() {
    await publish();
    listComments(1)[0].user = { login: 'attacker' };
    await rejectWithoutWrites(publish);
    issues.get(1).labels.push({ name: 'plan_accepted' });
    context.payload.label.name = 'plan_accepted';
    await rejectWithoutWrites(decompose);
  },
  async parentOnly() {
    parents.set(1, 2);
    await rejectWithoutWrites(publish);
  },
  async notPullRequest() {
    issues.get(1).pull_request = {};
    await rejectWithoutWrites(publish);
  },
  async removedReadiness() {
    issues.get(1).labels = [];
    await rejectWithoutWrites(publish);
  },
  async nonWriter() {
    permission = 'read';
    await rejectWithoutWrites(publish);
  },
  async wrongToken() {
    tokenLogin = 'someone-else';
    await rejectWithoutWrites(publish);
  },
  async nonDefaultBranch() {
    context.ref = 'refs/heads/untrusted';
    await rejectWithoutWrites(publish);
  },
  async readyBot() {
    context.actor = 'github-actions[bot]';
    permission = 'read';
    await publish();
    assert.equal(permissionChecks, 0);
    issues.get(1).labels.push({ name: 'plan_accepted' });
    context.payload.label.name = 'plan_accepted';
    await rejectWithoutWrites(decompose);
  },
  async injectedTarget() {
    await h.publish({ ...options, item: { ...item(), issue_number: 999, comment_id: 999, branch: 'main' } });
    assert.equal(listComments(1).length, 1);
    assert.equal(comments.has(999), false);
  },
  async invalidGraph() {
    plan.tasks[0].depends_on = ['second'];
    plan.tasks[1].depends_on = ['first'];
    await rejectWithoutWrites(publish);
    plan.tasks[1].depends_on = ['unknown'];
    await rejectWithoutWrites(publish);
    plan.tasks[1].key = 'first';
    await rejectWithoutWrites(publish);
  },
  async decomposition() {
    await accept();
    await decompose();
    await decompose();
    assert.equal(count('createIssue'), 2);
    assert.equal(count('attach'), 2);
    assert.equal(parents.size, 2);
    assert.equal(pulls.size, 0);
    assert.ok([...issues.values()].filter(i => i.number !== 1).every(i => i.labels.length === 0));
  },
  async lostCreateResponse() {
    await accept();
    failAfter = 'createIssue';
    await assert.rejects(decompose);
    await decompose();
    assert.equal(count('createIssue'), 2);
    assert.equal(parents.size, 2);
  },
  async attachFailure() {
    await accept();
    fail = 'attach';
    await assert.rejects(decompose);
    await decompose();
    assert.equal(count('createIssue'), 2);
    assert.equal(parents.size, 2);
  },
  async closedChildReplay() {
    await accept();
    await decompose();
    issues.get(10).state = 'closed';
    await decompose();
    assert.equal(count('createIssue'), 2);
  },
  async frozenPlan() {
    await accept();
    await decompose();
    issues.get(1).labels = [{ name: 'ready_for_implementation' }];
    context.payload.label.name = 'ready_for_implementation';
    await rejectWithoutWrites(publish);
    const comment = listComments(1)[0];
    const record = h.decode(comment.body, 'issue-plan');
    record.plan.summary = 'Changed accepted plan';
    comment.body = h.planBody(record);
    issues.get(1).labels.push({ name: 'plan_accepted' });
    context.payload.label.name = 'plan_accepted';
    await rejectWithoutWrites(decompose);
  },
  async duplicateChild() {
    await accept();
    await decompose();
    issues.set(99, { ...copy(issues.get(10)), number: 99, id: 1099 });
    await rejectWithoutWrites(decompose);
  },
  async removedChildMarker() {
    await accept();
    await decompose();
    issues.get(10).body = 'Marker removed by manual edit';
    await rejectWithoutWrites(decompose);
  },
  async wrongRelationship() {
    const child = await ready();
    parents.set(child.number, 99);
    await rejectWithoutWrites(bootstrap);
  },
  async bootstrap() {
    await ready();
    await bootstrap();
    await bootstrap();
    assert.equal(count('createPull'), 1);
    assert.equal(count('dispatch'), 1);
    const pull = [...pulls.values()][0];
    assert.equal(pull.draft, true);
    assert.deepEqual(pull.labels, [{ name: 'plan_accepted' }]);
    assert.ok(listComments(pull.number)[0].body.includes('Closes #10'));
    assert.equal(calls.find(([name]) => name === 'dispatch')[1].inputs.pull_request_number, String(pull.number));
    await h.validatePull({ ...options, pullRequest: pull.number });
  },
  async lostPrResponse() {
    await ready();
    failAfter = 'createPull';
    await assert.rejects(bootstrap);
    await bootstrap();
    assert.equal(count('createPull'), 1);
    assert.equal(count('dispatch'), 1);
  },
  async lostBranchResponse() {
    await ready();
    failAfter = 'createRef';
    await assert.rejects(bootstrap);
    await bootstrap();
    assert.equal(count('createRef'), 1);
    assert.equal(count('createPull'), 1);
    assert.equal(count('dispatch'), 1);
  },
  async lostContractResponse() {
    await ready();
    failAfter = 'createComment';
    await assert.rejects(bootstrap);
    await bootstrap();
    const pull = [...pulls.values()][0];
    assert.equal(listComments(pull.number).length, 1);
    assert.equal(count('createPull'), 1);
    assert.equal(count('dispatch'), 1);
  },
  async untrustedBranch() {
    const child = await ready();
    commits.set('bad', { sha: 'bad', tree: { sha: 'evil' }, parents: [{ sha: 'base' }],
      message: `chore: initialize implementation task #${child.number}` });
    refs.set(`heads/implementation/issue-${child.number}`, { object: { sha: 'bad' } });
    await rejectWithoutWrites(bootstrap);
  },
  async unrelatedBranch() {
    await ready();
    github.rest.repos.compareCommitsWithBasehead = async () => ({ data: { status: 'diverged' } });
    await assert.rejects(bootstrap);
    assert.equal(count('createPull'), 0);
  },
  async labelFailure() {
    await ready();
    fail = 'addLabels';
    await assert.rejects(bootstrap);
    await bootstrap();
    assert.equal(count('createPull'), 1);
    assert.equal(count('dispatch'), 1);
  },
  async uncertainDispatch() {
    await ready();
    failAfter = 'dispatch';
    await assert.rejects(bootstrap);
    await bootstrap();
    assert.equal(count('dispatch'), 1);
    assert.equal(count('createPull'), 1);
  },
  async rejectedDispatch() {
    await ready();
    fail = 'dispatch';
    await assert.rejects(bootstrap);
    await bootstrap();
    assert.equal(count('dispatch'), 1, 'Ambiguous claim needs human recovery, not an automatic duplicate');
  },
  async closedPr() {
    await ready();
    await bootstrap();
    [...pulls.values()][0].state = 'closed';
    await rejectWithoutWrites(bootstrap);
    assert.equal(count('createPull'), 1);
  },
  async commentCollision() {
    await ready();
    failAfter = 'createPull';
    await assert.rejects(bootstrap);
    const pull = [...pulls.values()][0];
    comments.set(pull.number, [{ id: 999, user: { login: 'attacker' }, body: 'Implement something else' }]);
    await rejectWithoutWrites(bootstrap);
  },
  async removedOptIn() {
    const child = await ready();
    await bootstrap();
    const pull = [...pulls.values()][0];
    child.labels = [];
    await assert.rejects(() => h.validatePull({ ...options, pullRequest: pull.number }));
    await rejectWithoutWrites(bootstrap);
  },
  async changedContract() {
    await ready();
    await bootstrap();
    const pull = [...pulls.values()][0];
    listComments(pull.number)[0].body += '\nNew unauthorized scope';
    await assert.rejects(() => h.validatePull({ ...options, pullRequest: pull.number }));
  },
  async changedParent() {
    await ready();
    await bootstrap();
    const pull = [...pulls.values()][0];
    issues.get(1).body = 'Different accepted scope';
    await assert.rejects(() => h.validatePull({ ...options, pullRequest: pull.number }));
  },
  async removedParentApproval() {
    await ready();
    await bootstrap();
    const pull = [...pulls.values()][0];
    issues.get(1).labels = [];
    await assert.rejects(() => h.validatePull({ ...options, pullRequest: pull.number }));
  },
  async staleSourceAtPublication() {
    let reads = 0;
    const get = github.rest.issues.get;
    github.rest.issues.get = async p => {
      if (++reads === 2) issues.get(1).body = 'Edited during API reads';
      return get(p);
    };
    await rejectWithoutWrites(publish);
  },
  async legacyContract() {
    pulls.set(90, { number: 90, state: 'open', labels: [{ name: 'plan_accepted' }],
      head: { ref: 'plan/issue-1-1234', sha: 'base', repo: { full_name: 'owner/repo' } } });
    await h.validatePull({ ...options, login: '', pullRequest: 90 });
  },
  async dependencies() {
    plan.tasks[1].depends_on = ['first'];
    await ready();
    await bootstrap();
    const first = [...pulls.values()][0];
    const second = issues.get(11);
    second.labels.push({ name: 'implementation_ready' });
    context.payload.issue = copy(second);
    await rejectWithoutWrites(bootstrap);
    issues.get(10).state = 'closed';
    await rejectWithoutWrites(bootstrap);
    first.merged_at = 'merged';
    first.state = 'closed';
    await bootstrap();
    assert.equal(count('createPull'), 2);
    assert.equal(count('dispatch'), 2);
  },
  async independentParallel() {
    await ready();
    await bootstrap();
    const second = issues.get(11);
    second.labels.push({ name: 'implementation_ready' });
    context.payload.issue = copy(second);
    await bootstrap();
    assert.equal(count('createPull'), 2);
    assert.equal(count('dispatch'), 2);
    assert.equal([...pulls.values()].filter(p => p.state === 'open').length, 2);
  },
  async reviewTarget() {
    await ready();
    await bootstrap();
    const pull = [...pulls.values()][0];
    context.payload = { pull_request: copy(pull) };
    const file = path.join(process.cwd(), 'outputs.json');
    fs.writeFileSync(file, JSON.stringify({ items: [{ type: 'dispatch_workflow',
      inputs: { pull_request_number: '999', reviewed_head_sha: pull.head.sha } }] }));
    await assert.rejects(() => h.guardDelivery({ ...options, review: true, outputFile: file }));
    fs.writeFileSync(file, JSON.stringify({ items: [{ type: 'dispatch_workflow',
      inputs: { pull_request_number: String(pull.number), reviewed_head_sha: pull.head.sha } }] }));
    await h.guardDelivery({ ...options, review: true, outputFile: file });
    pull.head.sha = 'new-head';
    await assert.rejects(() => h.guardDelivery({ ...options, review: true, outputFile: file }));
  },
  async multipleOutputs() {
    const file = path.join(process.cwd(), 'outputs.json');
    fs.writeFileSync(file, JSON.stringify({ items: [{ type: 'publish_issue_plan' }, { type: 'decompose_accepted_plan' }] }));
    process.env.GH_AW_AGENT_OUTPUT = file;
    await rejectWithoutWrites(() => h.safeOutput(options));
  },
  async workflowWiring() {
    const workflows = path.resolve(path.dirname(process.env.LIFECYCLE_HELPER), '../workflows');
    const planner = fs.readFileSync(path.join(workflows, 'plan-ready-issue.lock.yml'), 'utf8');
    const implementation = fs.readFileSync(path.join(workflows, 'implement-accepted-plan.lock.yml'), 'utf8');
    const review = fs.readFileSync(path.join(workflows, 'review-implemented-plan.lock.yml'), 'utf8');
    const bootstrap = fs.readFileSync(path.join(workflows, 'bootstrap-implementation-task.yml'), 'utf8');
    for (const lock of [planner, implementation, review]) assert.ok(lock.includes('"compiler_version":"v0.86.2"'));
    assert.equal(planner.includes('create_plan_pull_request'), false);
    assert.equal(planner.includes('update_plan_comment'), false);
    for (const job of ['publish_issue_plan', 'decompose_accepted_plan']) {
      const section = planner.split(`\n  ${job}:\n`)[1].split(/\n  [a-z_]+:\n/)[0];
      assert.ok(section.includes("AGENT_RESULT: ${{ needs.agent.result }}"));
      assert.ok(section.includes("DETECTION_SUCCESS: ${{ needs.detection.outputs.detection_success }}"));
      assert.ok(section.includes("process.env.DETECTION_SUCCESS !== 'true'"));
      assert.ok(section.includes('github-token: ${{ secrets.GH_AW_GITHUB_TOKEN }}'));
      assert.equal(section.includes('contents: write'), false);
      assert.equal(section.includes('pull-requests: write'), false);
    }
    assert.ok(bootstrap.includes('bootstrap-implementation-task-${{ github.event.issue.number || inputs.issue_number }}'));
    assert.ok(implementation.includes("!startsWith(github.event.pull_request.head.ref, 'implementation/')"));
    assert.ok(implementation.includes('issue-plan-lifecycle.cjs'));
    assert.ok(review.indexOf('- name: Recheck source task and bind repair dispatch') <
      review.indexOf('- name: Process Safe Outputs'));
  }
};
const scenario = process.argv[2];
assert.ok(scenarios[scenario], `Unknown scenario ${scenario}`);
scenarios[scenario]().catch(error => { console.error(error); process.exitCode = 1; });
