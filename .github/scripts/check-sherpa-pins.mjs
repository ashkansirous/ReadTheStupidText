// Slice 245: sherpa-onnx and onnxruntime are one version on every platform, written once in
// src/SherpaOnnxVersion.props. This fails when a pin anywhere else in the repo differs from it (or
// redefines the property), so Android, iOS and Windows cannot drift apart again.
//   node .github/scripts/check-sherpa-pins.mjs        (from the repo root; reads git ls-files)
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

export const VERSION_PROPS_PATH = 'src/SherpaOnnxVersion.props';

// Build, workflow, requirements and script files; documentation (md, docs/) may quote old versions.
const SCANNED_EXTENSIONS = /\.(csproj|props|targets|yml|yaml|txt|sh|ps1|mjs|py|json|cfg|toml)$/;
const IGNORED_PREFIXES = ['docs/', 'node_modules/'];
const SELF_PATTERN = /(^|\/)check-sherpa-pins\.(test\.)?mjs$/;

// A PackageReference is read as a whole element (attributes in any order, over several lines), so a
// pin cannot hide by being written Version-first or wrapped. A version written as $(Property) is fine.
const PACKAGE_REFERENCE = /<PackageReference\b[^>]*>/gs;
const PACKAGE_RULES = [
    { name: 'org.k2fsa.sherpa.onnx package', expect: 'sherpa', name_pattern: /^org\.k2fsa\.sherpa\.onnx[A-Za-z0-9.\-]*$/ },
    { name: 'Microsoft.ML.OnnxRuntime package', expect: 'onnxruntime', name_pattern: /^Microsoft\.ML\.OnnxRuntime(?:\.Managed)?$/ },
];
const PACKAGE_NAME_ATTRIBUTE = /\b(?:Include|Update)\s*=\s*"([^"]+)"/;
const PACKAGE_VERSION_ATTRIBUTE = /\b(?:Version|VersionOverride)\s*=\s*"([^"]*)"/;

// Each rule finds the pinned version in a line and says which expected value it must equal.
const PIN_RULES = [
    { name: 'sherpa-onnx pip pin', expect: 'sherpa', pattern: /(?:^|["'\s])sherpa-onnx==(\d[\w.]*)/ },
    { name: 'SherpaX64PinnedVersion', expect: 'sherpa', pattern: /<SherpaX64PinnedVersion>(\d[^<]*)</ },
    { name: 'SherpaIosPinnedVersion', expect: 'sherpa', pattern: /<SherpaIosPinnedVersion>(\d[^<]*)</ },
    { name: 'a second SherpaOnnxVersion definition', expect: 'sherpa', pattern: /<SherpaOnnxVersion>(\d[^<]*)</ },
    { name: 'a second OnnxRuntimeVersion definition', expect: 'onnxruntime', pattern: /<OnnxRuntimeVersion>(\d[^<]*)</ },
    { name: 'a retired SherpaIosVersion definition', expect: 'sherpa', pattern: /<SherpaIosVersion>(\d[^<]*)</ },
];

export function readExpectedVersions(propsText) {
    const sherpa = /<SherpaOnnxVersion>(\d[^<]*)</.exec(propsText)?.[1];
    const onnxruntime = /<OnnxRuntimeVersion>(\d[^<]*)</.exec(propsText)?.[1];
    if (!sherpa || !onnxruntime) {
        throw new Error(`${VERSION_PROPS_PATH} must define SherpaOnnxVersion and OnnxRuntimeVersion`);
    }
    return { sherpa, onnxruntime };
}

function isCheckedFile(path) {
    return SCANNED_EXTENSIONS.test(path)
        && !IGNORED_PREFIXES.some((prefix) => path.startsWith(prefix))
        && path !== VERSION_PROPS_PATH
        && !SELF_PATTERN.test(path);
}

function pinsInLine(line) {
    return PIN_RULES.map((rule) => ({ rule, match: rule.pattern.exec(line) })).filter((hit) => hit.match);
}

function lineOf(text, index) {
    return text.slice(0, index).split('\n').length;
}

function packageProblems(file, expected) {
    const problems = [];
    for (const element of file.text.matchAll(PACKAGE_REFERENCE)) {
        const name = PACKAGE_NAME_ATTRIBUTE.exec(element[0])?.[1];
        const version = PACKAGE_VERSION_ATTRIBUTE.exec(element[0])?.[1];
        const rule = PACKAGE_RULES.find((r) => name && r.name_pattern.test(name));
        if (!rule || !version || version.startsWith('$(')) {
            continue;
        }
        const bare = version.replace(/^[[(]|[\])]$/g, '');
        if (bare !== expected[rule.expect]) {
            problems.push(`${file.path}:${lineOf(file.text, element.index)}: ${rule.name} ${name} is ${version}, ${VERSION_PROPS_PATH} says ${expected[rule.expect]}`);
        }
    }
    return problems;
}

function linePinProblems(file, expected) {
    const problems = [];
    file.text.split(/\r?\n/).forEach((line, index) => {
        for (const { rule, match } of pinsInLine(line)) {
            if (match[1] !== expected[rule.expect]) {
                problems.push(`${file.path}:${index + 1}: ${rule.name} is ${match[1]}, ${VERSION_PROPS_PATH} says ${expected[rule.expect]}`);
            }
        }
    });
    return problems;
}

export function findPinMismatches(files, expected) {
    return files
        .filter((f) => isCheckedFile(f.path))
        .flatMap((file) => [...packageProblems(file, expected), ...linePinProblems(file, expected)]);
}

function listTrackedFiles() {
    const names = execFileSync('git', ['ls-files'], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
    return names.split(/\r?\n/).filter((name) => name && existsSync(name));
}

function main() {
    const expected = readExpectedVersions(readFileSync(VERSION_PROPS_PATH, 'utf8'));
    const files = listTrackedFiles().filter(isCheckedFile).map((path) => ({ path, text: readFileSync(path, 'utf8') }));
    const problems = findPinMismatches(files, expected);
    if (problems.length > 0) {
        console.error(problems.join('\n'));
        console.error(`\n${problems.length} sherpa-onnx/onnxruntime pin(s) differ from ${VERSION_PROPS_PATH}.`);
        process.exit(1);
    }
    console.log(`sherpa-onnx ${expected.sherpa}, onnxruntime ${expected.onnxruntime}: every pin matches ${VERSION_PROPS_PATH}.`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main();
}
