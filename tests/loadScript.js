import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

// wwwroot/js/*.js files are plain non-module <script> tags in the real app — bare `function`
// declarations that attach to `window`. Importing them as ES modules would make those
// declarations module-local and unreachable, so instead we read the raw source and run it
// through indirect eval, which mirrors real <script> tag semantics and attaches everything
// to globalThis without requiring any changes to the production files.
//
// Top-level `const`/`let` are the one exception: per spec they become global *lexical*
// bindings rather than globalThis properties, and in this test runner's module context those
// lexical bindings aren't reachable from the test file at all (unlike in a real browser, where
// other classic <script> tags share that scope). Rewriting top-level (column-0, unindented)
// `const`/`let` to `var` before eval — in this in-memory copy only, never on disk — makes them
// real globalThis properties so tests can read them the same way as any other exported symbol.
export function loadScript(relativePath) {
  const absolutePath = path.resolve(process.cwd(), 'wwwroot/js', relativePath);
  const source = fs.readFileSync(absolutePath, 'utf8');
  const testable = source.replace(/^(const|let)\b/gm, 'var');
  // Without a sourceURL, V8 has no way to attribute this eval'd code back to the real file,
  // so coverage collection (and stack traces) silently report 0% for every script under test.
  // @vitest/coverage-v8 matches collected script URLs against the include glob as file:// URLs,
  // so a bare Windows path here would silently fail to merge with the on-disk file's entry.
  (0, eval)(`${testable}\n//# sourceURL=${pathToFileURL(absolutePath).href}`);
}
