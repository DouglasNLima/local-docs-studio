# Native Windows edition

The reconstruction brief in docs/native/reconstruction-brief.txt is authorised work. Root browser-only restrictions protect the web product and do not prohibit this separate .NET solution.

- .NET 10, WPF/XAML, native controls. No embedded browser, JavaScript runtime, Node dependency, local server or web preview.
- Keep domain, parsing, filesystem, exports, state and updates in the Core project; Windows-specific controls and platform services in Native.
- Preserve portable UTF-8 Markdown and images. Never overwrite a changed file without conflict handling. Recovery is independent of workspace source.
- Reject workspace escapes, reparse points and active content; imported metadata cannot authorise network or filesystem access.
- Use British English, neutral surfaces and restrained #FF883E accents.
- Tests must exercise meaningful outcomes: actual files, package structure, source mapping, rejected unsafe inputs and native UI behaviour.
- Keep the five docs/native tracking documents accurate. Incomplete is a valid status; production certification requires trust, clean-machine and GUI evidence.
- Native builds and installation are isolated from src/windows and GitHub Pages. Product id, state, installer and association keys must remain distinct.
- Do not merge, publish releases, move tags, embed signing secrets or overwrite the existing installation.
