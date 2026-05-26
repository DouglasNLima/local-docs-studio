export function createContentRegistries({ slugFromText, todayIso }) {
    const sample = `# Markdown + Mermaid sample

This preview renders **Markdown** and Mermaid diagrams inside \`\`\`mermaid\`\`\` fenced blocks.

## Flow

\`\`\`mermaid
flowchart LR
  A[Markdown / MMD] --> B[Parser]
  B --> C{Source type}
  C -->|.md| D[Marked + DOMPurify]
  C -->|.mmd| E[Mermaid]
  D --> F[Browser preview]
  E --> F
\`\`\`

## Table

| File | Rendering |
|---|---|
| .md / .markdown | Markdown with Mermaid blocks |
| .mmd / .mermaid | Standalone Mermaid diagram |

## Code

\`\`\`ts
const render = async (source: string) => await renderPreview(source);
\`\`\`
`;

    const examples = {
      readme: {
        name: 'readme-example.md',
        folderName: 'Example',
        content: `# Product README

Short, practical docs help new users understand what this project does and how to start.

## Quick start

1. Open the app.
2. Choose a Markdown or Mermaid file.
3. Review the preview and export when ready.

## Workflow

\`\`\`mermaid
flowchart LR
  A[Open file] --> B[Edit Markdown]
  B --> C[Preview]
  C --> D{Ready?}
  D -->|No| B
  D -->|Yes| E[Export HTML or Word]
\`\`\`

## Notes

| Area | Detail |
|---|---|
| Privacy | Files stay in the browser |
| Hosting | Static GitHub Pages app |
| Output | HTML and Word documents |
`,
      },
      architecture: {
        name: 'architecture-example.md',
        folderName: 'Example',
        content: `# Browser-only Architecture

The app keeps rendering, editing, and exporting in the browser so it can run from GitHub Pages.

\`\`\`mermaid
flowchart TB
  subgraph Browser
    A[File picker] --> B[Markdown editor]
    B --> C[Marked parser]
    B --> D[Mermaid renderer]
    C --> E[Sanitised preview]
    D --> E
    E --> F[HTML export]
    E --> G[Word export]
  end
  H[(Local files)] --> A
  F --> I[Download]
  G --> I
\`\`\`

## Responsibilities

- The page owns rendering, saving, exporting, and theme preferences.
- Browser APIs provide file access where supported.
- Fallback downloads keep the app useful in browsers with limited file permissions.
`,
      },
      brief: {
        name: 'project-brief-example.md',
        folderName: 'Example',
        content: `# Project Brief

## Goal

Create a reliable local documentation studio that exports clean Markdown and Mermaid documents without a backend.

## Audience

Teams that need to review specs, diagrams, and notes from local files.

## Success criteria

- Open individual files or folders.
- Preview Markdown and Mermaid accurately.
- Save local edits when browser permissions allow it.
- Export HTML and Word documents with visible confidence checks.

## Timeline

\`\`\`mermaid
gantt
  title Adoption plan
  dateFormat  YYYY-MM-DD
  section UX
  First-run flow      :done,    a1, 2026-05-20, 1d
  Examples and help   :active,  a2, 2026-05-21, 1d
  section Trust
  Export checks       :         b1, 2026-05-22, 1d
  GitHub Pages docs   :         b2, 2026-05-23, 1d
\`\`\`
`,
      },
    };

    const studioTemplates = {
      flowchart: {
        name: 'flowchart.mmd',
        label: 'Flowchart',
        content: `flowchart LR
  A[Idea] --> B{Ready?}
  B -->|Yes| C[Build]
  B -->|No| D[Refine]
  D --> B
  C --> E[Ship]
`,
      },
      sequence: {
        name: 'sequence.mmd',
        label: 'Sequence',
        content: `sequenceDiagram
  participant User
  participant App
  participant Browser
  User->>App: Open diagram
  App->>Browser: Render Mermaid
  Browser-->>User: Preview and export
`,
      },
      gantt: {
        name: 'gantt-plan.mmd',
        label: 'Gantt',
        content: `gantt
  title Delivery plan
  dateFormat  YYYY-MM-DD
  section Discovery
  Requirements      :done,    a1, 2026-05-20, 2d
  Prototype         :active,  a2, 2026-05-22, 3d
  section Build
  Implementation    :         b1, 2026-05-25, 5d
  Review            :         b2, 2026-05-30, 2d
`,
      },
      class: {
        name: 'class-diagram.mmd',
        label: 'Class',
        content: `classDiagram
  class Renderer {
    +renderMarkdown()
    +renderMermaid()
    +exportHtml()
  }
  class Document {
    +title
    +source
  }
  Renderer --> Document
`,
      },
      state: {
        name: 'state-diagram.mmd',
        label: 'State',
        content: `stateDiagram-v2
  [*] --> Editing
  Editing --> Rendering: Ctrl+Enter
  Rendering --> Ready
  Ready --> Exporting
  Exporting --> Ready
`,
      },
      er: {
        name: 'entity-relationship.mmd',
        label: 'ERD',
        content: `erDiagram
  USER ||--o{ DOCUMENT : owns
  DOCUMENT ||--o{ DIAGRAM : contains
  DOCUMENT {
    string title
    string path
  }
  DIAGRAM {
    string type
    string source
  }
`,
      },
      journey: {
        name: 'journey-map.mmd',
        label: 'Journey',
        content: `journey
  title Diagram export journey
  section Create
    Pick template: 5: User
    Edit source: 4: User
  section Share
    Render preview: 5: App
    Export image: 5: User
`,
      },
      mindmap: {
        name: 'mindmap.mmd',
        label: 'Mindmap',
        content: `mindmap
  root((Mermaid Studio))
    Templates
      Flowchart
      Sequence
      Gantt
    Exports
      SVG
      PNG
      Source
`,
      },
      pie: {
        name: 'pie-chart.mmd',
        label: 'Pie',
        content: `pie showData
  title Export formats
  "HTML" : 35
  "Word" : 25
  "SVG" : 20
  "PNG" : 20
`,
      },
      timeline: {
        name: 'timeline.mmd',
        label: 'Timeline',
        content: `timeline
  title Product milestones
  2026-05-20 : Markdown renderer
             : Mermaid support
  2026-05-21 : Docs site builder
  2026-05-22 : Diagram Studio
`,
      },
    };

    const studioSnippets = {
      subgraph: `subgraph Browser
  A[Editor] --> B[Preview]
end
`,
      decision: `B{Decision?}
B -->|Yes| C[Continue]
B -->|No| D[Revise]
`,
      sequenceNote: `Note over User,App: Add context here
`,
      style: `style A fill:#FFF1E8,stroke:#FF883E,stroke-width:2px
`,
      classDef: `classDef important fill:#FFF1E8,stroke:#FF883E,color:#111827
class A important
`,
      linkStyle: `linkStyle 0 stroke:#FF883E,stroke-width:2px
`,
    };

    const projectDocTemplates = {
      readme: {
        label: 'README',
        mode: 'Project Docs',
        fields: [
          { key: 'title', label: 'Project name', fallback: 'Project Name' },
          { key: 'audience', label: 'Primary audience', fallback: 'Developers and operators' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'project')}-readme.md`,
        content: (meta) => `# ${meta.title}

Short description of what this project does and why it exists.

## Who this is for

${meta.audience}

## Quick start

\`\`\`sh
# Install dependencies
npm install

# Run locally
npm run dev
\`\`\`

## Usage

1. Configure the environment.
2. Run the application.
3. Review output and logs.

## Architecture

\`\`\`mermaid
flowchart LR
  User[User] --> App[Application]
  App --> Data[(Data)]
  App --> Export[Outputs]
\`\`\`

## Configuration

| Setting | Purpose | Default |
|---|---|---|
| ENVIRONMENT | Runtime environment | local |
| LOG_LEVEL | Logging verbosity | info |

## Development

- Keep changes small and reviewed.
- Update tests when behaviour changes.
- Document operational assumptions.

## Support

Add ownership, escalation, and troubleshooting notes here.
`,
      },
      architecture: {
        label: 'Architecture Overview',
        mode: 'Project Docs',
        fields: [
          { key: 'title', label: 'System name', fallback: 'System Architecture' },
          { key: 'audience', label: 'Audience', fallback: 'Engineering and product stakeholders' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'architecture')}-architecture.md`,
        content: (meta) => `# ${meta.title} Architecture

## Audience

${meta.audience}

## Context

Describe the system boundary, users, and core business capability.

## System View

\`\`\`mermaid
flowchart TB
  subgraph Client
    UI[User Interface]
  end
  subgraph Application
    API[API Layer]
    Services[Domain Services]
  end
  subgraph Data
    Store[(Primary Store)]
  end
  UI --> API
  API --> Services
  Services --> Store
\`\`\`

## Key Components

| Component | Responsibility | Owner |
|---|---|---|
| User Interface | User workflows | TBD |
| API Layer | Request handling | TBD |
| Domain Services | Business logic | TBD |
| Primary Store | Durable data | TBD |

## Important Decisions

- Decision 1: TBD
- Decision 2: TBD

## Risks

- Risk: TBD
- Mitigation: TBD
`,
      },
      adr: {
        label: 'ADR',
        mode: 'Project Docs',
        fields: [
          { key: 'title', label: 'Decision title', fallback: 'Use Browser-only Architecture' },
          { key: 'date', label: 'Decision date', fallback: () => todayIso() },
        ],
        name: (meta) => `adr-${slugFromText(meta.title || 'decision')}.md`,
        content: (meta) => `# ADR: ${meta.title}

Date: ${meta.date}

Status: Proposed

## Context

Describe the problem, constraints, and forces affecting this decision.

## Decision

State the decision in one or two clear paragraphs.

## Consequences

### Positive

- Outcome: TBD

### Negative

- Tradeoff: TBD

### Neutral

- Follow-up: TBD

## Alternatives Considered

| Option | Pros | Cons | Decision |
|---|---|---|---|
| Option A | TBD | TBD | TBD |
| Option B | TBD | TBD | TBD |
`,
      },
      runbook: {
        label: 'Runbook',
        mode: 'Project Docs',
        fields: [
          { key: 'title', label: 'Service or workflow name', fallback: 'Service Runbook' },
          { key: 'owner', label: 'Owner/team', fallback: 'Team Name' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'runbook')}-runbook.md`,
        content: (meta) => `# ${meta.title} Runbook

Owner: ${meta.owner}

## Purpose

Describe what this runbook helps operators diagnose or complete.

## Normal Operation

- Expected status: TBD
- Expected logs: TBD
- Expected dashboards: TBD

## Common Tasks

### Start

\`\`\`sh
command-to-start
\`\`\`

### Stop

\`\`\`sh
command-to-stop
\`\`\`

### Health Check

\`\`\`sh
command-to-check-health
\`\`\`

## Troubleshooting

| Symptom | Likely Cause | Action |
|---|---|---|
| TBD | TBD | TBD |

## Escalation

Add contacts, support windows, and escalation path.
`,
      },
      api: {
        label: 'API Notes',
        mode: 'Project Docs',
        fields: [
          { key: 'title', label: 'API name', fallback: 'Project API' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'api')}-api-notes.md`,
        content: (meta) => `# ${meta.title} Notes

## Overview

Describe the API purpose, consumers, and authentication model.

## Endpoints

| Method | Path | Purpose |
|---|---|---|
| GET | /health | Health check |
| GET | /items | List items |
| POST | /items | Create item |

## Request Example

\`\`\`json
{
  "name": "Example"
}
\`\`\`

## Response Example

\`\`\`json
{
  "id": "item_123",
  "name": "Example"
}
\`\`\`

## Error Handling

| Status | Meaning | Recovery |
|---|---|---|
| 400 | Invalid request | Fix input |
| 401 | Unauthorized | Check credentials |
| 500 | Server error | Retry or escalate |
`,
      },
      onboarding: {
        label: 'Onboarding Guide',
        mode: 'Project Docs',
        fields: [
          { key: 'title', label: 'Team/project name', fallback: 'Project' },
          { key: 'audience', label: 'New joiner audience', fallback: 'New team members' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'project')}-onboarding.md`,
        content: (meta) => `# ${meta.title} Onboarding

Audience: ${meta.audience}

## First Day

- Get repository access.
- Set up local environment.
- Read the README and architecture overview.

## First Week

- Run the app locally.
- Pick a starter issue.
- Pair with a maintainer.

## Core Concepts

| Concept | Why it matters |
|---|---|
| Domain model | TBD |
| Deployment flow | TBD |
| Support process | TBD |

## Useful Links

- Repository: TBD
- Docs: TBD
- Dashboard: TBD
`,
      },
      docsIndex: {
        label: 'Docs Pack Index',
        mode: 'Project Docs',
        fields: [
          { key: 'title', label: 'Docs pack name', fallback: 'Project Docs' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'docs')}-index.md`,
        content: (meta) => `# ${meta.title}

Use this page as the entry point for a docs site bundle.

## Start Here

- [README](README.md)
- [Architecture](architecture.md)
- [Runbook](runbook.md)
- [Release Notes](release-notes.md)
- [Requirements](requirements.md)

## Docs Map

\`\`\`mermaid
flowchart LR
  Index[Docs Index] --> Readme[README]
  Index --> Architecture[Architecture]
  Index --> Runbook[Runbook]
  Index --> Releases[Release Notes]
  Index --> Requirements[Requirements]
\`\`\`
`,
      },
    };

    const releaseNoteTemplates = {
      customer: {
        label: 'Customer Release Notes',
        mode: 'Release Notes',
        fields: [
          { key: 'version', label: 'Release version', fallback: 'v1.0.0' },
          { key: 'date', label: 'Release date', fallback: () => todayIso() },
        ],
        name: (meta) => `release-notes-${slugFromText(meta.version || 'v1-0-0')}.md`,
        content: (meta) => `# Release Notes ${meta.version}

Release date: ${meta.date}

## Highlights

- Highlight 1: TBD
- Highlight 2: TBD

## New

- New capability: TBD

## Improved

- Improvement: TBD

## Fixed

- Fix: TBD

## Known Issues

- Known issue: TBD

## Release Timeline

\`\`\`mermaid
timeline
  title ${meta.version} rollout
  ${meta.date} : Release prepared
              : Customer notes published
\`\`\`
`,
      },
      changelog: {
        label: 'Technical Changelog',
        mode: 'Release Notes',
        fields: [
          { key: 'version', label: 'Version', fallback: 'v1.0.0' },
          { key: 'date', label: 'Date', fallback: () => todayIso() },
        ],
        name: (meta) => `changelog-${slugFromText(meta.version || 'v1-0-0')}.md`,
        content: (meta) => `# Changelog ${meta.version}

Date: ${meta.date}

## Added

- TBD

## Changed

- TBD

## Fixed

- TBD

## Deprecated

- TBD

## Security

- TBD

## Breaking Changes

- TBD
`,
      },
      sprint: {
        label: 'Sprint Summary',
        mode: 'Release Notes',
        fields: [
          { key: 'title', label: 'Sprint name', fallback: 'Sprint Summary' },
          { key: 'date', label: 'Summary date', fallback: () => todayIso() },
        ],
        name: (meta) => `${slugFromText(meta.title || 'sprint')}.md`,
        content: (meta) => `# ${meta.title}

Date: ${meta.date}

## Goals

- Goal: TBD

## Completed

- Completed work: TBD

## Carried Forward

- Item: TBD

## Risks And Blockers

- Risk: TBD

## Metrics

| Metric | Value | Notes |
|---|---:|---|
| Stories completed | TBD | TBD |
| Defects fixed | TBD | TBD |
`,
      },
      migration: {
        label: 'Migration Notes',
        mode: 'Release Notes',
        fields: [
          { key: 'version', label: 'Target version', fallback: 'v1.0.0' },
        ],
        name: (meta) => `migration-${slugFromText(meta.version || 'v1-0-0')}.md`,
        content: (meta) => `# Migration Notes ${meta.version}

## Who Needs This

Describe affected users, systems, and environments.

## What Changed

- Change: TBD

## Before You Upgrade

- Backup data.
- Review breaking changes.
- Confirm rollback plan.

## Migration Steps

1. Step one.
2. Step two.
3. Validate the result.

## Rollback

Describe rollback commands and decision criteria.

## Validation

| Check | Expected Result |
|---|---|
| Health check | Passing |
| Key workflow | Successful |
`,
      },
    };

    const requirementsTemplates = {
      prd: {
        label: 'PRD',
        mode: 'Requirements',
        fields: [
          { key: 'title', label: 'Product/feature name', fallback: 'Feature Name' },
          { key: 'audience', label: 'Target users', fallback: 'Target users' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'feature')}-prd.md`,
        content: (meta) => `# PRD: ${meta.title}

## Target Users

${meta.audience}

## Problem

Describe the problem and why it matters now.

## Goals

- Goal: TBD

## Non-goals

- Non-goal: TBD

## User Journey

\`\`\`mermaid
flowchart LR
  A[Need] --> B[Discover feature]
  B --> C[Complete workflow]
  C --> D[Measure success]
\`\`\`

## Requirements

| ID | Requirement | Priority |
|---|---|---|
| R1 | TBD | Must |

## Success Metrics

- Metric: TBD

## Open Questions

- Question: TBD
`,
      },
      featureBrief: {
        label: 'Feature Brief',
        mode: 'Requirements',
        fields: [
          { key: 'title', label: 'Feature name', fallback: 'Feature Brief' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'feature')}-brief.md`,
        content: (meta) => `# ${meta.title}

## Summary

One paragraph explaining the feature.

## Customer Need

Describe the need in plain language.

## Proposed Experience

- Step 1: TBD
- Step 2: TBD

## Scope

### In Scope

- TBD

### Out Of Scope

- TBD

## Risks

- Risk: TBD
`,
      },
      storySet: {
        label: 'User Story Set',
        mode: 'Requirements',
        fields: [
          { key: 'title', label: 'Epic name', fallback: 'Epic Name' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'epic')}-stories.md`,
        content: (meta) => `# User Stories: ${meta.title}

## Epic

As a user, I want a clear workflow so that I can complete my goal with confidence.

## Stories

### Story 1

As a [persona], I want [capability], so that [outcome].

Acceptance criteria:

- Given [context], when [action], then [result].

### Story 2

As a [persona], I want [capability], so that [outcome].

Acceptance criteria:

- Given [context], when [action], then [result].
`,
      },
      devopsConclusion: {
        label: 'DevOps User Story / Task Conclusion',
        mode: 'Requirements',
        fields: [
          { key: 'title', label: 'Story or task title', fallback: 'Completed User Story' },
          { key: 'workItemId', label: 'Azure DevOps work item ID', fallback: 'AB#0000' },
          { key: 'developer', label: 'Developer', fallback: 'Developer Name' },
          { key: 'date', label: 'Completion date', fallback: () => todayIso() },
        ],
        name: (meta) => `${slugFromText(meta.workItemId || meta.title || 'devops-task')}-conclusion.md`,
        content: (meta) => `# DevOps User Story / Task Conclusion: ${meta.title}

Work item: ${meta.workItemId}
Developer: ${meta.developer}
Date: ${meta.date}

## Outcome

Summarise what was completed and why the story or task is ready for review.

## Implementation Details

- Approach: TBD
- Important decisions: TBD
- Constraints or assumptions: TBD

## Components Created Or Changed

| Component | Type | Change | Notes |
|---|---|---|---|
| TBD | Created/Changed | TBD | TBD |

## How To Test

1. Test step one.
2. Test step two.
3. Confirm the expected result.

## Test Evidence

| Check | Result | Evidence |
|---|---|---|
| Unit/static checks | TBD | TBD |
| Browser/manual validation | TBD | TBD |
| Regression area | TBD | TBD |

## Deployment Requirements

- Configuration changes: TBD
- Feature flags: TBD
- Data/schema changes: TBD
- Environment variables or secrets: TBD
- Release order or dependency: TBD

## Rollback Plan

Describe how to revert safely if deployment validation fails.

## Reviewer Notes

- Known limitations: TBD
- Follow-up work: TBD
- Screenshots or links: TBD
`,
      },
      acceptance: {
        label: 'Acceptance Criteria',
        mode: 'Requirements',
        fields: [
          { key: 'title', label: 'Feature name', fallback: 'Feature' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'feature')}-acceptance.md`,
        content: (meta) => `# Acceptance Criteria: ${meta.title}

## Functional Criteria

- [ ] Criterion 1
- [ ] Criterion 2
- [ ] Criterion 3

## Non-functional Criteria

- [ ] Performance: TBD
- [ ] Accessibility: TBD
- [ ] Security: TBD

## Definition Of Done

- [ ] Tests are updated.
- [ ] Documentation is updated.
- [ ] Stakeholders have reviewed the result.
`,
      },
      gherkin: {
        label: 'Gherkin Scenarios',
        mode: 'Requirements',
        fields: [
          { key: 'title', label: 'Feature name', fallback: 'Feature' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'feature')}-gherkin.md`,
        content: (meta) => `# Gherkin: ${meta.title}

\`\`\`gherkin
Feature: ${meta.title}

  Scenario: Successful workflow
    Given the user has the required context
    When the user completes the main action
    Then the expected result is shown

  Scenario: Validation error
    Given the user provides incomplete input
    When the user submits the form
    Then a clear validation message is shown
\`\`\`
`,
      },
      journey: {
        label: 'Journey Map',
        mode: 'Requirements',
        fields: [
          { key: 'title', label: 'Journey name', fallback: 'User Journey' },
        ],
        name: (meta) => `${slugFromText(meta.title || 'journey')}-journey.md`,
        content: (meta) => `# ${meta.title}

## Persona

Describe the user, their context, and their goal.

## Journey

\`\`\`mermaid
journey
  title ${meta.title}
  section Discover
    Understand need: 3: User
    Find option: 4: User
  section Complete
    Start workflow: 4: User
    Finish goal: 5: User
  section Follow up
    Confirm result: 5: User
\`\`\`

## Pain Points

- Pain point: TBD

## Opportunities

- Opportunity: TBD
`,
      },
    };

    const projectDocSnippets = {
      install: `## Installation

\`\`\`sh
npm install
npm run dev
\`\`\`
`,
      usage: `## Usage

1. Start the application.
2. Complete the primary workflow.
3. Review the output.
`,
      supportMatrix: `## Support Matrix

| Environment | Supported | Notes |
|---|---|---|
| Chrome / Edge | Yes | Full local save support |
| Firefox | Partial | Download fallback for save |
| Safari | Partial | Download fallback for save |
`,
      roadmap: `## Roadmap

- Now: Stabilise the core workflow.
- Next: Improve documentation and onboarding.
- Later: Add optional automation around repeated tasks.
`,
      architectureBlock: `## Architecture

\`\`\`mermaid
flowchart TB
  User[User] --> UI[Browser UI]
  UI --> Logic[Local app logic]
  Logic --> Preview[Rendered preview]
  Logic --> Export[Export files]
\`\`\`
`,
    };

    const releaseNoteSnippets = {
      added: `## Added

- New capability: TBD
`,
      changed: `## Changed

- Changed behaviour: TBD
`,
      fixed: `## Fixed

- Fix: TBD
`,
      deprecated: `## Deprecated

- Deprecated item: TBD
`,
      security: `## Security

- Security update: TBD
`,
      breaking: `## Breaking Changes

- Breaking change: TBD

### Migration

1. Review affected workflows.
2. Update configuration or code.
3. Validate the result.
`,
      knownIssues: `## Known Issues

| Issue | Impact | Workaround |
|---|---|---|
| TBD | TBD | TBD |
`,
    };

    const requirementsSnippets = {
      persona: `## Persona

- Name: TBD
- Role: TBD
- Goal: TBD
- Pain point: TBD
`,
      problem: `## Problem Statement

Users need TBD because TBD. Solving this should improve TBD.
`,
      nonGoals: `## Non-goals

- This will not cover TBD.
- This will not replace TBD.
`,
      metrics: `## Success Metrics

| Metric | Target | Measurement |
|---|---:|---|
| Adoption | TBD | TBD |
| Completion rate | TBD | TBD |
| Support contacts | TBD | TBD |
`,
      risks: `## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| TBD | Medium | Medium | TBD |
`,
      openQuestions: `## Open Questions

- Question: TBD
- Decision owner: TBD
- Needed by: TBD
`,
      flow: `## User Flow

\`\`\`mermaid
flowchart LR
  A[Start] --> B[Choose option]
  B --> C[Complete task]
  C --> D[Confirm outcome]
\`\`\`
`,
    };

    const generatorGroups = {
      project: {
        label: 'Project Docs',
        folderName: 'Project Docs Generator',
        templates: projectDocTemplates,
        snippets: projectDocSnippets,
      },
      release: {
        label: 'Release Notes',
        folderName: 'Release Notes Builder',
        templates: releaseNoteTemplates,
        snippets: releaseNoteSnippets,
      },
      requirements: {
        label: 'Requirements',
        folderName: 'Requirements Studio',
        templates: requirementsTemplates,
        snippets: requirementsSnippets,
      },
    };

    return {
      sample,
      examples,
      studioTemplates,
      studioSnippets,
      projectDocTemplates,
      releaseNoteTemplates,
      requirementsTemplates,
      projectDocSnippets,
      releaseNoteSnippets,
      requirementsSnippets,
      generatorGroups,
    };
}
