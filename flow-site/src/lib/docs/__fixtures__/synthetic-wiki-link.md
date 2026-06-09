# Synthetic wiki-link fixture

This fixture exists ONLY for `transform.test.ts`. The real flow-sharp wiki contains
ZERO inter-page `[[Page-Name]]` links (RESEARCH Pitfall 7) — the only `[[` in the
wiki is array data inside a fenced code block in `Collections.md`. So the link
rewriter is unit-tested against this hand-written fixture instead of real content.

See the [[Quick-Start]] guide to get going, then read [[Note-Streams|note streams]]
for the inline note syntax.

A fenced code block must be left untouched — the `[[1,10], [2,20], [3,30]]` array
literal below mirrors the real `Collections.md:96` case and must survive verbatim:

```flow
Int[] a = [1, 2, 3]
Int[] b = [10, 20, 30]
Int[][] zipped = (zip a b)    Note: [[1,10], [2,20], [3,30]]
```

Inline code spans like `[[not-a-link]]` are also left untouched.

A second prose link to [[Effects]] confirms multiple rewrites in one pass.
