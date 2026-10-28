# Pinned ER layout adapter

BoundedErLayout.cs and the two text-metric helpers are adapted from Mermaider 0.15.1, upstream commit 71b64845570d633a94d29e85579c0a02786fece8 (https://github.com/nullean/mermaider).

The adapter calls the SAME pinned Sugiyama 0.15.1 library and returns Mermaider models to the SAME pinned parser/SVG pipeline. It is not a second rendering engine. Exact node sizing is obtained from the pinned provider without relationships. Label metrics, occurrence mapping, self-loop routing and canvas padding follow the pinned adapter. Changes supply cancellation and a bounded search policy; missing outputs fail closed. See the Goal #52 engineering receipt for measurements and geometry acceptance.

Upstream licence:
MIT License

Copyright (c) 2025 Nullean

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
