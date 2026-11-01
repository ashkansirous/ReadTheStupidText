import test from 'node:test';
import assert from 'node:assert/strict';

import { findPinMismatches, readExpectedVersions } from './check-sherpa-pins.mjs';

const EXPECTED = { sherpa: '1.13.8', onnxruntime: '1.28.0' };

test('the expected versions come from the props file', () => {
    const props = '<Project><PropertyGroup><SherpaOnnxVersion>1.13.8</SherpaOnnxVersion><OnnxRuntimeVersion>1.28.0</OnnxRuntimeVersion></PropertyGroup></Project>';
    assert.deepEqual(readExpectedVersions(props), EXPECTED);
    assert.throws(() => readExpectedVersions('<Project />'));
});

test('pins that read the property or equal the version pass', () => {
    const files = [
        { path: 'a.csproj', text: '<PackageReference Include="org.k2fsa.sherpa.onnx" Version="$(SherpaOnnxVersion)" />\r\n<PackageReference Include="Microsoft.ML.OnnxRuntime.Managed" Version="1.28.0" />' },
        { path: 'b.yml', text: 'python -m pip install "sherpa-onnx==1.13.8"' },
        { path: 'c.txt', text: 'sherpa-onnx==1.13.8' },
    ];
    assert.deepEqual(findPinMismatches(files, EXPECTED), []);
});

test('a different sherpa-onnx or onnxruntime pin is reported with file and line', () => {
    const files = [
        { path: 'a.csproj', text: 'x\n<PackageReference Include="org.k2fsa.sherpa.onnx.runtime.win-x64" Version="1.13.3" ExcludeAssets="all" />' },
        { path: 'b.yml', text: 'pip install "sherpa-onnx==1.13.3"' },
        { path: 'c.csproj', text: '<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.24.3" />' },
        { path: 'd.props', text: '<SherpaX64PinnedVersion>1.13.3</SherpaX64PinnedVersion>' },
    ];
    const problems = findPinMismatches(files, EXPECTED);
    assert.equal(problems.length, 4);
    assert.match(problems[0], /^a\.csproj:2:/);
});

test('a pin at the start of a line, Version before Include, wrapped attributes and ranges are reported', () => {
    const files = [
        { path: 'requirements.txt', text: 'sherpa-onnx==1.13.3' },
        { path: 'a.csproj', text: '<PackageReference Version="1.13.3" Include="org.k2fsa.sherpa.onnx" />' },
        { path: 'b.csproj', text: '<PackageReference\n    Include="Microsoft.ML.OnnxRuntime.Managed"\n    Version="1.24.3" />' },
        { path: 'c.csproj', text: '<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="[1.24.3]" />' },
        { path: 'd.csproj', text: '<PackageReference Include="org.k2fsa.sherpa.onnx" Version="1.13.*" />' },
        { path: 'e.csproj', text: '<PackageReference Update="org.k2fsa.sherpa.onnx" VersionOverride="1.13.3" />' },
        { path: 'f.props', text: '<SherpaIosPinnedVersion>1.13.3</SherpaIosPinnedVersion>' },
    ];
    const problems = findPinMismatches(files, EXPECTED);
    assert.equal(problems.length, files.length);
    assert.match(problems[2], /^b\.csproj:1:/);
    assert.deepEqual(findPinMismatches([{ path: 'g.csproj', text: '<PackageReference Version="[1.13.8]" Include="org.k2fsa.sherpa.onnx" />' }], EXPECTED), []);
});

test('documentation under docs/ and files outside the scanned types are left alone', () => {
    const files = [
        { path: 'docs/plan/x.txt', text: 'sherpa-onnx==1.13.3' },
        { path: 'README.md', text: 'sherpa-onnx==1.13.3' },
    ];
    assert.deepEqual(findPinMismatches(files, EXPECTED), []);
});

test('a second definition of the version property is reported', () => {
    const problems = findPinMismatches([{ path: 'x.props', text: '<SherpaOnnxVersion>1.13.3</SherpaOnnxVersion>' }], EXPECTED);
    assert.equal(problems.length, 1);
});

test('other onnxruntime packages and the quantiser pip pin are not pins of ours', () => {
    const files = [
        { path: 'a.props', text: '<PackageReference Include="Microsoft.ML.OnnxRuntime.Extensions.Dummy" Version="0.13.0" />' },
        { path: 'b.yml', text: 'pip install onnx==1.19.1 onnxruntime==1.23.2' },
    ];
    assert.deepEqual(findPinMismatches(files, EXPECTED), []);
});
