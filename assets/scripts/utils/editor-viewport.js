// Measure with the textarea's actual wrapping/font/padding rather than a global
// document ratio. The hidden mirror is discarded immediately after layout.
export function measureEditorIndex(editor, index) {
  const style = getComputedStyle(editor);
  const mirror = document.createElement('div');
  const marker = document.createElement('span');
  for (const key of ['boxSizing', 'padding', 'font', 'letterSpacing', 'lineHeight', 'tabSize', 'whiteSpace', 'overflowWrap', 'wordBreak']) mirror.style[key] = style[key];
  Object.assign(mirror.style, { position: 'absolute', visibility: 'hidden', pointerEvents: 'none', left: '-9999px', top: '0', width: `${editor.clientWidth}px`, minHeight: '0', border: '0', overflow: 'hidden' });
  marker.textContent = '\u200b';
  mirror.append(document.createTextNode(editor.value.slice(0, Math.max(0, Math.min(index, editor.value.length)))), marker);
  document.body.append(mirror);
  const rect = { top: marker.offsetTop, left: marker.offsetLeft, height: marker.offsetHeight || parseFloat(style.lineHeight) || 22 };
  mirror.remove();
  return rect;
}

export function revealEditorRange(editor, start, end = start) {
  const first = measureEditorIndex(editor, start);
  const last = start === end ? first : measureEditorIndex(editor, end);
  const padding = 8;
  const top = first.top;
  const bottom = last.top + last.height;
  if (top < editor.scrollTop + padding) editor.scrollTop = Math.max(0, top - padding);
  else if (bottom > editor.scrollTop + editor.clientHeight - padding) {
    editor.scrollTop = bottom - top > editor.clientHeight - 2 * padding
      ? top - padding : bottom - editor.clientHeight + padding;
  }
  if (first.left < editor.scrollLeft) editor.scrollLeft = first.left;
  else if (first.left > editor.scrollLeft + editor.clientWidth - padding) editor.scrollLeft = first.left - editor.clientWidth + padding;
}
