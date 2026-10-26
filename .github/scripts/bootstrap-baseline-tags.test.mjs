import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const SCRIPT = fileURLToPath(new URL('./bootstrap-baseline-tags.sh', import.meta.url)).split('\\').join('/');
const GIT_ENV = {
    GIT_AUTHOR_NAME: 't', GIT_AUTHOR_EMAIL: 't@t', GIT_COMMITTER_NAME: 't', GIT_COMMITTER_EMAIL: 't@t',
};

const git = (cwd, ...args) => execFileSync('git', args, { cwd, encoding: 'utf8', env: { ...process.env, ...GIT_ENV } }).trim();

/** A work repo with a bare "origin"; `tags` are [name, commitIndex] pairs over three commits. */
function makeRepo(tags) {
    const root = mkdtempSync(join(tmpdir(), 'bootstrap-tags-'));
    const origin = join(root, 'origin.git');
    const work = join(root, 'work');
    execFileSync('git', ['init', '--bare', '-q', origin]);
    execFileSync('git', ['init', '-q', work]);
    const commits = [];
    for (let i = 0; i < 3; i++) {
        writeFileSync(join(work, 'f.txt'), String(i));
        git(work, 'add', '.');
        git(work, 'commit', '-q', '-m', `c${i}`);
        commits.push(git(work, 'rev-parse', 'HEAD'));
    }
    for (const [name, index] of tags) git(work, 'tag', name, commits[index]);
    git(work, 'remote', 'add', 'origin', origin);
    return { root, work, origin, commits };
}

function run(work, ...prefixes) {
    return spawnSync('bash', [SCRIPT, ...prefixes], { cwd: work, encoding: 'utf8', env: { ...process.env, ...GIT_ENV } });
}

test('creates and pushes each missing baseline at the newest legacy tag', () => {
    const { root, work, origin, commits } = makeRepo([['v1.2.0', 0], ['v1.10.0', 1], ['v1.9.0', 1], ['packs-1', 2]]);
    try {
        const result = run(work, 'rtst-v', 'dino-v');
        assert.equal(result.status, 0, result.stderr + result.stdout);
        for (const name of ['rtst-v1.10.0', 'dino-v1.10.0']) {
            assert.equal(git(work, 'rev-parse', `${name}^{commit}`), commits[1]);
            assert.equal(git(origin, 'rev-parse', `${name}^{commit}`), commits[1]);
        }
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
});

test('leaves a prefix that already has a tag alone', () => {
    const { root, work, origin } = makeRepo([['v1.2.0', 0], ['rtst-v1.3.0', 1]]);
    try {
        const result = run(work, 'rtst-v', 'dino-v');
        assert.equal(result.status, 0, result.stderr + result.stdout);
        assert.equal(git(origin, 'tag', '--list', 'rtst-v*'), '', 'existing tag is not re-pushed');
        assert.equal(git(origin, 'tag', '--list', 'dino-v*'), 'dino-v1.2.0');
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
});

test('fails when neither a product tag nor a legacy tag exists', () => {
    const { root, work } = makeRepo([['packs-1', 0]]);
    try {
        const result = run(work, 'rtst-v');
        assert.notEqual(result.status, 0);
        assert.match(result.stdout + result.stderr, /No rtst-v<x\.y\.z> tag and no legacy/);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
});

test('requires at least one prefix', () => {
    const { root, work } = makeRepo([]);
    try {
        assert.equal(run(work).status, 2);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
});
